// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Amqp;
    using Core;
    using Primitives;

    public class TopicClient : ClientEntity, ITopicClient
    {
        public TopicClient(string connectionString, string entityPath)
            : this(new ServiceBusNamespaceConnection(connectionString), entityPath)
        {
        }

        protected TopicClient(ServiceBusNamespaceConnection serviceBusConnection, string entityPath)
            : base($"{nameof(QueueClient)}{GetNextId()}({entityPath})")
        {
            this.ServiceBusConnection = serviceBusConnection;
            this.TopicName = entityPath;
            this.InnerClient = new AmqpClient(serviceBusConnection, entityPath, MessagingEntityType.Topic);
        }

        public string TopicName { get; }

        internal IInnerClient InnerClient { get; }

        protected ServiceBusConnection ServiceBusConnection { get; }

        public sealed override async Task CloseAsync()
        {
            await this.InnerClient.CloseAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Send <see cref="BrokeredMessage"/> to Queue.
        /// <see cref="SendAsync(BrokeredMessage)"/> sends the <see cref="BrokeredMessage"/> to a Service Gateway, which in-turn will forward the BrokeredMessage to the queue.
        /// </summary>
        /// <param name="brokeredMessage">the <see cref="BrokeredMessage"/> to be sent.</param>
        /// <returns>A Task that completes when the send operations is done.</returns>
        public Task SendAsync(BrokeredMessage brokeredMessage)
        {
            return this.SendAsync(new[] { brokeredMessage });
        }

        public Task SendAsync(IList<BrokeredMessage> brokeredMessages)
        {
            return this.InnerClient.InnerSender.SendAsync(brokeredMessages);
        }

        /// <summary>
        /// Sends a scheduled message
        /// </summary>
        /// <param name="message">Message to be scheduled</param>
        /// <param name="scheduleEnqueueTimeUtc">Time of enqueue</param>
        /// <returns>Sequence number that is needed for cancelling.</returns>
        public Task<long> ScheduleMessageAsync(BrokeredMessage message, DateTimeOffset scheduleEnqueueTimeUtc)
        {
            return this.InnerClient.InnerSender.ScheduleMessageAsync(message, scheduleEnqueueTimeUtc);
        }

        /// <summary>
        /// Cancels a scheduled message
        /// </summary>
        /// <param name="sequenceNumber">Returned on scheduling a message.</param>
        /// <returns></returns>
        public Task CancelScheduledMessageAsync(long sequenceNumber)
        {
            return this.InnerClient.InnerSender.CancelScheduledMessageAsync(sequenceNumber);
        }

        protected MessageSender CreateMessageSender()
        {
            return this.InnerClient.CreateMessageSender();
        }
    }
}