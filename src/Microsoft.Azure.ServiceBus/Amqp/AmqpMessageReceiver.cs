// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Amqp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Azure.Amqp.Encoding;
    using Messaging.Amqp;
    using Messaging.Primitives;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Primitives;

    sealed class AmqpMessageReceiver : MessageReceiver
    {
        public static readonly TimeSpan DefaultBatchFlushInterval = TimeSpan.FromMilliseconds(20);
        readonly string entityName;
        readonly bool isSessionReceiver;
        string sessionId;
        DateTime lockedUntilUtc;
        readonly ConcurrentExpiringSet<Guid> requestResponseLockedMessages;

        public AmqpMessageReceiver(QueueClient queueClient)
            : this(queueClient, null)
        {
            
        }

        public AmqpMessageReceiver(QueueClient queueClient, string sessionId, bool isSessionReceiver = false)
            : base(queueClient.Mode, queueClient.ConnectionSettings.OperationTimeout)
        {
            this.QueueClient = queueClient;
            this.entityName = queueClient.QueueName;
            this.sessionId = sessionId;
            this.isSessionReceiver = isSessionReceiver;
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

        public override string Path
        {
            get { return this.entityName; }
        }

        public DateTime LockedUntilUtc
        {
            get { return this.lockedUntilUtc; }
        }

        QueueClient QueueClient { get; }

        FaultTolerantAmqpObject<ReceivingAmqpLink> ReceiveLinkManager { get; }

        FaultTolerantAmqpObject<RequestResponseAmqpLink> RequestResponseLinkManager { get; }

        internal async Task GetSessionReceiverLinkAsync()
        {
            var timeoutHelper = new TimeoutHelper(this.OperationTimeout, true);
            ReceivingAmqpLink receivingAmqpLink = await this.ReceiveLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
            Source source = (Source) receivingAmqpLink.Settings.Source;
            if (!source.FilterSet.TryGetValue<string>(AmqpClientConstants.SessionFilterName, out this.sessionId))
            {
                receivingAmqpLink.Session.SafeClose();
                throw new ServiceBusException(false, Resources.AmqpFieldSessionId);
            }

            long lockedUntilUtcTicks;
            this.lockedUntilUtc = receivingAmqpLink.Settings.Properties.TryGetValue(AmqpClientConstants.LockedUntilUtc, out lockedUntilUtcTicks) ? new DateTime(lockedUntilUtcTicks, DateTimeKind.Utc) : DateTime.MinValue;
        }

        public override async Task CloseAsync()
        {
            await this.ReceiveLinkManager.CloseAsync();
            await this.RequestResponseLinkManager.CloseAsync();
        }

        protected override async Task<IList<BrokeredMessage>> OnReceiveAsync(int maxMessageCount)
        {
            try
            {
                var timeoutHelper = new TimeoutHelper(this.OperationTimeout, true);
                ReceivingAmqpLink receiveLink = await this.ReceiveLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
                IEnumerable<AmqpMessage> amqpMessages = null;
                bool hasMessages = await Task.Factory.FromAsync(
                    (c, s) => receiveLink.BeginReceiveRemoteMessages(maxMessageCount, AmqpMessageReceiver.DefaultBatchFlushInterval, timeoutHelper.RemainingTime(), c, s),
                    (a) => receiveLink.EndReceiveMessages(a, out amqpMessages),
                    this).ConfigureAwait(false);

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
                AmqpRequestMessage requestMessage = AmqpRequestMessage.CreateRequest(ManagementConstants.Operations.ReceiveBySequenceNumberOperation, this.OperationTimeout, null);
                requestMessage.Map[ManagementConstants.Properties.SequenceNumbers] = sequenceNumbers.ToArray();
                requestMessage.Map[ManagementConstants.Properties.ReceiverSettleMode] = (uint)(this.ReceiveMode == ReceiveMode.ReceiveAndDelete ? 0 : 1);

                AmqpResponseMessage response = await this.ExecuteRequestResponseAsync(requestMessage).ConfigureAwait(false);

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
                    await this.DisposeMessageRequestResponseAsync(lockTokens, DispositionStatus.Completed).ConfigureAwait(false);
                }
                else
                {
                    await this.DisposeMessagesAsync(lockTokens, AmqpConstants.AcceptedOutcome).ConfigureAwait(false); 
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
                    await this.DisposeMessageRequestResponseAsync(lockTokens, DispositionStatus.Abandoned).ConfigureAwait(false);
                }
                else
                {
                    await this.DisposeMessagesAsync(lockTokens, new Modified()).ConfigureAwait(false);
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
                    await this.DisposeMessageRequestResponseAsync(lockTokens, DispositionStatus.Defered).ConfigureAwait(false);
                }
                else
                {
                    await this.DisposeMessagesAsync(lockTokens, new Modified() { UndeliverableHere = true }).ConfigureAwait(false);
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
                    await this.DisposeMessageRequestResponseAsync(lockTokens, DispositionStatus.Suspended).ConfigureAwait(false);
                }
                else
                {
                    await this.DisposeMessagesAsync(lockTokens, AmqpConstants.RejectedOutcome).ConfigureAwait(false);
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
                AmqpRequestMessage requestMessage = AmqpRequestMessage.CreateRequest(ManagementConstants.Operations.RenewLockOperation, this.OperationTimeout, null);
                requestMessage.Map[ManagementConstants.Properties.LockTokens] = new Guid[] {lockToken};

                AmqpResponseMessage response = await this.ExecuteRequestResponseAsync(requestMessage).ConfigureAwait(false) ;

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
            var timeoutHelper = new TimeoutHelper(this.OperationTimeout, true);
            IList<ArraySegment<byte>> deliveryTags = ConvertLockTokensToDeliveryTags(lockTokens);

            ReceivingAmqpLink receiveLink = await this.ReceiveLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
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
                AmqpRequestMessage requestMessage = AmqpRequestMessage.CreateRequest(ManagementConstants.Operations.UpdateDispositionOperation, this.OperationTimeout, null);
                requestMessage.Map[ManagementConstants.Properties.LockTokens] = lockTokens.ToArray();
                requestMessage.Map[ManagementConstants.Properties.DispositionStatus] = dispositionStatus.ToString().ToLowerInvariant();

                await this.ExecuteRequestResponseAsync(requestMessage);
            }
            catch (AmqpException amqpException)
            {
                throw AmqpExceptionHelper.ToMessagingContract(amqpException.Error);
            }
        }

        internal override async Task<AmqpResponseMessage> OnExecuteRequestResponseAsync(AmqpRequestMessage amqpRequestMessage)
        {
            AmqpMessage amqpMessage = amqpRequestMessage.AmqpMessage;
            var timeoutHelper = new TimeoutHelper(this.OperationTimeout, true);
            RequestResponseAmqpLink requestResponseAmqpLink = await this.RequestResponseLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);

            AmqpMessage responseAmqpMessage = await Task.Factory.FromAsync(
                (c, s) => requestResponseAmqpLink.BeginRequest(amqpMessage, timeoutHelper.RemainingTime(), c, s),
                (a) => requestResponseAmqpLink.EndRequest(a),
                this).ConfigureAwait(false);

            AmqpResponseMessage responseMessage = AmqpResponseMessage.CreateResponse(responseAmqpMessage);
            return responseMessage;
        }

        IList<ArraySegment<byte>> ConvertLockTokensToDeliveryTags(IEnumerable<Guid> lockTokens)
        {
            return lockTokens.Select(lockToken => new ArraySegment<Byte>(lockToken.ToByteArray())).ToList();
        }

        async Task<ReceivingAmqpLink> CreateLinkAsync(TimeSpan timeout)
        {
            FilterSet filterMap = null;
            if (this.isSessionReceiver)
            {
                filterMap = new FilterSet { { AmqpClientConstants.SessionFilterName, this.sessionId } };
            }

            var linkSettings = new AmqpLinkSettings
            {
                Role = true,
                TotalLinkCredit = (uint) this.PrefetchCount,
                AutoSendFlow = this.PrefetchCount > 0,
                Source = new Source { Address = this.Path, FilterSet = filterMap },
                Target = new Target { Address = this.ClientId },
                SettleType = (this.QueueClient.Mode == ReceiveMode.PeekLock) ? SettleMode.SettleOnDispose : SettleMode.SettleOnSend
            };

            linkSettings.AddProperty(AmqpClientConstants.EntityTypeName, (int)MessagingEntityType.Queue);

            ReceivingAmqpLink receivingAmqpLink = (ReceivingAmqpLink) await AmqpLinkHelper.CreateAndOpenAmqpLinkAsync((AmqpQueueClient) this.QueueClient, this.Path, new[] {ClaimConstants.Listen}, linkSettings, false).ConfigureAwait(false);
            return receivingAmqpLink;
        }

        //TODO: Consolidate the link creation paths
        async Task<RequestResponseAmqpLink> CreateRequestResponseLinkAsync(TimeSpan timeout)
        {
            string entityPath = this.Path + '/' + AmqpClientConstants.ManagementAddress;
            var linkSettings = new AmqpLinkSettings();
            linkSettings.AddProperty(AmqpClientConstants.EntityTypeName, AmqpClientConstants.EntityTypeManagement);

            RequestResponseAmqpLink requestResponseAmqpLink = (RequestResponseAmqpLink) await AmqpLinkHelper.CreateAndOpenAmqpLinkAsync((AmqpQueueClient) this.QueueClient, entityPath, new[] { ClaimConstants.Manage, ClaimConstants.Listen }, linkSettings, true).ConfigureAwait(false);
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
