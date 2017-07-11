﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Amqp;
    using Azure.Amqp;
    using Core;
    using Primitives;

    /// <summary> 
    /// A session client can be used to accept session objects which can be used to interact with all messages with the same sessionId. 
    /// </summary> 
    /// <remarks> 
    /// You can accept any session or a given session (identified by <see cref="IMessageSession.SessionId"/> using a session client. 
    /// Once you accept a session, you can use it as a <see cref="MessageReceiver"/> which receives only messages having the same session id.
    /// See <see cref="IMessageSession"/> for usage of session object.
    /// This uses AMQP protocol to communicate with the service.
    /// </remarks>
    /// <example>
    /// To create a new SessionClient
    /// <code>
    /// ISessionClient sessionClient = new SessionClient(
    ///     namespaceConnectionString,
    ///     queueName,
    ///     ReceiveMode.PeekLock);
    /// </code>
    /// 
    /// To receive a session object for a given sessionId
    /// <code>
    /// IMessageSession session = await sessionClient.AcceptMessageSessionAsync(sessionId);
    /// </code>
    /// 
    /// To receive any session
    /// <code>
    /// IMessageSession session = await sessionClient.AcceptMessageSessionAsync();
    /// </code>
    /// </example>
    /// <seealso cref="IMessageSession"/>
    public sealed class SessionClient : ClientEntity, ISessionClient
    {
        const int DefaultPrefetchCount = 0;
        readonly bool ownsConnection;

        /// <summary>
        /// Creates a new SessionClient from a <see cref="ServiceBusConnectionStringBuilder"/>
        /// </summary>
        /// <param name="connectionStringBuilder">The <see cref="ServiceBusConnectionStringBuilder"/> having entity level connection details.</param>
        /// <param name="receiveMode">The <see cref="ReceiveMode"/> used to specify how messages are received. Defaults to PeekLock mode.</param>
        /// <param name="retryPolicy">The <see cref="RetryPolicy"/> that will be used when communicating with ServiceBus. Defaults to <see cref="RetryPolicy.Default"/></param>
        /// <param name="prefetchCount">The <see cref="PrefetchCount"/> that specifies the upper limit of messages the session object
        /// will actively receive regardless of whether a receive operation is pending. Defaults to 0.</param>
        /// <remarks>Creates a new connection to the entity, which is used for all the sessions objects accepted using this client.</remarks>
        public SessionClient(
            ServiceBusConnectionStringBuilder connectionStringBuilder,
            ReceiveMode receiveMode = ReceiveMode.PeekLock,
            RetryPolicy retryPolicy = null,
            int prefetchCount = DefaultPrefetchCount)
            : this(connectionStringBuilder?.GetNamespaceConnectionString(), connectionStringBuilder?.EntityPath, receiveMode, retryPolicy, prefetchCount)
        {   
        }

        /// <summary>
        /// Creates a new SessionClient from a specified connection string and entity path.
        /// </summary>
        /// <param name="connectionString">Namespace connection string used to communicate with Service Bus. Must not contain entity details.</param>
        /// <param name="entityPath">The path of the entity for this receiver. For Queues this will be the anme, but for Subscriptions this will be the full path.</param>
        /// <param name="receiveMode">The <see cref="ReceiveMode"/> used to specify how messages are received. Defaults to PeekLock mode.</param>
        /// <param name="retryPolicy">The <see cref="RetryPolicy"/> that will be used when communicating with ServiceBus. Defaults to <see cref="RetryPolicy.Default"/></param>
        /// <param name="prefetchCount">The <see cref="PrefetchCount"/> that specifies the upper limit of messages the session object
        /// will actively receive regardless of whether a receive operation is pending. Defaults to 0.</param>
        /// <remarks>Creates a new connection to the entity, which is used for all the sessions objects accepted using this client.</remarks>
        public SessionClient(
            string connectionString,
            string entityPath,
            ReceiveMode receiveMode = ReceiveMode.PeekLock,
            RetryPolicy retryPolicy = null,
            int prefetchCount = DefaultPrefetchCount)
            : this(ClientEntity.GenerateClientId(nameof(SessionClient), entityPath),
                  entityPath,
                  null,
                  receiveMode,
                  prefetchCount,
                  new ServiceBusNamespaceConnection(connectionString),
                  null,
                  retryPolicy)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(connectionString);
            }
            if (string.IsNullOrWhiteSpace(entityPath))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(entityPath);
            }

            this.ownsConnection = true;
            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(this.ServiceBusConnection.SasKeyName, this.ServiceBusConnection.SasKey);
            this.CbsTokenProvider = new TokenProviderAdapter(tokenProvider, this.ServiceBusConnection.OperationTimeout);
        }

        internal SessionClient(
            string clientId,
            string entityPath,
            MessagingEntityType? entityType,
            ReceiveMode receiveMode,
            int prefetchCount,
            ServiceBusConnection serviceBusConnection,
            ICbsTokenProvider cbsTokenProvider,
            RetryPolicy retryPolicy)
            : base(clientId, retryPolicy ?? RetryPolicy.Default)
        {
            this.ServiceBusConnection = serviceBusConnection ?? throw new ArgumentNullException(nameof(serviceBusConnection));
            this.EntityPath = entityPath;
            this.EntityType = entityType;
            this.ReceiveMode = receiveMode;
            this.PrefetchCount = prefetchCount;
            this.CbsTokenProvider = cbsTokenProvider;
        }

        ReceiveMode ReceiveMode { get; }

        /// <summary>
        /// Gets the path of the entity. This is either the name of the queue, or the full path of the subscription.
        /// </summary>
        public string EntityPath { get; }

        MessagingEntityType? EntityType { get; }

        int PrefetchCount { get; }

        ServiceBusConnection ServiceBusConnection { get; }

        ICbsTokenProvider CbsTokenProvider { get; }

        /// <summary>
        /// Gets a list of currently registered plugins.
        /// </summary>
        public override IList<ServiceBusPlugin> RegisteredPlugins => throw new NotImplementedException();

        /// <summary></summary>
        /// <returns>The asynchronous operation.</returns>
        protected override async Task OnClosingAsync()
        {
            if (this.ownsConnection)
            {
                await this.ServiceBusConnection.CloseAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets a session object of any <see cref="IMessageSession.SessionId"/> that can be used to receive messages for that sessionId.
        /// </summary>
        /// <returns>A session object.</returns>
        public Task<IMessageSession> AcceptMessageSessionAsync()
        {
            return this.AcceptMessageSessionAsync(this.ServiceBusConnection.OperationTimeout);
        }

        /// <summary>
        /// Gets a session object of any <see cref="IMessageSession.SessionId"/> that can be used to receive messages for that sessionId.
        /// </summary>
        /// <param name="serverWaitTime">Amount of time for which the call should wait to fetch the next session.</param>
        /// <returns>A session object.</returns>
        public Task<IMessageSession> AcceptMessageSessionAsync(TimeSpan serverWaitTime)
        {
            return this.AcceptMessageSessionAsync(null, serverWaitTime);
        }

        /// <summary>
        /// Gets a particular session object identified by <paramref name="sessionId"/> that can be used to receive messages for that sessionId.
        /// </summary>
        /// <param name="sessionId">The sessionId present in all its messages.</param>
        /// <returns>A session object.</returns>
        public Task<IMessageSession> AcceptMessageSessionAsync(string sessionId)
        {
            return this.AcceptMessageSessionAsync(sessionId, this.ServiceBusConnection.OperationTimeout);
        }

        /// <summary>
        /// Gets a particular session object identified by <paramref name="sessionId"/> that can be used to receive messages for that sessionId.
        /// </summary>
        /// <param name="sessionId">The sessionId present in all its messages.</param>
        /// <param name="serverWaitTime">Amount of time for which the call should wait to fetch the next session.</param>
        /// <returns>A session object.</returns>
        public async Task<IMessageSession> AcceptMessageSessionAsync(string sessionId, TimeSpan serverWaitTime)
        {
            MessagingEventSource.Log.AmqpSessionClientAcceptMessageSessionStart(
                this.ClientId,
                this.EntityPath,
                this.ReceiveMode,
                this.PrefetchCount,
                sessionId);

            var session = new MessageSession(
                this.EntityPath,
                this.EntityType,
                this.ReceiveMode,
                this.ServiceBusConnection,
                this.CbsTokenProvider,
                this.RetryPolicy,
                this.PrefetchCount,
                sessionId,
                true);

            try
            {
                await this.RetryPolicy.RunOperation(
                    async () =>
                    {
                        await session.GetSessionReceiverLinkAsync(serverWaitTime).ConfigureAwait(false);
                    }, serverWaitTime)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.AmqpSessionClientAcceptMessageSessionException(
                    this.ClientId,
                    this.EntityPath,
                    exception);

                await session.CloseAsync().ConfigureAwait(false);
                throw AmqpExceptionHelper.GetClientException(exception);
            }
            
            MessagingEventSource.Log.AmqpSessionClientAcceptMessageSessionStop(
                this.ClientId,
                this.EntityPath,
                session.SessionIdInternal);

            session.UpdateClientId(ClientEntity.GenerateClientId(nameof(MessageSession), $"{this.EntityPath}_{session.SessionId}"));
            return session;
        }

        /// <summary>
        /// Registers a <see cref="ServiceBusPlugin"/> to be used with this receiver.
        /// </summary>
        /// <param name="serviceBusPlugin">The <see cref="ServiceBusPlugin"/> to register.</param>
        public override void RegisterPlugin(ServiceBusPlugin serviceBusPlugin)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unregisters a <see cref="ServiceBusPlugin"/>.
        /// </summary>
        /// <param name="serviceBusPluginName">The <see cref="ServiceBusPlugin.Name"/> of the plugin to be unregistered.</param>
        public override void UnregisterPlugin(string serviceBusPluginName)
        {
            throw new NotImplementedException();
        }
    }
}