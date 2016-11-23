﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Messaging.Amqp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Azure.Amqp.Encoding;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Primitives;

    sealed class AmqpMessageReceiver : MessageReceiver
    {
        public static readonly TimeSpan DefaultBatchFlushInterval = TimeSpan.FromMilliseconds(20);
        readonly ConcurrentExpiringSet<Guid> requestResponseLockedMessages;

        public AmqpMessageReceiver(QueueClient queueClient)
            : base(queueClient.Mode)
        {
            this.QueueClient = queueClient;
            this.Path = queueClient.QueueName;
            this.ReceiveLinkManager = new FaultTolerantAmqpObject<ReceivingAmqpLink>(this.CreateLinkAsync, this.CloseSession);
            this.RequestResponseLinkManager = new FaultTolerantAmqpObject<RequestResponseAmqpLink>(this.CreateRequestResponseLinkAsync, this.CloseRequestResponseSession);
            this.requestResponseLockedMessages = new ConcurrentExpiringSet<Guid>();
            this.PrefetchCount = queueClient.PrefetchCount;
        }

        /// <summary>
        /// Get Prefetch Count configured on the Receiver.
        /// </summary>
        /// <value>The upper limit of events this receiver will actively receive regardless of whether a receive operation is pending.</value>
        public int PrefetchCount { get; set; }

        QueueClient QueueClient { get; }

        string Path { get; }

        FaultTolerantAmqpObject<ReceivingAmqpLink> ReceiveLinkManager { get; }

        FaultTolerantAmqpObject<RequestResponseAmqpLink> RequestResponseLinkManager { get; }

        public override Task CloseAsync()
        {
            return this.ReceiveLinkManager.CloseAsync();
        }

        protected override async Task<IList<BrokeredMessage>> OnReceiveAsync(int maxMessageCount)
        {
            try
            {
                var timeoutHelper = new TimeoutHelper(this.QueueClient.ConnectionSettings.OperationTimeout, true);
                ReceivingAmqpLink receiveLink = await this.ReceiveLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime());
                IEnumerable<AmqpMessage> amqpMessages = null;
                bool hasMessages = await Task.Factory.FromAsync(
                    (c, s) => receiveLink.BeginReceiveRemoteMessages(maxMessageCount, AmqpMessageReceiver.DefaultBatchFlushInterval, timeoutHelper.RemainingTime(), c, s),
                    (a) => receiveLink.EndReceiveMessages(a, out amqpMessages),
                    this);

                if (receiveLink.TerminalException != null)
                {
                    throw receiveLink.TerminalException;
                }

                if (hasMessages && amqpMessages != null)
                {
                    IList<BrokeredMessage> brokeredMessages = null;
                    foreach (var amqpMessage in amqpMessages)
                    {
                        if (brokeredMessages == null)
                        {
                            brokeredMessages = new List<BrokeredMessage>();
                        }

                        if (this.QueueClient.Mode == ReceiveMode.ReceiveAndDelete)
                        {
                            receiveLink.DisposeDelivery(amqpMessage, true, AmqpConstants.AcceptedOutcome);
                        }

                        BrokeredMessage brokeredMessage = AmqpMessageConverter.ClientGetMessage(amqpMessage);
                        brokeredMessage.Receiver = this; // Associate the Message with this Receiver.
                        brokeredMessages.Add(brokeredMessage);
                    }

                    return brokeredMessages;
                }
                else
                {
                    return null;
                }
            }
            catch (AmqpException amqpException)
            {
                throw AmqpExceptionHelper.ToMessagingContract(amqpException.Error);
            }
        }

        protected override async Task<IList<BrokeredMessage>> OnReceiveBySequenceNumberAsync(IEnumerable<long> sequenceNumbers)
        {
            List<BrokeredMessage> messages = new List<BrokeredMessage>();
            try
            {
                AmqpRequestMessage requestMessage = AmqpRequestMessage.CreateRequest(ManagementConstants.Operations.ReceiveBySequenceNumberOperation, this.QueueClient.ConnectionSettings.OperationTimeout, null);
                requestMessage.Map[ManagementConstants.Properties.SequenceNumbers] = sequenceNumbers.ToArray();
                requestMessage.Map[ManagementConstants.Properties.ReceiverSettleMode] = (uint)(this.ReceiveMode == ReceiveMode.ReceiveAndDelete ? 0 : 1);

                AmqpResponseMessage response = await this.ExecuteRequestResponseAsync(requestMessage.AmqpMessage);

                if (response.StatusCode == AmqpResponseStatusCode.OK)
                {
                    var amqpMapList = response.GetListValue<AmqpMap>(ManagementConstants.Properties.Messages);
                    foreach (AmqpMap entry in amqpMapList)
                    {
                        var payload = (ArraySegment<byte>)entry[ManagementConstants.Properties.Message];
                        AmqpMessage amqpMessage = AmqpMessage.CreateAmqpStreamMessage(new BufferListStream(new[] { payload }), true);
                        BrokeredMessage brokeredMessage = AmqpMessageConverter.ClientGetMessage(amqpMessage);
                        brokeredMessage.Receiver = this; // Associate the Message with this Receiver.
                        Guid lockToken;
                        if (entry.TryGetValue(ManagementConstants.Properties.LockToken, out lockToken))
                        {
                            brokeredMessage.LockToken = lockToken;
                            this.requestResponseLockedMessages.AddOrUpdate(lockToken, brokeredMessage.LockedUntilUtc);
                        }

                        messages.Add(brokeredMessage);
                    }
                }
            }
            catch (AmqpException amqpException)
            {
                throw AmqpExceptionHelper.ToMessagingContract(amqpException.Error);
            }

            return messages;
        }


        protected override async Task OnCompleteAsync(IEnumerable<Guid> lockTokens)
        {
            try
            {
                if (lockTokens.Any((lt) => this.requestResponseLockedMessages.Contains(lt)))
                {
                    await this.DisposeMessageRequestResponseAsync(lockTokens, DispositionStatus.Completed);
                }
                else
                {
                    await this.DisposeMessagesAsync(lockTokens, AmqpConstants.AcceptedOutcome); 
                }
            }
            catch (AmqpException amqpException)
            {
                throw AmqpExceptionHelper.ToMessagingContract(amqpException.Error);
            }
        }

        protected override async Task OnAbandonAsync(IEnumerable<Guid> lockTokens)
        {
            try
            {
                if (lockTokens.Any((lt) => this.requestResponseLockedMessages.Contains(lt)))
                {
                    await this.DisposeMessageRequestResponseAsync(lockTokens, DispositionStatus.Abandoned);
                }
                else
                {
                    await DisposeMessagesAsync(lockTokens, new Modified());
                }
            }
            catch (AmqpException amqpException)
            {
                throw AmqpExceptionHelper.ToMessagingContract(amqpException.Error);
            }
        }

        protected override async Task OnDeferAsync(IEnumerable<Guid> lockTokens)
        {
            try
            {
                if (lockTokens.Any((lt) => this.requestResponseLockedMessages.Contains(lt)))
                {
                    await this.DisposeMessageRequestResponseAsync(lockTokens, DispositionStatus.Defered);
                }
                else
                {
                    await this.DisposeMessagesAsync(lockTokens, new Modified() { UndeliverableHere = true });
                }
            }
            catch (AmqpException amqpException)
            {
                throw AmqpExceptionHelper.ToMessagingContract(amqpException.Error);
            }
        }

        protected override async Task OnDeadLetterAsync(IEnumerable<Guid> lockTokens)
        {
            try
            {
                if (lockTokens.Any((lt) => this.requestResponseLockedMessages.Contains(lt)))
                {
                    await this.DisposeMessageRequestResponseAsync(lockTokens, DispositionStatus.Suspended);
                }
                else
                {
                    await this.DisposeMessagesAsync(lockTokens, AmqpConstants.RejectedOutcome);
                }
            }
            catch (AmqpException amqpException)
            {
                throw AmqpExceptionHelper.ToMessagingContract(amqpException.Error);
            }
        }

        protected override async Task<DateTime> OnRenewLockAsync(Guid lockToken)
        {
            DateTime lockedUntilUtc = DateTime.MinValue;
            try
            {
                // Create an AmqpRequest Message to renew  lock
                AmqpRequestMessage requestMessage = AmqpRequestMessage.CreateRequest(ManagementConstants.Operations.RenewLockOperation, this.QueueClient.ConnectionSettings.OperationTimeout, null);
                requestMessage.Map[ManagementConstants.Properties.LockTokens] = new Guid[] {lockToken};

                AmqpResponseMessage response = await this.ExecuteRequestResponseAsync(requestMessage.AmqpMessage);

                if (response.StatusCode == AmqpResponseStatusCode.OK)
                {
                    IEnumerable<DateTime> lockedUntilUtcTimes = response.GetValue<IEnumerable<DateTime>>(ManagementConstants.Properties.Expirations);
                    lockedUntilUtc = lockedUntilUtcTimes.First();
                }
            }
            catch (AmqpException amqpException)
            {
                throw AmqpExceptionHelper.ToMessagingContract(amqpException.Error);
            }

            return lockedUntilUtc;
        }

        async Task DisposeMessagesAsync(IEnumerable<Guid> lockTokens, Outcome outcome)
        {
            var timeoutHelper = new TimeoutHelper(this.QueueClient.ConnectionSettings.OperationTimeout, true);
            IList<ArraySegment<byte>> deliveryTags = ConvertLockTokensToDeliveryTags(lockTokens);

            ReceivingAmqpLink receiveLink = await this.ReceiveLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime());
            Task[] disposeMessageTasks = new Task[deliveryTags.Count];
            int i = 0;
            foreach (ArraySegment<byte> deliveryTag in deliveryTags)
            {
                disposeMessageTasks[i++] = Task.Factory.FromAsync(
                        (c, s) => receiveLink.BeginDisposeMessage(deliveryTag, outcome, true, timeoutHelper.RemainingTime(), c, s),
                        (a) => receiveLink.EndDisposeMessage(a),
                        this);
            }

            Task.WaitAll(disposeMessageTasks);
        }

        async Task DisposeMessageRequestResponseAsync(IEnumerable<Guid> lockTokens, DispositionStatus  dispositionStatus)
        {
            try
            {
                // Create an AmqpRequest Message to update disposition
                AmqpRequestMessage requestMessage = AmqpRequestMessage.CreateRequest(ManagementConstants.Operations.UpdateDispositionOperation, this.QueueClient.ConnectionSettings.OperationTimeout, null);
                requestMessage.Map[ManagementConstants.Properties.LockTokens] = lockTokens.ToArray();
                requestMessage.Map[ManagementConstants.Properties.DispositionStatus] = dispositionStatus.ToString().ToLowerInvariant();

                await this.ExecuteRequestResponseAsync(requestMessage.AmqpMessage);
            }
            catch (AmqpException amqpException)
            {
                throw AmqpExceptionHelper.ToMessagingContract(amqpException.Error);
            }
        }

        async Task<AmqpResponseMessage> ExecuteRequestResponseAsync(AmqpMessage requestAmqpMessage)
        {
            var timeoutHelper = new TimeoutHelper(this.QueueClient.ConnectionSettings.OperationTimeout, true);
            RequestResponseAmqpLink requestResponseAmqpLink = await this.RequestResponseLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime());

            AmqpMessage responseAmqpMessage = await Task.Factory.FromAsync(
                (c, s) => requestResponseAmqpLink.BeginRequest(requestAmqpMessage, timeoutHelper.RemainingTime(), c, s),
                (a) => requestResponseAmqpLink.EndRequest(a),
                this);

            AmqpResponseMessage responseMessage = AmqpResponseMessage.CreateResponse(responseAmqpMessage);
            return responseMessage;
        }

        IList<ArraySegment<byte>> ConvertLockTokensToDeliveryTags(IEnumerable<Guid> lockTokens)
        {
            return lockTokens.Select(lockToken => new ArraySegment<Byte>(lockToken.ToByteArray())).ToList();
        }

        async Task<ReceivingAmqpLink> CreateLinkAsync(TimeSpan timeout)
        {
            var linkSettings = new AmqpLinkSettings
            {
                Role = true,
                TotalLinkCredit = (uint) this.PrefetchCount,
                AutoSendFlow = this.PrefetchCount > 0,
                Source = new Source { Address = this.Path },
                Target = new Target { Address = this.ClientId },
                SettleType = (this.QueueClient.Mode == ReceiveMode.PeekLock) ? SettleMode.SettleOnDispose : SettleMode.SettleOnSend
            };
            linkSettings.AddProperty(AmqpClientConstants.EntityTypeName, (int)MessagingEntityType.Queue);

            ReceivingAmqpLink receivingAmqpLink = (ReceivingAmqpLink) await AmqpLinkHelper.CreateAndOpenAmqpLinkAsync((AmqpQueueClient) this.QueueClient, this.Path, new[] {ClaimConstants.Listen}, linkSettings, false);
            return receivingAmqpLink;
        }

        //TODO: Consolidate the link creation paths
        async Task<RequestResponseAmqpLink> CreateRequestResponseLinkAsync(TimeSpan timeout)
        {
            string entityPath = this.Path + '/' + AmqpClientConstants.ManagementAddress;
            var linkSettings = new AmqpLinkSettings();
            linkSettings.AddProperty(AmqpClientConstants.EntityTypeName, AmqpClientConstants.EntityTypeManagement);

            RequestResponseAmqpLink requestResponseAmqpLink = (RequestResponseAmqpLink) await AmqpLinkHelper.CreateAndOpenAmqpLinkAsync((AmqpQueueClient) this.QueueClient, entityPath, new[] { ClaimConstants.Manage, ClaimConstants.Listen }, linkSettings, true);
            return requestResponseAmqpLink;
        }

        void CloseSession(ReceivingAmqpLink link)
        {
            link.Session.SafeClose();
        }

        void CloseRequestResponseSession(RequestResponseAmqpLink requestResponseAmqpLink)
        {
            requestResponseAmqpLink.Session.SafeClose();
        }
    }
}
