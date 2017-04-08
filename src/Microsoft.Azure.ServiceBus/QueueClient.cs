﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Amqp;
    using Core;
    using Microsoft.Azure.Amqp;
    using Primitives;

    /// <summary>
    /// Anchor class - all Queue client operations start here.
    /// </summary>
    public sealed class QueueClient : ClientEntity, IQueueClient
    {
        readonly object syncLock;
        MessageSender innerSender;
        MessageReceiver innerReceiver;
        SessionPumpHost pumpHost;

        public QueueClient(string connectionString, string entityPath, ReceiveMode receiveMode = ReceiveMode.PeekLock, RetryPolicy retryPolicy = null)
            : this(new ServiceBusNamespaceConnection(connectionString), entityPath, receiveMode, retryPolicy ?? RetryPolicy.Default)
        {
        }

        QueueClient(ServiceBusNamespaceConnection serviceBusConnection, string entityPath, ReceiveMode receiveMode, RetryPolicy retryPolicy)
            : base($"{nameof(QueueClient)}{ClientEntity.GetNextId()}({entityPath})", retryPolicy)
        {
            this.syncLock = new object();
            this.QueueName = entityPath;
            this.ReceiveMode = receiveMode;
            this.ServiceBusConnection = serviceBusConnection;
            this.TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(
                serviceBusConnection.SasKeyName,
                serviceBusConnection.SasKey);
            this.CbsTokenProvider = new TokenProviderAdapter(this.TokenProvider, serviceBusConnection.OperationTimeout);
        }

        public string QueueName { get; }

        public ReceiveMode ReceiveMode { get; }

        public string Path => this.QueueName;

        internal MessageSender InnerSender
        {
            get
            {
                if (this.innerSender == null)
                {
                    lock (this.syncLock)
                    {
                        if (this.innerSender == null)
                        {
                            this.innerSender = new AmqpMessageSender(
                                this.QueueName,
                                MessagingEntityType.Queue,
                                this.ServiceBusConnection,
                                this.CbsTokenProvider,
                                this.RetryPolicy);
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
                    lock (this.syncLock)
                    {
                        if (this.innerReceiver == null)
                        {
                            this.innerReceiver = new AmqpMessageReceiver(
                                this.QueueName,
                                MessagingEntityType.Queue,
                                this.ReceiveMode,
                                this.ServiceBusConnection.PrefetchCount,
                                this.ServiceBusConnection,
                                this.CbsTokenProvider,
                                this.RetryPolicy);
                        }
                    }
                }

                return this.innerReceiver;
            }
        }

        ServiceBusConnection ServiceBusConnection { get; set; }

        ICbsTokenProvider CbsTokenProvider { get; }

        TokenProvider TokenProvider { get; }

        public override async Task OnClosingAsync()
        {
            if (this.innerSender != null)
            {
                await this.innerSender.CloseAsync().ConfigureAwait(false);
            }

            if (this.innerReceiver != null)
            {
                await this.innerReceiver.CloseAsync().ConfigureAwait(false);
            }

            this.pumpHost?.OnClosingAsync();
        }

        /// <summary>
        /// Send <see cref="Message"/> to Queue.
        /// <see cref="SendAsync(Message)"/> sends the <see cref="Message"/> to a Service Gateway, which in-turn will forward the Message to the queue.
        /// </summary>
        /// <param name="message">the <see cref="Message"/> to be sent.</param>
        /// <returns>A Task that completes when the send operations is done.</returns>
        public Task SendAsync(Message message)
        {
            return this.SendAsync(new[] { message });
        }

        public Task SendAsync(IList<Message> messageList)
        {
            return this.InnerSender.SendAsync(messageList);
        }

        public Task CompleteAsync(string lockToken)
        {
            return this.InnerReceiver.CompleteAsync(lockToken);
        }

        public Task AbandonAsync(string lockToken)
        {
            return this.InnerReceiver.AbandonAsync(lockToken);
        }

        public Task DeadLetterAsync(string lockToken)
        {
            return this.InnerReceiver.DeadLetterAsync(lockToken);
        }

        /// <summary>Asynchronously processes a message.</summary>
        /// <param name="handler"></param>
        public void RegisterMessageHandler(Func<Message, CancellationToken, Task> handler)
        {
            this.InnerReceiver.RegisterMessageHandler(handler);
        }

        /// <summary>Asynchronously processes a message.</summary>
        /// <param name="handler"></param>
        /// <param name="registerMessageHandlerOptions">Options associated with message pump processing.</param>
        public void RegisterMessageHandler(Func<Message, CancellationToken, Task> handler, RegisterMessageHandlerOptions registerMessageHandlerOptions)
        {
            this.InnerReceiver.RegisterMessageHandler(handler, registerMessageHandlerOptions);
        }

        /// <summary>Register a session handler.</summary>
        /// <param name="handler"></param>
        public void RegisterSessionHandler(Func<IMessageSession, Message, CancellationToken, Task> handler)
        {
            var sessionHandlerOptions = new RegisterSessionHandlerOptions();
            this.RegisterSessionHandler(handler, sessionHandlerOptions);
        }

        /// <summary>Register a session handler.</summary>
        /// <param name="handler"></param>
        /// <param name="registerSessionHandlerOptions">Options associated with session pump processing.</param>
        public void RegisterSessionHandler(Func<IMessageSession, Message, CancellationToken, Task> handler, RegisterSessionHandlerOptions registerSessionHandlerOptions)
        {
            lock (this.syncLock)
            {
                if (this.pumpHost != null)
                {
                    throw new InvalidOperationException(Resources.SessionHandlerAlreadyRegistered);
                }

                AmqpSessionClient sessionClient = new AmqpSessionClient(
                    this.Path,
                    MessagingEntityType.Queue,
                    this.ReceiveMode,
                    this.ServiceBusConnection.PrefetchCount,
                    this.ServiceBusConnection,
                    this.CbsTokenProvider,
                    this.RetryPolicy);

                this.pumpHost = new SessionPumpHost(this.ClientId, this.ReceiveMode, sessionClient);
            }

            try
            {
                this.pumpHost.OnSessionHandlerAsync(handler, registerSessionHandlerOptions).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                this.pumpHost = null;
                throw;
            }
        }

        /// <summary>
        /// Sends a scheduled message
        /// </summary>
        /// <param name="message">Message to be scheduled</param>
        /// <param name="scheduleEnqueueTimeUtc">Time of enqueue</param>
        /// <returns>Sequence number that is needed for cancelling.</returns>
        public Task<long> ScheduleMessageAsync(Message message, DateTimeOffset scheduleEnqueueTimeUtc)
        {
            return this.InnerSender.ScheduleMessageAsync(message, scheduleEnqueueTimeUtc);
        }

        /// <summary>
        /// Cancels a scheduled message
        /// </summary>
        /// <param name="sequenceNumber">Returned on scheduling a message.</param>
        /// <returns></returns>
        public Task CancelScheduledMessageAsync(long sequenceNumber)
        {
            return this.InnerSender.CancelScheduledMessageAsync(sequenceNumber);
        }
    }
}