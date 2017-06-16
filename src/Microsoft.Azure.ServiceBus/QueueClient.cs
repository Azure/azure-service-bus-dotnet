// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.ServiceBus.Amqp;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus
{
    /// <summary>
    ///     Used for all basic interactions with a Service Bus queue.
    /// </summary>
    public class QueueClient : ClientEntity, IQueueClient
    {
        readonly bool ownsConnection;
        readonly object syncLock;
        MessageReceiver innerReceiver;
        MessageSender innerSender;
        int prefetchCount;
        AmqpSessionClient sessionClient;
        SessionPumpHost sessionPumpHost;

        /// <summary>
        ///     Instantiates a new <see cref="QueueClient" /> to perform operations on a queue.
        /// </summary>
        /// <param name="connectionStringBuilder">
        ///     <see cref="ServiceBusConnectionStringBuilder" /> having namespace and queue
        ///     information.
        /// </param>
        /// <param name="receiveMode">Mode of receive of messages. Defaults to <see cref="ReceiveMode" />.PeekLock.</param>
        /// <param name="retryPolicy">Retry policy for queue operations. Defaults to <see cref="RetryPolicy.Default" /></param>
        public QueueClient(ServiceBusConnectionStringBuilder connectionStringBuilder, ReceiveMode receiveMode = ReceiveMode.PeekLock, RetryPolicy retryPolicy = null)
            : this(connectionStringBuilder.GetNamespaceConnectionString(), connectionStringBuilder.EntityPath, receiveMode, retryPolicy)
        {
        }

        /// <summary>
        ///     Instantiates a new <see cref="QueueClient" /> to perform operations on a queue.
        /// </summary>
        /// <param name="connectionString">
        ///     Namespace connection string.
        ///     <remarks>Should not contain queue information.</remarks>
        /// </param>
        /// <param name="entityPath">Path to the queue</param>
        /// <param name="receiveMode">Mode of receive of messages. Defaults to <see cref="ReceiveMode" />.PeekLock.</param>
        /// <param name="retryPolicy">Retry policy for queue operations. Defaults to <see cref="RetryPolicy.Default" /></param>
        public QueueClient(string connectionString, string entityPath, ReceiveMode receiveMode = ReceiveMode.PeekLock, RetryPolicy retryPolicy = null)
            : this(new ServiceBusNamespaceConnection(connectionString), entityPath, receiveMode, retryPolicy ?? RetryPolicy.Default)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(connectionString);
            }
            if (string.IsNullOrWhiteSpace(entityPath))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(entityPath);
            }

            ownsConnection = true;
        }

        QueueClient(ServiceBusNamespaceConnection serviceBusConnection, string entityPath, ReceiveMode receiveMode, RetryPolicy retryPolicy)
            : base($"{nameof(QueueClient)}{GetNextId()}({entityPath})", retryPolicy)
        {
            syncLock = new object();
            QueueName = entityPath;
            ReceiveMode = receiveMode;
            ServiceBusConnection = serviceBusConnection;
            TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(
                serviceBusConnection.SasKeyName,
                serviceBusConnection.SasKey);
            CbsTokenProvider = new TokenProviderAdapter(TokenProvider, serviceBusConnection.OperationTimeout);
        }

        /// <summary>
        ///     Gets or sets the number of messages that the queue client can simultaneously request.
        /// </summary>
        /// <value>The number of messages that the queue client can simultaneously request.</value>
        public int PrefetchCount
        {
            get => prefetchCount;
            set
            {
                if (value < 0)
                {
                    throw Fx.Exception.ArgumentOutOfRange(nameof(PrefetchCount), value, "Value cannot be less than 0.");
                }
                prefetchCount = value;
                if (innerReceiver != null)
                {
                    innerReceiver.PrefetchCount = value;
                }
            }
        }

        internal MessageSender InnerSender
        {
            get
            {
                if (innerSender == null)
                {
                    lock (syncLock)
                    {
                        if (innerSender == null)
                        {
                            innerSender = new MessageSender(
                                QueueName,
                                MessagingEntityType.Queue,
                                ServiceBusConnection,
                                CbsTokenProvider,
                                RetryPolicy);
                        }
                    }
                }

                return innerSender;
            }
        }

        internal MessageReceiver InnerReceiver
        {
            get
            {
                if (innerReceiver == null)
                {
                    lock (syncLock)
                    {
                        if (innerReceiver == null)
                        {
                            innerReceiver = new MessageReceiver(
                                QueueName,
                                MessagingEntityType.Queue,
                                ReceiveMode,
                                ServiceBusConnection,
                                CbsTokenProvider,
                                RetryPolicy,
                                PrefetchCount);
                        }
                    }
                }

                return innerReceiver;
            }
        }

        internal AmqpSessionClient SessionClient
        {
            get
            {
                if (sessionClient == null)
                {
                    lock (syncLock)
                    {
                        if (sessionClient == null)
                        {
                            sessionClient = new AmqpSessionClient(
                                ClientId,
                                Path,
                                MessagingEntityType.Queue,
                                ReceiveMode,
                                PrefetchCount,
                                ServiceBusConnection,
                                CbsTokenProvider,
                                RetryPolicy);
                        }
                    }
                }

                return sessionClient;
            }
        }

        internal SessionPumpHost SessionPumpHost
        {
            get
            {
                if (sessionPumpHost == null)
                {
                    lock (syncLock)
                    {
                        if (sessionPumpHost == null)
                        {
                            sessionPumpHost = new SessionPumpHost(
                                ClientId,
                                ReceiveMode,
                                SessionClient);
                        }
                    }
                }

                return sessionPumpHost;
            }
        }

        internal ServiceBusConnection ServiceBusConnection { get; }

        ICbsTokenProvider CbsTokenProvider { get; }

        TokenProvider TokenProvider { get; }

        /// <summary>
        ///     Gets the name of the queue.
        /// </summary>
        public string QueueName { get; }

        /// <summary>
        ///     Gets the <see cref="ReceiveMode.ReceiveMode" /> for the QueueClient.
        /// </summary>
        public ReceiveMode ReceiveMode { get; }

        /// <summary>
        ///     Gets the name of the queue.
        /// </summary>
        public string Path => QueueName;

        /// <summary>
        ///     Sends a message to Service Bus.
        /// </summary>
        /// <param name="message">The <see cref="Message" /></param>
        /// <returns>An asynchronous operation</returns>
        public Task SendAsync(Message message)
        {
            return SendAsync(new[] {message});
        }

        /// <summary>
        ///     Sends a list of messages to Service Bus.
        /// </summary>
        /// <param name="messageList">The list of messages</param>
        /// <returns>An asynchronous operation</returns>
        public Task SendAsync(IList<Message> messageList)
        {
            return InnerSender.SendAsync(messageList);
        }

        /// <summary>
        ///     Completes a <see cref="Message" /> using a lock token.
        /// </summary>
        /// <param name="lockToken">The lock token of the corresponding message to complete.</param>
        /// <remarks>
        ///     A lock token can be found in <see cref="Message.SystemPropertiesCollection.LockToken" />, only when
        ///     <see cref="ReceiveMode" /> is set to <see cref="ReceiveMode.PeekLock" />.
        /// </remarks>
        /// <returns>The asynchronous operation.</returns>
        public Task CompleteAsync(string lockToken)
        {
            return InnerReceiver.CompleteAsync(lockToken);
        }

        /// <summary>
        ///     Abandons a <see cref="Message" /> using a lock token. This will make the message available again for processing.
        /// </summary>
        /// <param name="lockToken">The lock token of the corresponding message to abandon.</param>
        /// <remarks>
        ///     A lock token can be found in <see cref="Message.SystemPropertiesCollection.LockToken" />, only when
        ///     <see cref="ReceiveMode" /> is set to <see cref="ReceiveMode.PeekLock" />.
        /// </remarks>
        /// <returns>The asynchronous operation.</returns>
        public Task AbandonAsync(string lockToken)
        {
            return InnerReceiver.AbandonAsync(lockToken);
        }

        /// <summary>
        ///     Moves a message to the deadletter queue.
        /// </summary>
        /// <param name="lockToken">The lock token of the corresponding message to deadletter.</param>
        /// <remarks>
        ///     A lock token can be found in <see cref="Message.SystemPropertiesCollection.LockToken" />, only when
        ///     <see cref="ReceiveMode" /> is set to <see cref="ReceiveMode.PeekLock" />.
        ///     In order to receive a message from the deadletter queue, you will need a new <see cref="IMessageReceiver" />, with
        ///     the corresponding path. You can use <see cref="EntityNameHelper.FormatDeadLetterPath(string)" /> to help with this.
        /// </remarks>
        /// <returns>The asynchronous operation.</returns>
        public Task DeadLetterAsync(string lockToken)
        {
            return InnerReceiver.DeadLetterAsync(lockToken);
        }

        /// <summary>Asynchronously processes a message.</summary>
        /// <param name="handler"></param>
        public void RegisterMessageHandler(Func<Message, CancellationToken, Task> handler)
        {
            InnerReceiver.RegisterMessageHandler(handler);
        }

        /// <summary>Asynchronously processes a message.</summary>
        /// <param name="handler"></param>
        /// <param name="messageHandlerOptions">Options associated with message pump processing.</param>
        public void RegisterMessageHandler(Func<Message, CancellationToken, Task> handler, MessageHandlerOptions messageHandlerOptions)
        {
            InnerReceiver.RegisterMessageHandler(handler, messageHandlerOptions);
        }

        /// <summary>
        ///     Sends a scheduled message
        /// </summary>
        /// <param name="message">Message to be scheduled</param>
        /// <param name="scheduleEnqueueTimeUtc">Time of enqueue</param>
        /// <returns>Sequence number that is needed for cancelling.</returns>
        public Task<long> ScheduleMessageAsync(Message message, DateTimeOffset scheduleEnqueueTimeUtc)
        {
            return InnerSender.ScheduleMessageAsync(message, scheduleEnqueueTimeUtc);
        }

        /// <summary>
        ///     Cancels a scheduled message
        /// </summary>
        /// <param name="sequenceNumber">Returned on scheduling a message.</param>
        /// <returns></returns>
        public Task CancelScheduledMessageAsync(long sequenceNumber)
        {
            return InnerSender.CancelScheduledMessageAsync(sequenceNumber);
        }

        /// <summary></summary>
        /// <returns></returns>
        protected override async Task OnClosingAsync()
        {
            if (innerSender != null)
            {
                await innerSender.CloseAsync().ConfigureAwait(false);
            }

            if (innerReceiver != null)
            {
                await innerReceiver.CloseAsync().ConfigureAwait(false);
            }

            sessionPumpHost?.Close();

            if (ownsConnection)
            {
                await ServiceBusConnection.CloseAsync().ConfigureAwait(false);
            }
        }

        /// <summary>Register a session handler.</summary>
        /// <param name="handler"></param>
        public void RegisterSessionHandler(Func<IMessageSession, Message, CancellationToken, Task> handler)
        {
            var sessionHandlerOptions = new SessionHandlerOptions();
            RegisterSessionHandler(handler, sessionHandlerOptions);
        }

        /// <summary>Register a session handler.</summary>
        /// <param name="handler"></param>
        /// <param name="sessionHandlerOptions">Options associated with session pump processing.</param>
        public void RegisterSessionHandler(Func<IMessageSession, Message, CancellationToken, Task> handler, SessionHandlerOptions sessionHandlerOptions)
        {
            SessionPumpHost.OnSessionHandlerAsync(handler, sessionHandlerOptions).GetAwaiter().GetResult();
        }

        /// <summary>
        ///     Registers a <see cref="ServiceBusPlugin" /> to be used for sending and receiving messages from Service Bus.
        /// </summary>
        /// <param name="serviceBusPlugin">The <see cref="ServiceBusPlugin" /> to register</param>
        public void RegisterPlugin(ServiceBusPlugin serviceBusPlugin)
        {
            InnerSender.RegisterPlugin(serviceBusPlugin);
            InnerReceiver.RegisterPlugin(serviceBusPlugin);
        }

        /// <summary>
        ///     Unregisters a <see cref="ServiceBusPlugin" />.
        /// </summary>
        /// <param name="serviceBusPluginName">The name <see cref="ServiceBusPlugin.Name" /> to be unregistered</param>
        public void UnregisterPlugin(string serviceBusPluginName)
        {
            InnerSender.UnregisterPlugin(serviceBusPluginName);
            InnerReceiver.UnregisterPlugin(serviceBusPluginName);
        }
    }
}