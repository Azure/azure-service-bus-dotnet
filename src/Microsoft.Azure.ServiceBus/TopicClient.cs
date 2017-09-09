// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Azure.Amqp;
    using Core;
    using Primitives;

    /// <summary>
    /// TopicClient can be used for all basic interactions with a Service Bus topic.
    /// </summary>
    /// <example>
    /// Create a new TopicClient
    /// <code>
    /// ITopicClient topicClient = new TopicClient(
    ///     namespaceConnectionString,
    ///     topicName,
    ///     RetryExponential);
    /// </code>
    ///
    /// Send a message to the topic:
    /// <code>
    /// byte[] data = GetData();
    /// await topicClient.SendAsync(data);
    /// </code>
    /// </example>
    /// <remarks>It uses AMQP protocol for communicating with servicebus.</remarks>
    public class TopicClient : ClientEntity, ITopicClient
    {
        readonly bool ownsConnection;
        readonly object syncLock;
        MessageSender innerSender;

        /// <summary>
        /// Instantiates a new <see cref="TopicClient"/> to perform operations on a topic.
        /// </summary>
        /// <param name="connectionStringBuilder"><see cref="ServiceBusConnectionStringBuilder"/> having namespace and topic information.</param>
        /// <param name="retryPolicy">Retry policy for topic operations. Defaults to <see cref="RetryPolicy.Default"/></param>
        /// <remarks>Creates a new connection to the topic, which is opened during the first send operation.</remarks>
        public TopicClient(ServiceBusConnectionStringBuilder connectionStringBuilder, RetryPolicy retryPolicy = null)
            : this(connectionStringBuilder?.GetNamespaceConnectionString(), connectionStringBuilder?.EntityPath, retryPolicy)
        {
        }

        /// <summary>
        /// Instantiates a new <see cref="TopicClient"/> to perform operations on a topic.
        /// </summary>
        /// <param name="connectionString">Namespace connection string. Must not contain topic information.</param>
        /// <param name="entityPath">Path to the topic</param>
        /// <param name="retryPolicy">Retry policy for topic operations. Defaults to <see cref="RetryPolicy.Default"/></param>
        /// <remarks>Creates a new connection to the topic, which is opened during the first send operation.</remarks>
        public TopicClient(string connectionString, string entityPath, RetryPolicy retryPolicy = null)
            : this(new ServiceBusNamespaceConnection(connectionString), entityPath, retryPolicy ?? RetryPolicy.Default)
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

        TopicClient(ServiceBusNamespaceConnection serviceBusConnection, string entityPath, RetryPolicy retryPolicy)
            : base(nameof(TopicClient), entityPath, retryPolicy)
        {
            MessagingEventSource.Log.TopicClientCreateStart(serviceBusConnection?.Endpoint.Authority, entityPath);

            ServiceBusConnection = serviceBusConnection ?? throw new ArgumentNullException(nameof(serviceBusConnection));
            OperationTimeout = ServiceBusConnection.OperationTimeout;
            syncLock = new object();
            TopicName = entityPath;
            TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(
                serviceBusConnection.SasKeyName,
                serviceBusConnection.SasKey);
            CbsTokenProvider = new TokenProviderAdapter(TokenProvider, serviceBusConnection.OperationTimeout);

            MessagingEventSource.Log.TopicClientCreateStop(serviceBusConnection?.Endpoint.Authority, entityPath, ClientId);
        }

        /// <summary>
        /// Gets the name of the topic.
        /// </summary>
        public string TopicName { get; }

        /// <summary>
        /// Duration after which individual operations will timeout.
        /// </summary>
        public override TimeSpan OperationTimeout
        {
            get => ServiceBusConnection.OperationTimeout;
            set => ServiceBusConnection.OperationTimeout = value;
        }

        /// <summary>
        /// Gets the name of the topic.
        /// </summary>
        public string Path => TopicName;

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
                                TopicName,
                                MessagingEntityType.Topic,
                                ServiceBusConnection,
                                CbsTokenProvider,
                                RetryPolicy);
                        }
                    }
                }

                return innerSender;
            }
        }

        internal ServiceBusNamespaceConnection ServiceBusConnection { get; }

        ICbsTokenProvider CbsTokenProvider { get; }

        TokenProvider TokenProvider { get; }

        /// <summary>
        /// Sends a message to Service Bus.
        /// </summary>
        /// <param name="message">The <see cref="Message"/></param>
        /// <returns>An asynchronous operation</returns>
        public Task SendAsync(Message message)
        {
            return SendAsync(new[] { message });
        }

        /// <summary>
        /// Sends a list of messages to Service Bus.
        /// </summary>
        /// <param name="messageList">The list of messages</param>
        /// <returns>An asynchronous operation</returns>
        public Task SendAsync(IList<Message> messageList)
        {
            ThrowIfClosed();
            return InnerSender.SendAsync(messageList);
        }

        /// <summary>
        /// Schedules a message to appear on Service Bus at a later time.
        /// </summary>
        /// <param name="message">The <see cref="Message"/> that needs to be scheduled.</param>
        /// <param name="scheduleEnqueueTimeUtc">The UTC time at which the message should be available for processing.</param>
        /// <returns>The sequence number of the message that was scheduled.</returns>
        public Task<long> ScheduleMessageAsync(Message message, DateTimeOffset scheduleEnqueueTimeUtc)
        {
            ThrowIfClosed();
            return InnerSender.ScheduleMessageAsync(message, scheduleEnqueueTimeUtc);
        }

        /// <summary>
        /// Cancels a message that was scheduled.
        /// </summary>
        /// <param name="sequenceNumber">The <see cref="Message.SystemPropertiesCollection.SequenceNumber"/> of the message to be cancelled.</param>
        /// <returns>An asynchronous operation</returns>
        public Task CancelScheduledMessageAsync(long sequenceNumber)
        {
            ThrowIfClosed();
            return InnerSender.CancelScheduledMessageAsync(sequenceNumber);
        }

        /// <summary>
        /// Gets a list of currently registered plugins for this TopicClient.
        /// </summary>
        public override IList<ServiceBusPlugin> RegisteredPlugins => InnerSender.RegisteredPlugins;

        /// <summary>
        /// Registers a <see cref="ServiceBusPlugin"/> to be used with this topic client.
        /// </summary>
        /// <param name="serviceBusPlugin">The <see cref="ServiceBusPlugin"/> to register.</param>
        public override void RegisterPlugin(ServiceBusPlugin serviceBusPlugin)
        {
            ThrowIfClosed();
            InnerSender.RegisterPlugin(serviceBusPlugin);
        }

        /// <summary>
        /// Unregisters a <see cref="ServiceBusPlugin"/>.
        /// </summary>
        /// <param name="serviceBusPluginName">The name <see cref="ServiceBusPlugin.Name"/> to be unregistered</param>
        public override void UnregisterPlugin(string serviceBusPluginName)
        {
            ThrowIfClosed();
            InnerSender.UnregisterPlugin(serviceBusPluginName);
        }

        protected override async Task OnClosingAsync()
        {
            if (innerSender != null)
            {
                await innerSender.CloseAsync().ConfigureAwait(false);
            }

            if (ownsConnection)
            {
                await ServiceBusConnection.CloseAsync().ConfigureAwait(false);
            }
        }
    }
}