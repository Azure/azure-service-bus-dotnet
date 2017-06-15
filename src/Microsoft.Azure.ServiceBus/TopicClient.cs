// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus
{
    /// <summary>
    ///     Used for all basic interactions with a Service Bus topic.
    /// </summary>
    public class TopicClient : ClientEntity, ITopicClient
    {
        readonly bool ownsConnection;
        readonly object syncLock;
        MessageSender innerSender;

        /// <summary>
        ///     Instantiates a new <see cref="TopicClient" /> to perform operations on a topic.
        /// </summary>
        /// <param name="connectionStringBuilder">
        ///     <see cref="ServiceBusConnectionStringBuilder" /> having namespace and topic
        ///     information.
        /// </param>
        /// <param name="retryPolicy">Retry policy for topic operations. Defaults to <see cref="RetryPolicy.Default" /></param>
        public TopicClient(ServiceBusConnectionStringBuilder connectionStringBuilder, RetryPolicy retryPolicy = null)
            : this(connectionStringBuilder.GetNamespaceConnectionString(), connectionStringBuilder.EntityPath, retryPolicy)
        {
        }

        /// <summary>
        ///     Instantiates a new <see cref="TopicClient" /> to perform operations on a topic.
        /// </summary>
        /// <param name="connectionString">Namespace connection string.
        ///     <remarks>Should not contain topic information.</remarks>
        /// </param>
        /// <param name="entityPath">Path to the topic</param>
        /// <param name="retryPolicy">Retry policy for topic operations. Defaults to <see cref="RetryPolicy.Default" /></param>
        public TopicClient(string connectionString, string entityPath, RetryPolicy retryPolicy = null)
            : this(new ServiceBusNamespaceConnection(connectionString), entityPath, retryPolicy ?? RetryPolicy.Default)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw Fx.Exception.ArgumentNullOrWhiteSpace(connectionString);
            if (string.IsNullOrWhiteSpace(entityPath))
                throw Fx.Exception.ArgumentNullOrWhiteSpace(entityPath);

            ownsConnection = true;
        }

        TopicClient(ServiceBusNamespaceConnection serviceBusConnection, string entityPath, RetryPolicy retryPolicy)
            : base($"{nameof(TopicClient)}{GetNextId()}({entityPath})", retryPolicy)
        {
            syncLock = new object();
            TopicName = entityPath;
            ServiceBusConnection = serviceBusConnection;
            TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(
                serviceBusConnection.SasKeyName,
                serviceBusConnection.SasKey);
            CbsTokenProvider = new TokenProviderAdapter(TokenProvider, serviceBusConnection.OperationTimeout);
        }

        /// <summary>
        ///     Gets the name of the topic.
        /// </summary>
        public string Path => TopicName;

        internal MessageSender InnerSender
        {
            get
            {
                if (innerSender == null)
                    lock (syncLock)
                    {
                        if (innerSender == null)
                            innerSender = new MessageSender(
                                TopicName,
                                MessagingEntityType.Topic,
                                ServiceBusConnection,
                                CbsTokenProvider,
                                RetryPolicy);
                    }

                return innerSender;
            }
        }

        internal ServiceBusNamespaceConnection ServiceBusConnection { get; }

        ICbsTokenProvider CbsTokenProvider { get; }

        TokenProvider TokenProvider { get; }

        /// <summary>
        ///     Gets the name of the topic.
        /// </summary>
        public string TopicName { get; }

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
                await innerSender.CloseAsync().ConfigureAwait(false);

            if (ownsConnection)
                await ServiceBusConnection.CloseAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///     Registers a <see cref="ServiceBusPlugin" /> to be used for sending messages to Service Bus.
        /// </summary>
        /// <param name="serviceBusPlugin">The <see cref="ServiceBusPlugin" /> to register</param>
        public void RegisterPlugin(ServiceBusPlugin serviceBusPlugin)
        {
            InnerSender.RegisterPlugin(serviceBusPlugin);
        }

        /// <summary>
        ///     Unregisters a <see cref="ServiceBusPlugin" />.
        /// </summary>
        /// <param name="serviceBusPluginName">The name <see cref="ServiceBusPlugin.Name" /> to be unregistered</param>
        public void UnregisterPlugin(string serviceBusPluginName)
        {
            InnerSender.UnregisterPlugin(serviceBusPluginName);
        }
    }
}