﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public abstract class TopicClient : ClientEntity, ITopicClient
    {
        MessageSender innerSender;

        protected TopicClient(ServiceBusConnection serviceBusConnection, string entityPath)
            : base($"{nameof(TopicClient)}{ClientEntity.GetNextId()}({entityPath})")
        {
            this.ServiceBusConnection = serviceBusConnection;
            this.TopicName = entityPath;
        }

        public string TopicName { get; }

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

        protected ServiceBusConnection ServiceBusConnection { get; }

        protected object ThisLock { get; } = new object();

        public sealed override async Task CloseAsync()
        {
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

        public Task SendAsync(IEnumerable<BrokeredMessage> brokeredMessages)
        {
            return this.InnerSender.SendAsync(brokeredMessages);
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

        protected abstract MessageSender OnCreateMessageSender();

        protected abstract Task OnCloseAsync();
    }
}