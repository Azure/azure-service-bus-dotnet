// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Messaging.Amqp
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;

    sealed class AmqpMessageSender : MessageSender
    {
        int deliveryCount;

        internal AmqpMessageSender(AmqpQueueClient queueClient)
            : base()
        {
            this.QueueClient = queueClient;
            this.Path = this.QueueClient.QueueName;
            this.SendLinkManager = new FaultTolerantAmqpObject<SendingAmqpLink>(this.CreateLinkAsync, this.CloseSession);
        }

        QueueClient QueueClient { get; }

        string Path { get; }

        FaultTolerantAmqpObject<SendingAmqpLink> SendLinkManager { get; }

        public override Task CloseAsync()
        {
            return this.SendLinkManager.CloseAsync();
        }

        protected override async Task OnSendAsync(IEnumerable<BrokeredMessage> brokeredMessages)
        {
            var timeoutHelper = new TimeoutHelper(this.QueueClient.ConnectionSettings.OperationTimeout, true);
            using (AmqpMessage amqpMessage = AmqpMessageConverter.BrokeredMessagesToAmqpMessage(brokeredMessages, true))
            {
                var amqpLink = await this.SendLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime());
                if (amqpLink.Settings.MaxMessageSize.HasValue)
                {
                    ulong size = (ulong)amqpMessage.SerializedMessageSize;
                    if (size > amqpLink.Settings.MaxMessageSize.Value)
                    {
                        // TODO: Add MessageSizeExceededException
                        throw new NotImplementedException("MessageSizeExceededException: " + Resources.AmqpMessageSizeExceeded.FormatForUser(amqpMessage.DeliveryId.Value, size, amqpLink.Settings.MaxMessageSize.Value));
                        //throw Fx.Exception.AsError(new MessageSizeExceededException(
                        //    Resources.AmqpMessageSizeExceeded.FormatForUser(amqpMessage.DeliveryId.Value, size, amqpLink.Settings.MaxMessageSize.Value)));
                    }
                }

                Outcome outcome = await amqpLink.SendMessageAsync(amqpMessage, this.GetNextDeliveryTag(), AmqpConstants.NullBinary, timeoutHelper.RemainingTime());
                if (outcome.DescriptorCode != Accepted.Code)
                {
                    Rejected rejected = (Rejected)outcome;
                    throw Fx.Exception.AsError(AmqpExceptionHelper.ToMessagingContract(rejected.Error));
                }
            }
        }

        ArraySegment<byte> GetNextDeliveryTag()
        {
            int deliveryId = Interlocked.Increment(ref this.deliveryCount);
            return new ArraySegment<byte>(BitConverter.GetBytes(deliveryId));
        }

        async Task<SendingAmqpLink> CreateLinkAsync(TimeSpan timeout)
        {
            var linkSettings = new AmqpLinkSettings
            {
                Role = false,
                InitialDeliveryCount = 0,
                Target = new Target {Address = this.Path},
                Source = new Source {Address = this.ClientId}
            };
            linkSettings.AddProperty(AmqpClientConstants.EntityTypeName, (int)MessagingEntityType.Queue);

            SendingAmqpLink sendingAmqpLink = (SendingAmqpLink) await AmqpLinkHelper.CreateAndOpenAmqpLinkAsync((AmqpQueueClient) this.QueueClient, this.Path, new[] { ClaimConstants.Send }, linkSettings, false);
            return sendingAmqpLink;
        }

        void CloseSession(SendingAmqpLink link)
        {
            // Note we close the session (which includes the link).
            link.Session.SafeClose();
        }
    }
}
