// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.ServiceBus.Core;

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Anchor class - all Queue client operations start here.
    /// </summary>
    public abstract class QueueClient : ClientEntity, IQueueClient
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
            return this.SendAsync(new[] { brokeredMessage });
        }

        public Task SendAsync(IList<BrokeredMessage> brokeredMessages)
        {
            return this.InnerSender.SendAsync(brokeredMessages);
        }

        public Task CompleteAsync(Guid lockToken)
        {
            return this.InnerReceiver.CompleteAsync(lockToken);
        }

        public Task AbandonAsync(Guid lockToken)
        {
            return this.InnerReceiver.AbandonAsync(lockToken);
        }

        public Task DeferAsync(Guid lockToken)
        {
            return this.InnerReceiver.DeferAsync(lockToken);
        }

        public Task DeadLetterAsync(Guid lockToken)
        {
            return this.InnerReceiver.DeadLetterAsync(lockToken);
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

        protected abstract Task<MessageSession> OnAcceptMessageSessionAsync(string sessionId, TimeSpan serverWaitTime);

        protected abstract Task OnCloseAsync();
    }
}