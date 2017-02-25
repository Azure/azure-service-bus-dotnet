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

    /// <summary>
    /// Anchor class - all Queue client operations start here.
    /// </summary>
    public class QueueClient : ClientEntity, IQueueClient
    {
        public QueueClient(string connectionString, string entityPath, ReceiveMode receiveMode = ReceiveMode.PeekLock)
            : this(new ServiceBusNamespaceConnection(connectionString), entityPath, receiveMode)
        {
        }

        protected QueueClient(ServiceBusNamespaceConnection serviceBusConnection, string entityPath, ReceiveMode receiveMode)
            : base($"{nameof(QueueClient)}{ClientEntity.GetNextId()}({entityPath})")
        {
            this.ServiceBusConnection = serviceBusConnection;
            this.QueueName = entityPath;
            this.ReceiveMode = receiveMode;
            this.InnerClient = new AmqpClient(serviceBusConnection, entityPath, MessagingEntityType.Queue, receiveMode);
        }

        public string QueueName { get; }

        public ReceiveMode ReceiveMode { get; private set; }

        public string Path => this.QueueName;

        internal IInnerClient InnerClient { get; }

        // TODO nemakam: Remove this, and ensure someone is accountable for its closure. --> Across all clients
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

        public Task CompleteAsync(Guid lockToken)
        {
            return this.InnerClient.InnerReceiver.CompleteAsync(lockToken);
        }

        public Task AbandonAsync(Guid lockToken)
        {
            return this.InnerClient.InnerReceiver.AbandonAsync(lockToken);
        }

        public Task DeadLetterAsync(Guid lockToken)
        {
            return this.InnerClient.InnerReceiver.DeadLetterAsync(lockToken);
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

        protected MessageReceiver CreateMessageReceiver()
        {
            return this.InnerClient.CreateMessageReceiver();
        }
    }
}