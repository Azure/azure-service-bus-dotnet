// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.ServiceBus.Amqp;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Filters;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus
{
    /// <summary>
    ///     Used for all basic interactions with a Service Bus subscription.
    /// </summary>
    public class SubscriptionClient : ClientEntity, ISubscriptionClient
    {
        /// <summary>
        ///     Gets the name of the default rule on the subscription.
        /// </summary>
        public const string DefaultRule = "$Default";

        readonly bool ownsConnection;
        readonly object syncLock;
        IInnerSubscriptionClient innerSubscriptionClient;
        int prefetchCount;
        AmqpSessionClient sessionClient;
        SessionPumpHost sessionPumpHost;

        /// <summary>
        ///     Instantiates a new <see cref="SubscriptionClient" /> to perform operations on a subscription.
        /// </summary>
        /// <param name="connectionStringBuilder">
        ///     <see cref="ServiceBusConnectionStringBuilder" /> having namespace and topic
        ///     information.
        /// </param>
        /// <param name="subscriptionName">Name of the subscription.</param>
        /// <param name="receiveMode">Mode of receive of messages. Defaults to <see cref="ReceiveMode" />.PeekLock.</param>
        /// <param name="retryPolicy">
        ///     Retry policy for subscription operations. Defaults to <see cref="RetryPolicy.Default" />
        /// </param>
        public SubscriptionClient(ServiceBusConnectionStringBuilder connectionStringBuilder, string subscriptionName, ReceiveMode receiveMode = ReceiveMode.PeekLock, RetryPolicy retryPolicy = null)
            : this(connectionStringBuilder.GetNamespaceConnectionString(), connectionStringBuilder.EntityPath, subscriptionName, receiveMode, retryPolicy)
        {
        }

        /// <summary>
        ///     Instantiates a new <see cref="SubscriptionClient" /> to perform operations on a subscription.
        /// </summary>
        /// <param name="connectionString">
        ///     Namespace connection string.
        ///     <remarks>Should not contain topic information.</remarks>
        /// </param>
        /// <param name="topicPath">Path to the topic.</param>
        /// <param name="subscriptionName">Name of the subscription.</param>
        /// <param name="receiveMode">Mode of receive of messages. Defaults to <see cref="ReceiveMode" />.PeekLock.</param>
        /// <param name="retryPolicy">
        ///     Retry policy for subscription operations. Defaults to <see cref="RetryPolicy.Default" />
        /// </param>
        public SubscriptionClient(string connectionString, string topicPath, string subscriptionName, ReceiveMode receiveMode = ReceiveMode.PeekLock, RetryPolicy retryPolicy = null)
            : this(new ServiceBusNamespaceConnection(connectionString), topicPath, subscriptionName, receiveMode, retryPolicy ?? RetryPolicy.Default)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(connectionString);
            }
            if (string.IsNullOrWhiteSpace(topicPath))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(topicPath);
            }
            if (string.IsNullOrWhiteSpace(subscriptionName))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(subscriptionName);
            }

            ownsConnection = true;
        }

        SubscriptionClient(ServiceBusNamespaceConnection serviceBusConnection, string topicPath, string subscriptionName, ReceiveMode receiveMode, RetryPolicy retryPolicy)
            : base($"{nameof(SubscriptionClient)}{GetNextId()}({subscriptionName})", retryPolicy)
        {
            syncLock = new object();
            TopicPath = topicPath;
            ServiceBusConnection = serviceBusConnection;
            SubscriptionName = subscriptionName;
            Path = EntityNameHelper.FormatSubscriptionPath(TopicPath, SubscriptionName);
            ReceiveMode = receiveMode;
            TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(
                serviceBusConnection.SasKeyName,
                serviceBusConnection.SasKey);
            CbsTokenProvider = new TokenProviderAdapter(TokenProvider, serviceBusConnection.OperationTimeout);
        }

        /// <summary>
        ///     Gets or sets the number of messages that the subscription client can simultaneously request.
        /// </summary>
        /// <value>The number of messages that the subscription client can simultaneously request.</value>
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
                if (innerSubscriptionClient != null)
                {
                    innerSubscriptionClient.PrefetchCount = value;
                }
            }
        }

        internal IInnerSubscriptionClient InnerSubscriptionClient
        {
            get
            {
                if (innerSubscriptionClient == null)
                {
                    lock (syncLock)
                    {
                        innerSubscriptionClient = new AmqpSubscriptionClient(
                            Path,
                            ServiceBusConnection,
                            RetryPolicy,
                            CbsTokenProvider,
                            PrefetchCount,
                            ReceiveMode);
                    }
                }

                return innerSubscriptionClient;
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
                                MessagingEntityType.Subscriber,
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

        internal ServiceBusNamespaceConnection ServiceBusConnection { get; }

        ICbsTokenProvider CbsTokenProvider { get; }

        TokenProvider TokenProvider { get; }

        /// <summary>
        ///     Gets the path of the corresponding topic.
        /// </summary>
        public string TopicPath { get; }

        /// <summary>
        ///     Gets the path of the subscription client.
        /// </summary>
        public string Path { get; }

        /// <summary>
        ///     Gets the name of the subscription.
        /// </summary>
        public string SubscriptionName { get; }

        /// <summary>
        ///     Gets the <see cref="ReceiveMode.ReceiveMode" /> for the SubscriptionClient.
        /// </summary>
        public ReceiveMode ReceiveMode { get; }

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
            return InnerSubscriptionClient.InnerReceiver.CompleteAsync(lockToken);
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
            return InnerSubscriptionClient.InnerReceiver.AbandonAsync(lockToken);
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
            return InnerSubscriptionClient.InnerReceiver.DeadLetterAsync(lockToken);
        }

        /// <summary>Asynchronously processes a message.</summary>
        /// <param name="handler"></param>
        public void RegisterMessageHandler(Func<Message, CancellationToken, Task> handler)
        {
            InnerSubscriptionClient.InnerReceiver.RegisterMessageHandler(handler);
        }

        /// <summary>Asynchronously processes a message.</summary>
        /// <param name="handler"></param>
        /// <param name="registerHandlerOptions">Calls a message option.</param>
        public void RegisterMessageHandler(Func<Message, CancellationToken, Task> handler, MessageHandlerOptions registerHandlerOptions)
        {
            InnerSubscriptionClient.InnerReceiver.RegisterMessageHandler(handler, registerHandlerOptions);
        }

        /// <summary>
        ///     Asynchronously adds a rule to the current subscription with the specified name and filter expression.
        /// </summary>
        /// <param name="ruleName">The name of the rule to add.</param>
        /// <param name="filter">The filter expression against which messages will be matched.</param>
        /// <returns>A task instance that represents the asynchronous add rule operation.</returns>
        public Task AddRuleAsync(string ruleName, Filter filter)
        {
            return AddRuleAsync(new RuleDescription(ruleName, filter));
        }

        /// <summary>
        ///     Asynchronously adds a new rule to the subscription using the specified rule description.
        /// </summary>
        /// <param name="description">The rule description that provides metadata of the rule to add.</param>
        /// <returns>A task instance that represents the asynchronous add rule operation.</returns>
        public async Task AddRuleAsync(RuleDescription description)
        {
            if (description == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(description));
            }

            description.ValidateDescriptionName();
            MessagingEventSource.Log.AddRuleStart(ClientId, description.Name);

            try
            {
                await InnerSubscriptionClient.OnAddRuleAsync(description).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.AddRuleException(ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.AddRuleStop(ClientId);
        }

        /// <summary>
        ///     Asynchronously removes the rule described by <paramref name="ruleName" />.
        /// </summary>
        /// <param name="ruleName">The name of the rule.</param>
        /// <returns>A task instance that represents the asynchronous remove rule operation.</returns>
        public async Task RemoveRuleAsync(string ruleName)
        {
            if (string.IsNullOrWhiteSpace(ruleName))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(ruleName));
            }

            MessagingEventSource.Log.RemoveRuleStart(ClientId, ruleName);

            try
            {
                await InnerSubscriptionClient.OnRemoveRuleAsync(ruleName).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.RemoveRuleException(ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.RemoveRuleStop(ClientId);
        }

        /// <summary></summary>
        /// <returns></returns>
        protected override async Task OnClosingAsync()
        {
            if (innerSubscriptionClient != null)
            {
                await innerSubscriptionClient.CloseAsync().ConfigureAwait(false);
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
        ///     Registers a <see cref="ServiceBusPlugin" /> to be used for receiving messages from Service Bus.
        /// </summary>
        /// <param name="serviceBusPlugin">The <see cref="ServiceBusPlugin" /> to register</param>
        public void RegisterPlugin(ServiceBusPlugin serviceBusPlugin)
        {
            InnerSubscriptionClient.InnerReceiver.RegisterPlugin(serviceBusPlugin);
        }

        /// <summary>
        ///     Unregisters a <see cref="ServiceBusPlugin" />.
        /// </summary>
        /// <param name="serviceBusPluginName">The name <see cref="ServiceBusPlugin.Name" /> to be unregistered</param>
        public void UnregisterPlugin(string serviceBusPluginName)
        {
            InnerSubscriptionClient.InnerReceiver.UnregisterPlugin(serviceBusPluginName);
        }
    }
}