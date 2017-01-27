﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus.Primitives;

    /// <summary>
    /// Anchor class - all Queue client operations start here.
    /// See <see cref="QueueClient.CreateFromConnectionString(string)"/>
    /// </summary>
    public abstract class QueueClient : ClientEntity, IMessageReceiver, IMessageSender, IMessageSessionEntity
    {
        MessageSender innerSender;
        MessageReceiver innerReceiver;

        protected QueueClient(ServiceBusConnection serviceBusConnection, string entityPath, ReceiveMode receiveMode)
            : base($"{nameof(QueueClient)}{ClientEntity.GetNextId()}({entityPath})")
        {
            this.ServiceBusConnection = serviceBusConnection;
            this.QueueName = entityPath;
            this.ReceiveMode = receiveMode;
        }

        public string QueueName { get; }

        public ReceiveMode ReceiveMode { get; private set; }

        public string Path => this.QueueName;

        public int PrefetchCount
        {
            get
            {
                return this.InnerReceiver.PrefetchCount;
            }

            set
            {
                this.InnerReceiver.PrefetchCount = value;
            }
        }

        public long LastPeekedSequenceNumber => this.InnerReceiver.LastPeekedSequenceNumber;

        internal MessageSender InnerSender
        {
            get
            {
                if (this.innerSender == null)
                {
                    lock (this.ThisLock)
                    {
                        if (this.innerSender == null)
                        {
                            this.innerSender = this.CreateMessageSender();
                        }
                    }
                }

                return this.innerSender;
            }
        }

        internal MessageReceiver InnerReceiver
        {
            get
            {
                if (this.innerReceiver == null)
                {
                    lock (this.ThisLock)
                    {
                        if (this.innerReceiver == null)
                        {
                            this.innerReceiver = this.CreateMessageReceiver();
                        }
                    }
                }

                return this.innerReceiver;
            }
        }

        protected object ThisLock { get; } = new object();

        protected ServiceBusConnection ServiceBusConnection { get; }

        public static QueueClient CreateFromConnectionString(string entityConnectionString)
        {
            return CreateFromConnectionString(entityConnectionString, ReceiveMode.PeekLock);
        }

        public static QueueClient CreateFromConnectionString(string entityConnectionString, ReceiveMode mode)
        {
            if (string.IsNullOrWhiteSpace(entityConnectionString))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(entityConnectionString));
            }

            ServiceBusEntityConnection entityConnection = new ServiceBusEntityConnection(entityConnectionString);
            return entityConnection.CreateQueueClient(entityConnection.EntityPath, mode);
        }

        public static QueueClient Create(ServiceBusNamespaceConnection namespaceConnection, string entityPath)
        {
            return QueueClient.Create(namespaceConnection, entityPath, ReceiveMode.PeekLock);
        }

        public static QueueClient Create(ServiceBusNamespaceConnection namespaceConnection, string entityPath, ReceiveMode mode)
        {
            if (namespaceConnection == null)
            {
                throw Fx.Exception.Argument(nameof(namespaceConnection), "Namespace Connection is null. Create a connection using the NamespaceConnection class");
            }

            if (string.IsNullOrWhiteSpace(entityPath))
            {
                throw Fx.Exception.Argument(nameof(namespaceConnection), "Entity Path is null");
            }

            return namespaceConnection.CreateQueueClient(entityPath, mode);
        }

        public static QueueClient Create(ServiceBusEntityConnection entityConnection)
        {
            return QueueClient.Create(entityConnection, ReceiveMode.PeekLock);
        }

        public static QueueClient Create(ServiceBusEntityConnection entityConnection, ReceiveMode mode)
        {
            if (entityConnection == null)
            {
                throw Fx.Exception.Argument(nameof(entityConnection), "Namespace Connection is null. Create a connection using the NamespaceConnection class");
            }

            return entityConnection.CreateQueueClient(entityConnection.EntityPath, mode);
        }

        public sealed override async Task CloseAsync()
        {
            await this.InnerReceiver.CloseAsync().ConfigureAwait(false);
            await this.OnCloseAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Send <see cref="BrokeredMessage"/> to Queue.
        /// <see cref="SendAsync(BrokeredMessage)"/> sends the <see cref="BrokeredMessage"/> to a Service Gateway, which in-turn will forward the BrokeredMessage to the queue.
        /// </summary>
        /// <param name="brokeredMessage">the <see cref="BrokeredMessage"/> to be sent.</param>
        /// <returns>A Task that completes when the send operations is done.</returns>
        public Task SendAsync(BrokeredMessage brokeredMessage)
        {
            return this.SendAsync(new BrokeredMessage[] { brokeredMessage });
        }

        public Task SendAsync(IEnumerable<BrokeredMessage> brokeredMessages)
        {
            return this.InnerSender.SendAsync(brokeredMessages);
        }

        public async Task<BrokeredMessage> ReceiveAsync()
        {
            IList<BrokeredMessage> messages = await this.ReceiveAsync(1).ConfigureAwait(false);
            if (messages != null && messages.Count > 0)
            {
                return messages[0];
            }

            return null;
        }

        public Task<IList<BrokeredMessage>> ReceiveAsync(int maxMessageCount)
        {
            return this.InnerReceiver.ReceiveAsync(maxMessageCount);
        }

        public async Task<BrokeredMessage> ReceiveBySequenceNumberAsync(long sequenceNumber)
        {
            IList<BrokeredMessage> messages = await this.ReceiveBySequenceNumberAsync(new long[] { sequenceNumber });
            if (messages != null && messages.Count > 0)
            {
                return messages[0];
            }

            return null;
        }

        public Task<IList<BrokeredMessage>> ReceiveBySequenceNumberAsync(IEnumerable<long> sequenceNumbers)
        {
            return this.InnerReceiver.ReceiveBySequenceNumberAsync(sequenceNumbers);
        }

        /// <summary>
        /// Asynchronously reads the next message without changing the state of the receiver or the message source.
        /// </summary>
        /// <returns>The asynchronous operation that returns the <see cref="Microsoft.Azure.ServiceBus.BrokeredMessage" /> that represents the next message to be read.</returns>
        public Task<BrokeredMessage> PeekAsync()
        {
            return this.innerReceiver.PeekAsync();
        }

        /// <summary>
        /// Asynchronously reads the next batch of message without changing the state of the receiver or the message source.
        /// </summary>
        /// <param name="maxMessageCount">The number of messages.</param>
        /// <returns>The asynchronous operation that returns a list of <see cref="Microsoft.Azure.ServiceBus.BrokeredMessage" /> to be read.</returns>
        public Task<IList<BrokeredMessage>> PeekAsync(int maxMessageCount)
        {
            return this.innerReceiver.PeekAsync(maxMessageCount);
        }

        /// <summary>
        /// Asynchronously reads the next message without changing the state of the receiver or the message source.
        /// </summary>
        /// <param name="fromSequenceNumber">The sequence number from where to read the message.</param>
        /// <returns>The asynchronous operation that returns the <see cref="Microsoft.Azure.ServiceBus.BrokeredMessage" /> that represents the next message to be read.</returns>
        public Task<BrokeredMessage> PeekBySequenceNumberAsync(long fromSequenceNumber)
        {
            return this.innerReceiver.PeekBySequenceNumberAsync(fromSequenceNumber);
        }

        /// <summary>Peeks a batch of messages.</summary>
        /// <param name="fromSequenceNumber">The starting point from which to browse a batch of messages.</param>
        /// <param name="messageCount">The number of messages.</param>
        /// <returns>A batch of messages peeked.</returns>
        public Task<IList<BrokeredMessage>> PeekBySequenceNumberAsync(long fromSequenceNumber, int messageCount)
        {
            return this.innerReceiver.PeekBySequenceNumberAsync(fromSequenceNumber, messageCount);
        }

        public Task CompleteAsync(Guid lockToken)
        {
            return this.CompleteAsync(new Guid[] { lockToken });
        }

        public Task CompleteAsync(IEnumerable<Guid> lockTokens)
        {
            return this.InnerReceiver.CompleteAsync(lockTokens);
        }

        public Task AbandonAsync(Guid lockToken)
        {
            return this.InnerReceiver.AbandonAsync(lockToken);
        }

        public Task<MessageSession> AcceptMessageSessionAsync()
        {
            return this.AcceptMessageSessionAsync(null);
        }

        public async Task<MessageSession> AcceptMessageSessionAsync(string sessionId)
        {
            MessageSession session = null;

            MessagingEventSource.Log.AcceptMessageSessionStart(this.ClientId, sessionId);

            try
            {
                session = await this.OnAcceptMessageSessionAsync(sessionId).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.AcceptMessageSessionException(this.ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.AcceptMessageSessionStop(this.ClientId);
            return session;
        }

        public Task DeferAsync(Guid lockToken)
        {
            return this.InnerReceiver.DeferAsync(lockToken);
        }

        public Task DeadLetterAsync(Guid lockToken)
        {
            return this.InnerReceiver.DeadLetterAsync(lockToken);
        }

        public Task<DateTime> RenewLockAsync(Guid lockToken)
        {
            return this.InnerReceiver.RenewLockAsync(lockToken);
        }

        /// <summary>
        /// Sends a scheduled message
        /// </summary>
        /// <param name="message">Message to be scheduled</param>
        /// <param name="scheduleEnqueueTimeUtc">Time of enqueue</param>
        /// <returns>Sequence number that is needed for cancelling.</returns>
        public Task<long> ScheduleMessageAsync(BrokeredMessage message, DateTimeOffset scheduleEnqueueTimeUtc)
        {
            try
            {
                return this.innerSender.ScheduleMessageAsync(message, scheduleEnqueueTimeUtc);
            }
            catch (Exception)
            {
                // TODO: Log Complete Exception
                throw;
            }
        }

        /// <summary>
        /// Cancels a scheduled message
        /// </summary>
        /// <param name="sequenceNumber">Returned on scheduling a message.</param>
        /// <returns></returns>
        public Task CancelScheduledMessageAsync(long sequenceNumber)
        {
            try
            {
                return this.innerSender.CancelScheduledMessageAsync(sequenceNumber);
            }
            catch (Exception)
            {
                // TODO: Log Complete Exception
                throw;
            }
        }

        protected MessageSender CreateMessageSender()
        {
            return this.OnCreateMessageSender();
        }

        protected MessageReceiver CreateMessageReceiver()
        {
            return this.OnCreateMessageReceiver();
        }

        protected abstract MessageSender OnCreateMessageSender();

        protected abstract MessageReceiver OnCreateMessageReceiver();

        protected abstract Task<MessageSession> OnAcceptMessageSessionAsync(string sessionId);

        protected abstract Task OnCloseAsync();
    }
}