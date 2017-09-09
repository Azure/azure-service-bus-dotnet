// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Amqp;
    using Azure.Amqp.Encoding;
    using Azure.Amqp.Framing;
    using Amqp;
    using Primitives;

    /// <summary>
    /// The MessageReceiver can be used to receive messages from Queues and Subscriptions and acknowledge them.
    /// </summary>
    /// <example>
    /// Create a new MessageReceiver to receive a message from a Subscription
    /// <code>
    /// IMessageReceiver messageReceiver = new MessageReceiver(
    ///     namespaceConnectionString,
    ///     EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName),
    ///     ReceiveMode.PeekLock);
    /// </code>
    ///
    /// Receive a message from the Subscription.
    /// <code>
    /// var message = await messageReceiver.ReceiveAsync();
    /// await messageReceiver.CompleteAsync(message.SystemProperties.LockToken);
    /// </code>
    /// </example>
    /// <remarks>
    /// The MessageReceiver provides advanced functionality that is not found in the
    /// <see cref="QueueClient" /> or <see cref="SubscriptionClient" />. For instance,
    /// <see cref="ReceiveAsync()"/>, which allows you to receive messages on demand, but also requires
    /// you to manually renew locks using <see cref="RenewLockAsync(Message)"/>.
    /// It uses AMQP protocol to communicate with service.
    /// </remarks>
    public class MessageReceiver : ClientEntity, IMessageReceiver
    {
        private static readonly TimeSpan DefaultBatchFlushInterval = TimeSpan.FromMilliseconds(20);

        readonly ConcurrentExpiringSet<Guid> requestResponseLockedMessages;
        readonly bool isSessionReceiver;
        readonly object messageReceivePumpSyncLock;
        readonly bool ownsConnection;
        readonly ActiveClientLinkManager clientLinkManager;

        int prefetchCount;
        long lastPeekedSequenceNumber;
        MessageReceivePump receivePump;
        CancellationTokenSource receivePumpCancellationTokenSource;

        /// <summary>
        /// Creates a new MessageReceiver from a <see cref="ServiceBusConnectionStringBuilder"/>.
        /// </summary>
        /// <param name="connectionStringBuilder">The <see cref="ServiceBusConnectionStringBuilder"/> having entity level connection details.</param>
        /// <param name="receiveMode">The <see cref="ServiceBus.ReceiveMode"/> used to specify how messages are received. Defaults to PeekLock mode.</param>
        /// <param name="retryPolicy">The <see cref="RetryPolicy"/> that will be used when communicating with Service Bus. Defaults to <see cref="RetryPolicy.Default"/>.</param>
        /// <param name="prefetchCount">The <see cref="PrefetchCount"/> that specifies the upper limit of messages this receiver
        /// will actively receive regardless of whether a receive operation is pending. Defaults to 0.</param>
        /// <remarks>Creates a new connection to the entity, which is opened during the first operation.</remarks>
        public MessageReceiver(
            ServiceBusConnectionStringBuilder connectionStringBuilder,
            ReceiveMode receiveMode = ReceiveMode.PeekLock,
            RetryPolicy retryPolicy = null,
            int prefetchCount = Constants.DefaultClientPrefetchCount)
            : this(connectionStringBuilder?.GetNamespaceConnectionString(), connectionStringBuilder?.EntityPath, receiveMode, retryPolicy, prefetchCount)
        {
        }

        /// <summary>
        /// Creates a new MessageReceiver from a specified connection string and entity path.
        /// </summary>
        /// <param name="connectionString">Namespace connection string used to communicate with Service Bus. Must not contain Entity details.</param>
        /// <param name="entityPath">The path of the entity for this receiver. For Queues this will be the name, but for Subscriptions this will be the path.
        /// You can use <see cref="EntityNameHelper.FormatSubscriptionPath(string, string)"/>, to help create this path.</param>
        /// <param name="receiveMode">The <see cref="ServiceBus.ReceiveMode"/> used to specify how messages are received. Defaults to PeekLock mode.</param>
        /// <param name="retryPolicy">The <see cref="RetryPolicy"/> that will be used when communicating with Service Bus. Defaults to <see cref="RetryPolicy.Default"/></param>
        /// <param name="prefetchCount">The <see cref="PrefetchCount"/> that specifies the upper limit of messages this receiver
        /// will actively receive regardless of whether a receive operation is pending. Defaults to 0.</param>
        /// <remarks>Creates a new connection to the entity, which is opened during the first operation.</remarks>
        public MessageReceiver(
            string connectionString,
            string entityPath,
            ReceiveMode receiveMode = ReceiveMode.PeekLock,
            RetryPolicy retryPolicy = null,
            int prefetchCount = Constants.DefaultClientPrefetchCount)
            : this(entityPath, null, receiveMode, new ServiceBusNamespaceConnection(connectionString), null, retryPolicy, prefetchCount)
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
            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(ServiceBusConnection.SasKeyName, ServiceBusConnection.SasKey);
            CbsTokenProvider = new TokenProviderAdapter(tokenProvider, ServiceBusConnection.OperationTimeout);
        }

        internal MessageReceiver(
            string entityPath,
            MessagingEntityType? entityType,
            ReceiveMode receiveMode,
            ServiceBusConnection serviceBusConnection,
            ICbsTokenProvider cbsTokenProvider,
            RetryPolicy retryPolicy,
            int prefetchCount = Constants.DefaultClientPrefetchCount,
            string sessionId = null,
            bool isSessionReceiver = false)
            : base(nameof(MessageReceiver), entityPath, retryPolicy ?? RetryPolicy.Default)
        {
            MessagingEventSource.Log.MessageReceiverCreateStart(serviceBusConnection?.Endpoint.Authority, entityPath, receiveMode.ToString());

            ServiceBusConnection = serviceBusConnection ?? throw new ArgumentNullException(nameof(serviceBusConnection));
            ReceiveMode = receiveMode;
            OperationTimeout = serviceBusConnection.OperationTimeout;
            Path = entityPath;
            EntityType = entityType;
            CbsTokenProvider = cbsTokenProvider;
            SessionIdInternal = sessionId;
            this.isSessionReceiver = isSessionReceiver;
            ReceiveLinkManager = new FaultTolerantAmqpObject<ReceivingAmqpLink>(CreateLinkAsync, CloseSession);
            RequestResponseLinkManager = new FaultTolerantAmqpObject<RequestResponseAmqpLink>(CreateRequestResponseLinkAsync, CloseRequestResponseSession);
            requestResponseLockedMessages = new ConcurrentExpiringSet<Guid>();
            PrefetchCount = prefetchCount;
            messageReceivePumpSyncLock = new object();
            clientLinkManager = new ActiveClientLinkManager(ClientId, CbsTokenProvider);

            MessagingEventSource.Log.MessageReceiverCreateStop(serviceBusConnection.Endpoint.Authority, entityPath, ClientId);
        }

        /// <summary>
        /// Gets a list of currently registered plugins.
        /// </summary>
        public override IList<ServiceBusPlugin> RegisteredPlugins { get; } = new List<ServiceBusPlugin>();

        /// <summary>
        /// Gets the <see cref="ServiceBus.ReceiveMode"/> of the current receiver.
        /// </summary>
        public ReceiveMode ReceiveMode { get; protected set; }

        /// <summary>
        /// Prefetch speeds up the message flow by aiming to have a message readily available for local retrieval when and before the application asks for one using Receive.
        /// Setting a non-zero value prefetches PrefetchCount number of messages.
        /// Setting the value to zero turns prefetch off.
        /// Defaults to 0.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When Prefetch is enabled, the receiver will quietly acquire more messages, up to the PrefetchCount limit, than what the application
        /// immediately asks for. A single initial Receive/ReceiveAsync call will therefore acquire a message for immediate consumption
        /// that will be returned as soon as available, and the client will proceed to acquire further messages to fill the prefetch buffer in the background.
        /// </para>
        /// <para>
        /// While messages are available in the prefetch buffer, any subsequent ReceiveAsync calls will be immediately satisfied from the buffer, and the buffer is
        /// replenished in the background as space becomes available.If there are no messages available for delivery, the receive operation will drain the
        /// buffer and then wait or block as expected.
        /// </para>
        /// <para>Prefetch also works equivalently with the <see cref="RegisterMessageHandler(Func{Message,CancellationToken,Task}, Func{ExceptionReceivedEventArgs, Task})"/> APIs.</para>
        /// <para>Updates to this value take effect on the next receive call to the service.</para>
        /// </remarks>
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
                if (ReceiveLinkManager.TryGetOpenedObject(out var link))
                {
                    link.SetTotalLinkCredit((uint)value, true, true);
                }
            }
        }

        /// <summary>Gets the sequence number of the last peeked message.</summary>
        /// <seealso cref="PeekAsync()"/>
        public long LastPeekedSequenceNumber
        {
            get => lastPeekedSequenceNumber;

            internal set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(LastPeekedSequenceNumber), value.ToString());
                }

                lastPeekedSequenceNumber = value;
            }
        }

        /// <summary>The path of the entity for this receiver. For Queues this will be the name, but for Subscriptions this will be the path.</summary>
        public virtual string Path { get; }

        /// <summary>
        /// Duration after which individual operations will timeout.
        /// </summary>
        public override TimeSpan OperationTimeout {
            get => ServiceBusConnection.OperationTimeout;
            set => ServiceBusConnection.OperationTimeout = value;
        }

        /// <summary>
        /// Gets the DateTime that the current receiver is locked until. This is only applicable when Sessions are used.
        /// </summary>
        internal DateTime LockedUntilUtcInternal { get; set; }

        /// <summary>
        /// Gets the SessionId of the current receiver. This is only applicable when Sessions are used.
        /// </summary>
        internal string SessionIdInternal { get; set; }

        internal MessagingEntityType? EntityType { get; }

        Exception LinkException { get; set; }

        ServiceBusConnection ServiceBusConnection { get; }

        ICbsTokenProvider CbsTokenProvider { get; }

        FaultTolerantAmqpObject<ReceivingAmqpLink> ReceiveLinkManager { get; }

        FaultTolerantAmqpObject<RequestResponseAmqpLink> RequestResponseLinkManager { get; }

        /// <summary>
        /// Receive a message from the entity defined by <see cref="Path"/> using <see cref="ReceiveMode"/> mode.
        /// </summary>
        /// <returns>The message received. Returns null if no message is found.</returns>
        /// <remarks>Operation will time out after duration of <see cref="ClientEntity.OperationTimeout"/></remarks>
        public Task<Message> ReceiveAsync()
        {
            return ReceiveAsync(OperationTimeout);
        }

        /// <summary>
        /// Receive a message from the entity defined by <see cref="Path"/> using <see cref="ReceiveMode"/> mode.
        /// </summary>
        /// <param name="operationTimeout">The time span the client waits for receiving a message before it times out.</param>
        /// <returns>The message received. Returns null if no message is found.</returns>
        public async Task<Message> ReceiveAsync(TimeSpan operationTimeout)
        {
            IList<Message> messages = await ReceiveAsync(1, operationTimeout).ConfigureAwait(false);
            if (messages != null && messages.Count > 0)
            {
                return messages[0];
            }

            return null;
        }

        /// <summary>
        /// Receives a maximum of <paramref name="maxMessageCount"/> messages from the entity defined by <see cref="Path"/> using <see cref="ReceiveMode"/> mode.
        /// </summary>
        /// <param name="maxMessageCount">The maximum number of messages that will be received.</param>
        /// <returns>List of messages received. Returns null if no message is found.</returns>
        /// <remarks>Receiving less than <paramref name="maxMessageCount"/> messages is not an indication of empty entity.</remarks>
        public Task<IList<Message>> ReceiveAsync(int maxMessageCount)
        {
            return ReceiveAsync(maxMessageCount, OperationTimeout);
        }

        /// <summary>
        /// Receives a maximum of <paramref name="maxMessageCount"/> messages from the entity defined by <see cref="Path"/> using <see cref="ReceiveMode"/> mode.
        /// </summary>
        /// <param name="maxMessageCount">The maximum number of messages that will be received.</param>
        /// <param name="operationTimeout">The time span the client waits for receiving a message before it times out.</param>
        /// <returns>List of messages received. Returns null if no message is found.</returns>
        /// <remarks>Receiving less than <paramref name="maxMessageCount"/> messages is not an indication of empty entity.</remarks>
        public async Task<IList<Message>> ReceiveAsync(int maxMessageCount, TimeSpan operationTimeout)
        {
            ThrowIfClosed();

            if (operationTimeout <= TimeSpan.Zero)
            {
                throw Fx.Exception.ArgumentOutOfRange(nameof(operationTimeout), operationTimeout, Resources.TimeoutMustBePositiveNonZero.FormatForUser(nameof(operationTimeout), operationTimeout));
            }

            MessagingEventSource.Log.MessageReceiveStart(ClientId, maxMessageCount);

            IList<Message> unprocessedMessageList = null;
            try
            {
                await RetryPolicy.RunOperation(
                    async () =>
                    {
                        unprocessedMessageList = await OnReceiveAsync(maxMessageCount, operationTimeout).ConfigureAwait(false);
                    }, operationTimeout)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageReceiveException(ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageReceiveStop(ClientId, unprocessedMessageList?.Count ?? 0);

            if (unprocessedMessageList == null)
            {
                return unprocessedMessageList;
            }

            return await ProcessMessages(unprocessedMessageList).ConfigureAwait(false);
        }

        /// <summary>
        /// Receives a specific deferred message identified by <paramref name="sequenceNumber"/>.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number of the message that will be received.</param>
        /// <returns>Message identified by sequence number <paramref name="sequenceNumber"/>. Returns null if no such message is found.
        /// Throws if the message has not been deferred.</returns>
        /// <seealso cref="DeferAsync"/>
        public async Task<Message> ReceiveDeferredMessageAsync(long sequenceNumber)
        {
            IList<Message> messages = await ReceiveDeferredMessageAsync(new[] { sequenceNumber }).ConfigureAwait(false);
            if (messages != null && messages.Count > 0)
            {
                return messages[0];
            }

            return null;
        }

        /// <summary>
        /// Receives a <see cref="IList{Message}"/> of deferred messages identified by <paramref name="sequenceNumbers"/>.
        /// </summary>
        /// <param name="sequenceNumbers">An <see cref="IEnumerable{T}"/> containing the sequence numbers to receive.</param>
        /// <returns>Messages identified by sequence number are returned. Returns null if no messages are found.
        /// Throws if the messages have not been deferred.</returns>
        /// <seealso cref="DeferAsync"/>
        public async Task<IList<Message>> ReceiveDeferredMessageAsync(IEnumerable<long> sequenceNumbers)
        {
            ThrowIfClosed();
            ThrowIfNotPeekLockMode();

            int count = ValidateSequenceNumbers(sequenceNumbers);

            MessagingEventSource.Log.MessageReceiveDeferredMessageStart(ClientId, count, sequenceNumbers);

            IList<Message> messages = null;
            try
            {
                await RetryPolicy.RunOperation(
                    async () =>
                    {
                        messages = await OnReceiveDeferredMessageAsync(sequenceNumbers).ConfigureAwait(false);
                    }, OperationTimeout)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageReceiveDeferredMessageException(ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageReceiveDeferredMessageStop(ClientId, messages?.Count ?? 0);

            return messages;
        }

        /// <summary>
        /// Completes a <see cref="Message"/> using its lock token. This will delete the message from the service.
        /// </summary>
        /// <param name="lockToken">The lock token of the corresponding message to complete.</param>
        /// <remarks>
        /// A lock token can be found in <see cref="Message.SystemPropertiesCollection.LockToken"/>,
        /// only when <see cref="ReceiveMode"/> is set to <see cref="ServiceBus.ReceiveMode.PeekLock"/>.
        /// This operation can only be performed on messages that were received by this receiver.
        /// </remarks>
        public Task CompleteAsync(string lockToken)
        {
            return CompleteAsync(new[] { lockToken });
        }

        /// <summary>
        /// Completes a series of <see cref="Message"/> using a list of lock tokens. This will delete the message from the service.
        /// </summary>
        /// <remarks>
        /// A lock token can be found in <see cref="Message.SystemPropertiesCollection.LockToken"/>,
        /// only when <see cref="ReceiveMode"/> is set to <see cref="ServiceBus.ReceiveMode.PeekLock"/>.
        /// This operation can only be performed on messages that were received by this receiver.
        /// </remarks>
        /// <param name="lockTokens">An <see cref="IEnumerable{T}"/> containing the lock tokens of the corresponding messages to complete.</param>
        public async Task CompleteAsync(IEnumerable<string> lockTokens)
        {
            ThrowIfClosed();
            ThrowIfNotPeekLockMode();
            int count = ValidateLockTokens(lockTokens);

            MessagingEventSource.Log.MessageCompleteStart(ClientId, count, lockTokens);

            try
            {
                await RetryPolicy.RunOperation(
                    async () =>
                    {
                        await OnCompleteAsync(lockTokens).ConfigureAwait(false);
                    }, OperationTimeout)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageCompleteException(ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageCompleteStop(ClientId);
        }

        /// <summary>
        /// Abandons a <see cref="Message"/> using a lock token. This will make the message available again for processing.
        /// </summary>
        /// <param name="lockToken">The lock token of the corresponding message to abandon.</param>
        /// <remarks>A lock token can be found in <see cref="Message.SystemPropertiesCollection.LockToken"/>,
        /// only when <see cref="ReceiveMode"/> is set to <see cref="ServiceBus.ReceiveMode.PeekLock"/>.
        /// Abandoning a message will increase the delivery count on the message.
        /// This operation can only be performed on messages that were received by this receiver.
        /// </remarks>
        public async Task AbandonAsync(string lockToken)
        {
            ThrowIfClosed();
            ThrowIfNotPeekLockMode();

            MessagingEventSource.Log.MessageAbandonStart(ClientId, 1, lockToken);
            try
            {
                await RetryPolicy.RunOperation(
                    async () =>
                    {
                        await OnAbandonAsync(lockToken).ConfigureAwait(false);
                    }, OperationTimeout)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageAbandonException(ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageAbandonStop(ClientId);
        }

        /// <summary>Indicates that the receiver wants to defer the processing for the message.</summary>
        /// <param name="lockToken">The lock token of the <see cref="Message" />.</param>
        /// <remarks>
        /// A lock token can be found in <see cref="Message.SystemPropertiesCollection.LockToken"/>,
        /// only when <see cref="ReceiveMode"/> is set to <see cref="ServiceBus.ReceiveMode.PeekLock"/>.
        /// In order to receive this message again in the future, you will need to save the <see cref="Message.SystemPropertiesCollection.SequenceNumber"/>
        /// and receive it using <see cref="ReceiveDeferredMessageAsync(long)"/>.
        /// Deferring messages does not impact message's expiration, meaning that deferred messages can still expire.
        /// This operation can only be performed on messages that were received by this receiver.
        /// </remarks>
        public async Task DeferAsync(string lockToken)
        {
            ThrowIfClosed();
            ThrowIfNotPeekLockMode();

            MessagingEventSource.Log.MessageDeferStart(ClientId, 1, lockToken);

            try
            {
                await RetryPolicy.RunOperation(
                    async () =>
                    {
                        await OnDeferAsync(lockToken).ConfigureAwait(false);
                    }, OperationTimeout)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageDeferException(ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageDeferStop(ClientId);
        }

        /// <summary>
        /// Moves a message to the deadletter sub-queue.
        /// </summary>
        /// <param name="lockToken">The lock token of the corresponding message to deadletter.</param>
        /// <remarks>
        /// A lock token can be found in <see cref="Message.SystemPropertiesCollection.LockToken"/>,
        /// only when <see cref="ReceiveMode"/> is set to <see cref="ServiceBus.ReceiveMode.PeekLock"/>.
        /// In order to receive a message from the deadletter queue, you will need a new <see cref="IMessageReceiver"/>, with the corresponding path.
        /// You can use <see cref="EntityNameHelper.FormatDeadLetterPath(string)"/> to help with this.
        /// This operation can only be performed on messages that were received by this receiver.
        /// </remarks>
        public async Task DeadLetterAsync(string lockToken)
        {
            ThrowIfClosed();
            ThrowIfNotPeekLockMode();

            MessagingEventSource.Log.MessageDeadLetterStart(ClientId, 1, lockToken);

            try
            {
                await RetryPolicy.RunOperation(
                    async () =>
                    {
                        await OnDeadLetterAsync(lockToken).ConfigureAwait(false);
                    }, OperationTimeout)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageDeadLetterException(ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageDeadLetterStop(ClientId);
        }

        /// <summary>
        /// Renews the lock on the message specified by the lock token. The lock will be renewed based on the setting specified on the queue.
        /// </summary>
        /// <param name="message"> <see cref="Message" />.</param>
        /// <remarks>
        /// When a message is received in <see cref="ServiceBus.ReceiveMode.PeekLock"/> mode, the message is locked on the server for this
        /// receiver instance for a duration as specified during the Queue/Subscription creation (LockDuration).
        /// If processing of the message requires longer than this duration, the lock needs to be renewed. For each renewal, the lock is renewed by
        /// the entity's LockDuration.
        /// </remarks>
        public async Task RenewLockAsync(Message message)
        {
            ThrowIfClosed();
            ThrowIfNotPeekLockMode();

            MessagingEventSource.Log.MessageRenewLockStart(ClientId, 1, message.SystemProperties.LockToken);

            try
            {
                await RetryPolicy.RunOperation(
                    async () =>
                    {
                        message.SystemProperties.LockedUntilUtc = await OnRenewLockAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
                    }, OperationTimeout)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageRenewLockException(ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageRenewLockStop(ClientId);
        }

        /// <summary>
        /// Fetches the next active message without changing the state of the receiver or the message source.
        /// </summary>
        /// <remarks>
        /// The first call to <see cref="PeekAsync()"/> fetches the first active message for this receiver. Each subsequent call
        /// fetches the subsequent message in the entity.
        /// Unlike a received message, peeked message will not have lock token associated with it, and hence it cannot be Completed/Abandoned/Deferred/Deadlettered/Renewed.
        /// Also, unlike <see cref="ReceiveAsync()"/>, this method will fetch even Deferred messages (but not Deadlettered message)
        /// </remarks>
        /// <returns>The <see cref="Message" /> that represents the next message to be read. Returns null when nothing to peek.</returns>
        public Task<Message> PeekAsync()
        {
            return PeekBySequenceNumberAsync(lastPeekedSequenceNumber + 1);
        }

        /// <summary>
        /// Fetches the next batch of active messages without changing the state of the receiver or the message source.
        /// </summary>
        /// <remarks>
        /// The first call to <see cref="PeekAsync()"/> fetches the first active message for this receiver. Each subsequent call
        /// fetches the subsequent message in the entity.
        /// Unlike a received message, peeked message will not have lock token associated with it, and hence it cannot be Completed/Abandoned/Deferred/Deadlettered/Renewed.
        /// Also, unlike <see cref="ReceiveAsync()"/>, this method will fetch even Deferred messages (but not Deadlettered message)
        /// </remarks>
        /// <returns>List of <see cref="Message" /> that represents the next message to be read. Returns null when nothing to peek.</returns>
        public Task<IList<Message>> PeekAsync(int maxMessageCount)
        {
            return PeekBySequenceNumberAsync(lastPeekedSequenceNumber + 1, maxMessageCount);
        }

        /// <summary>
        /// Asynchronously reads the next message without changing the state of the receiver or the message source.
        /// </summary>
        /// <param name="fromSequenceNumber">The sequence number from where to read the message.</param>
        /// <returns>The asynchronous operation that returns the <see cref="Message" /> that represents the next message to be read.</returns>
        public async Task<Message> PeekBySequenceNumberAsync(long fromSequenceNumber)
        {
            var messages = await PeekBySequenceNumberAsync(fromSequenceNumber, 1).ConfigureAwait(false);
            return messages?.FirstOrDefault();
        }

        /// <summary>Peeks a batch of messages.</summary>
        /// <param name="fromSequenceNumber">The starting point from which to browse a batch of messages.</param>
        /// <param name="messageCount">The number of messages.</param>
        /// <returns>A batch of messages peeked.</returns>
        public async Task<IList<Message>> PeekBySequenceNumberAsync(long fromSequenceNumber, int messageCount)
        {
            ThrowIfClosed();
            IList<Message> messages = null;

            MessagingEventSource.Log.MessagePeekStart(ClientId, fromSequenceNumber, messageCount);
            try
            {
                await RetryPolicy.RunOperation(
                    async () =>
                    {
                        messages = await OnPeekAsync(fromSequenceNumber, messageCount).ConfigureAwait(false);
                    }, OperationTimeout)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessagePeekException(ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessagePeekStop(ClientId, messages?.Count ?? 0);
            return messages;
        }

        /// <summary>
        /// Receive messages continuously from the entity. Registers a message handler and begins a new thread to receive messages.
        /// This handler(<see cref="Func{Message, CancellationToken, Task}"/>) is awaited on every time a new message is received by the receiver.
        /// </summary>
        /// <param name="handler">A <see cref="Func{T1, T2, TResult}"/> that processes messages.</param>
        /// <param name="exceptionReceivedHandler">A <see cref="Func{T1, TResult}"/> that is used to notify exceptions.</param>
        public void RegisterMessageHandler(Func<Message, CancellationToken, Task> handler, Func<ExceptionReceivedEventArgs, Task> exceptionReceivedHandler)
        {
            RegisterMessageHandler(handler, new MessageHandlerOptions(exceptionReceivedHandler));
        }

        /// <summary>
        /// Receive messages continuously from the entity. Registers a message handler and begins a new thread to receive messages.
        /// This handler(<see cref="Func{Message, CancellationToken, Task}"/>) is awaited on every time a new message is received by the receiver.
        /// </summary>
        /// <param name="handler">A <see cref="Func{Message, CancellationToken, Task}"/> that processes messages.</param>
        /// <param name="messageHandlerOptions">The <see cref="MessageHandlerOptions"/> options used to configure the settings of the pump.</param>
        /// <remarks>Enable prefetch to speed up the receive rate.</remarks>
        public void RegisterMessageHandler(Func<Message, CancellationToken, Task> handler, MessageHandlerOptions messageHandlerOptions)
        {
            ThrowIfClosed();
            OnMessageHandler(messageHandlerOptions, handler);
        }

        /// <summary>
        /// Registers a <see cref="ServiceBusPlugin"/> to be used with this receiver.
        /// </summary>
        /// <param name="serviceBusPlugin">The <see cref="ServiceBusPlugin"/> to register.</param>
        public override void RegisterPlugin(ServiceBusPlugin serviceBusPlugin)
        {
            ThrowIfClosed();
            if (serviceBusPlugin == null)
            {
                throw new ArgumentNullException(nameof(serviceBusPlugin), Resources.ArgumentNullOrWhiteSpace.FormatForUser(nameof(serviceBusPlugin)));
            }
            if (RegisteredPlugins.Any(p => p.Name == serviceBusPlugin.Name))
            {
                throw new ArgumentException(nameof(serviceBusPlugin), Resources.PluginAlreadyRegistered.FormatForUser(nameof(serviceBusPlugin)));
            }
            RegisteredPlugins.Add(serviceBusPlugin);
        }

        /// <summary>
        /// Unregisters a <see cref="ServiceBusPlugin"/>.
        /// </summary>
        /// <param name="serviceBusPluginName">The <see cref="ServiceBusPlugin.Name"/> of the plugin to be unregistered.</param>
        public override void UnregisterPlugin(string serviceBusPluginName)
        {
            ThrowIfClosed();
            if (RegisteredPlugins == null)
            {
                return;
            }
            if (serviceBusPluginName == null)
            {
                throw new ArgumentNullException(nameof(serviceBusPluginName), Resources.ArgumentNullOrWhiteSpace.FormatForUser(nameof(serviceBusPluginName)));
            }
            if (RegisteredPlugins.Any(p => p.Name == serviceBusPluginName))
            {
                var plugin = RegisteredPlugins.First(p => p.Name == serviceBusPluginName);
                RegisteredPlugins.Remove(plugin);
            }
        }

        internal async Task GetSessionReceiverLinkAsync(TimeSpan serverWaitTime)
        {
            var timeoutHelper = new TimeoutHelper(serverWaitTime, true);
            ReceivingAmqpLink receivingAmqpLink = await ReceiveLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);

            var source = (Source)receivingAmqpLink.Settings.Source;
            if (!source.FilterSet.TryGetValue<string>(AmqpClientConstants.SessionFilterName, out var tempSessionId))
            {
                receivingAmqpLink.Session.SafeClose();
                throw new ServiceBusException(true, Resources.SessionFilterMissing);
            }

            if (string.IsNullOrWhiteSpace(tempSessionId))
            {
                receivingAmqpLink.Session.SafeClose();
                throw new ServiceBusException(true, Resources.AmqpFieldSessionId);
            }

            receivingAmqpLink.Closed += OnSessionReceiverLinkClosed;
            SessionIdInternal = tempSessionId;
            LockedUntilUtcInternal = receivingAmqpLink.Settings.Properties.TryGetValue<long>(AmqpClientConstants.LockedUntilUtc, out var lockedUntilUtcTicks)
                ? new DateTime(lockedUntilUtcTicks, DateTimeKind.Utc)
                : DateTime.MinValue;
        }

        internal async Task<AmqpResponseMessage> ExecuteRequestResponseAsync(AmqpRequestMessage amqpRequestMessage)
        {
            AmqpMessage amqpMessage = amqpRequestMessage.AmqpMessage;
            if (isSessionReceiver)
            {
                ThrowIfSessionLockLost();
            }

            var timeoutHelper = new TimeoutHelper(OperationTimeout, true);
            if (!RequestResponseLinkManager.TryGetOpenedObject(out var requestResponseAmqpLink))
            {
                MessagingEventSource.Log.CreatingNewLink(ClientId, isSessionReceiver, SessionIdInternal, true, LinkException);
                requestResponseAmqpLink = await RequestResponseLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
            }

            AmqpMessage responseAmqpMessage = await Task.Factory.FromAsync(
                (c, s) => requestResponseAmqpLink.BeginRequest(amqpMessage, timeoutHelper.RemainingTime(), c, s),
                (a) => requestResponseAmqpLink.EndRequest(a),
                this).ConfigureAwait(false);

            return AmqpResponseMessage.CreateResponse(responseAmqpMessage);
        }

        protected override async Task OnClosingAsync()
        {
            clientLinkManager.Close();
            lock (messageReceivePumpSyncLock)
            {
                if (receivePump != null)
                {
                    receivePumpCancellationTokenSource.Cancel();
                    receivePumpCancellationTokenSource.Dispose();
                    receivePump = null;
                }
            }
            await ReceiveLinkManager.CloseAsync().ConfigureAwait(false);
            await RequestResponseLinkManager.CloseAsync().ConfigureAwait(false);

            if (ownsConnection)
            {
                await ServiceBusConnection.CloseAsync().ConfigureAwait(false);
            }
        }

        protected virtual async Task<IList<Message>> OnReceiveAsync(int maxMessageCount, TimeSpan serverWaitTime)
        {
            ReceivingAmqpLink receiveLink = null;

            if (isSessionReceiver)
            {
                ThrowIfSessionLockLost();
            }

            try
            {
                var timeoutHelper = new TimeoutHelper(serverWaitTime, true);
                if(!ReceiveLinkManager.TryGetOpenedObject(out receiveLink))
                {
                    MessagingEventSource.Log.CreatingNewLink(ClientId, isSessionReceiver, SessionIdInternal, false, LinkException);
                    receiveLink = await ReceiveLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
                }

                IEnumerable<AmqpMessage> amqpMessages = null;
                IList<Message> brokeredMessages = null;

                while (timeoutHelper.RemainingTime() > TimeSpan.Zero)
                {
                    bool hasMessages = await Task.Factory.FromAsync(
                    (c, s) => receiveLink.BeginReceiveRemoteMessages(maxMessageCount, DefaultBatchFlushInterval, timeoutHelper.RemainingTime(), c, s),
                    a => receiveLink.EndReceiveMessages(a, out amqpMessages),
                    this).ConfigureAwait(false);

                    Exception exception;
                    if ((exception = receiveLink.GetInnerException()) != null)
                    {
                        throw exception;
                    }

                    if (hasMessages && amqpMessages != null)
                    {
                        foreach (var amqpMessage in amqpMessages)
                        {
                            if (ReceiveMode == ReceiveMode.ReceiveAndDelete)
                            {
                                receiveLink.DisposeDelivery(amqpMessage, true, AmqpConstants.AcceptedOutcome);
                            }

                            Message message = AmqpMessageConverter.AmqpMessageToSBMessage(amqpMessage);

                            if (ReceiveMode == ReceiveMode.PeekLock &&
                               message.SystemProperties.LockedUntilUtc <= DateTime.UtcNow)
                            {
                                receiveLink.ReleaseMessage(amqpMessage);
                                continue;
                            }
                            if (brokeredMessages == null)
                            {
                                brokeredMessages = new List<Message>();
                            }

                            brokeredMessages.Add(message);
                        }

                        if(brokeredMessages != null)
                        {
                            break;
                        }
                    }
                }

                return brokeredMessages;
            }
            catch (Exception exception)
            {
                throw AmqpExceptionHelper.GetClientException(exception, receiveLink?.GetTrackingId(), null, receiveLink?.Session.IsClosing() ?? false);
            }
        }

        protected virtual async Task<IList<Message>> OnPeekAsync(long fromSequenceNumber, int messageCount = 1)
        {
            try
            {
                AmqpRequestMessage requestMessage =
                    AmqpRequestMessage.CreateRequest(
                        ManagementConstants.Operations.PeekMessageOperation,
                        OperationTimeout,
                        null);

                requestMessage.Map[ManagementConstants.Properties.FromSequenceNumber] = fromSequenceNumber;
                requestMessage.Map[ManagementConstants.Properties.MessageCount] = messageCount;

                if (!string.IsNullOrWhiteSpace(SessionIdInternal))
                {
                    requestMessage.Map[ManagementConstants.Properties.SessionId] = SessionIdInternal;
                }

                var messages = new List<Message>();

                AmqpResponseMessage response = await ExecuteRequestResponseAsync(requestMessage).ConfigureAwait(false);
                if (response.StatusCode == AmqpResponseStatusCode.OK)
                {
                    Message message = null;
                    var messageList = response.GetListValue<AmqpMap>(ManagementConstants.Properties.Messages);
                    foreach (AmqpMap entry in messageList)
                    {
                        var payload = (ArraySegment<byte>)entry[ManagementConstants.Properties.Message];
                        AmqpMessage amqpMessage =
                            AmqpMessage.CreateAmqpStreamMessage(new BufferListStream(new[] { payload }), true);
                        message = AmqpMessageConverter.AmqpMessageToSBMessage(amqpMessage);
                        messages.Add(message);
                    }

                    if (message != null)
                    {
                        LastPeekedSequenceNumber = message.SystemProperties.SequenceNumber;
                    }

                    return messages;
                }

                if (response.StatusCode == AmqpResponseStatusCode.NoContent ||
                    (response.StatusCode == AmqpResponseStatusCode.NotFound && Equals(AmqpClientConstants.MessageNotFoundError, response.GetResponseErrorCondition())))
                {
                    return messages;
                }

                throw response.ToMessagingContractException();
            }
            catch (Exception exception)
            {
                throw AmqpExceptionHelper.GetClientException(exception);
            }
        }

        protected virtual async Task<IList<Message>> OnReceiveDeferredMessageAsync(IEnumerable<long> sequenceNumbers)
        {
            var messages = new List<Message>();
            try
            {
                AmqpRequestMessage requestMessage = AmqpRequestMessage.CreateRequest(ManagementConstants.Operations.ReceiveBySequenceNumberOperation, OperationTimeout, null);
                requestMessage.Map[ManagementConstants.Properties.SequenceNumbers] = sequenceNumbers.ToArray();
                requestMessage.Map[ManagementConstants.Properties.ReceiverSettleMode] = (uint)(ReceiveMode == ReceiveMode.ReceiveAndDelete ? 0 : 1);

                AmqpResponseMessage response = await ExecuteRequestResponseAsync(requestMessage).ConfigureAwait(false);

                if (response.StatusCode == AmqpResponseStatusCode.OK)
                {
                    IEnumerable<AmqpMap> amqpMapList = response.GetListValue<AmqpMap>(ManagementConstants.Properties.Messages);
                    foreach (AmqpMap entry in amqpMapList)
                    {
                        var payload = (ArraySegment<byte>)entry[ManagementConstants.Properties.Message];
                        AmqpMessage amqpMessage = AmqpMessage.CreateAmqpStreamMessage(new BufferListStream(new[] { payload }), true);
                        Message message = AmqpMessageConverter.AmqpMessageToSBMessage(amqpMessage);
                        if (entry.TryGetValue(ManagementConstants.Properties.LockToken, out Guid lockToken))
                        {
                            message.SystemProperties.LockTokenGuid = lockToken;
                            requestResponseLockedMessages.AddOrUpdate(lockToken, message.SystemProperties.LockedUntilUtc);
                        }

                        messages.Add(message);
                    }
                }
                else
                {
                    throw response.ToMessagingContractException();
                }
            }
            catch (Exception exception)
            {
                throw AmqpExceptionHelper.GetClientException(exception);
            }

            return messages;
        }

        protected virtual async Task OnCompleteAsync(IEnumerable<string> lockTokens)
        {
            var lockTokenGuids = lockTokens.Select(lt => new Guid(lt));
            if (lockTokenGuids.Any(lt => requestResponseLockedMessages.Contains(lt)))
            {
                await DisposeMessageRequestResponseAsync(lockTokenGuids, DispositionStatus.Completed).ConfigureAwait(false);
            }
            else
            {
                await DisposeMessagesAsync(lockTokenGuids, AmqpConstants.AcceptedOutcome).ConfigureAwait(false);
            }
        }

        protected virtual async Task OnAbandonAsync(string lockToken)
        {
            IEnumerable<Guid> lockTokens = new[] { new Guid(lockToken) };
            if (lockTokens.Any(lt => requestResponseLockedMessages.Contains(lt)))
            {
                await DisposeMessageRequestResponseAsync(lockTokens, DispositionStatus.Abandoned).ConfigureAwait(false);
            }
            else
            {
                await DisposeMessagesAsync(lockTokens, new Modified()).ConfigureAwait(false);
            }
        }

        protected virtual async Task OnDeferAsync(string lockToken)
        {
            IEnumerable<Guid> lockTokens = new[] { new Guid(lockToken) };
            if (lockTokens.Any(lt => requestResponseLockedMessages.Contains(lt)))
            {
                await DisposeMessageRequestResponseAsync(lockTokens, DispositionStatus.Deferred).ConfigureAwait(false);
            }
            else
            {
                await DisposeMessagesAsync(lockTokens, new Modified { UndeliverableHere = true }).ConfigureAwait(false);
            }
        }

        protected virtual async Task OnDeadLetterAsync(string lockToken)
        {
            IEnumerable<Guid> lockTokens = new[] { new Guid(lockToken) };
            if (lockTokens.Any(lt => requestResponseLockedMessages.Contains(lt)))
            {
                await DisposeMessageRequestResponseAsync(lockTokens, DispositionStatus.Suspended).ConfigureAwait(false);
            }
            else
            {
                await DisposeMessagesAsync(lockTokens, AmqpConstants.RejectedOutcome).ConfigureAwait(false);
            }
        }

        protected virtual async Task<DateTime> OnRenewLockAsync(string lockToken)
        {
            var lockedUntilUtc = DateTime.MinValue;
            try
            {
                // Create an AmqpRequest Message to renew  lock
                var requestMessage = AmqpRequestMessage.CreateRequest(ManagementConstants.Operations.RenewLockOperation, OperationTimeout, null);
                requestMessage.Map[ManagementConstants.Properties.LockTokens] = new[] { new Guid(lockToken) };

                AmqpResponseMessage response = await ExecuteRequestResponseAsync(requestMessage).ConfigureAwait(false);

                if (response.StatusCode == AmqpResponseStatusCode.OK)
                {
                    IEnumerable<DateTime> lockedUntilUtcTimes = response.GetValue<IEnumerable<DateTime>>(ManagementConstants.Properties.Expirations);
                    lockedUntilUtc = lockedUntilUtcTimes.First();
                }
                else
                {
                    throw response.ToMessagingContractException();
                }
            }
            catch (Exception exception)
            {
                throw AmqpExceptionHelper.GetClientException(exception);
            }

            return lockedUntilUtc;
        }

        /// <summary> </summary>
        protected virtual void OnMessageHandler(
            MessageHandlerOptions registerHandlerOptions,
            Func<Message, CancellationToken, Task> callback)
        {
            MessagingEventSource.Log.RegisterOnMessageHandlerStart(ClientId, registerHandlerOptions);

            lock (messageReceivePumpSyncLock)
            {
                if (receivePump != null)
                {
                    throw new InvalidOperationException(Resources.MessageHandlerAlreadyRegistered);
                }

                receivePumpCancellationTokenSource = new CancellationTokenSource();
                receivePump = new MessageReceivePump(this, registerHandlerOptions, callback, ServiceBusConnection.Endpoint.Authority, receivePumpCancellationTokenSource.Token);
            }

            try
            {
                receivePump.StartPump();
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.RegisterOnMessageHandlerException(ClientId, exception);
                lock (messageReceivePumpSyncLock)
                {
                    if (receivePump != null)
                    {
                        receivePumpCancellationTokenSource.Cancel();
                        receivePumpCancellationTokenSource.Dispose();
                        receivePump = null;
                    }
                }

                throw;
            }

            MessagingEventSource.Log.RegisterOnMessageHandlerStop(ClientId);
        }

        static int ValidateLockTokens(IEnumerable<string> lockTokens)
        {
            int count;
            if (lockTokens == null || (count = lockTokens.Count()) == 0)
            {
                throw Fx.Exception.ArgumentNull(nameof(lockTokens));
            }

            return count;
        }

        static int ValidateSequenceNumbers(IEnumerable<long> sequenceNumbers)
        {
            int count;
            if (sequenceNumbers == null || (count = sequenceNumbers.Count()) == 0)
            {
                throw Fx.Exception.ArgumentNull(nameof(sequenceNumbers));
            }

            return count;
        }

        static void CloseSession(ReceivingAmqpLink link)
        {
            link.Session.SafeClose();
        }

        static void CloseRequestResponseSession(RequestResponseAmqpLink requestResponseAmqpLink)
        {
            requestResponseAmqpLink.Session.SafeClose();
        }

        async Task<Message> ProcessMessage(Message message)
        {
            var processedMessage = message;
            foreach (var plugin in RegisteredPlugins)
            {
                try
                {
                    MessagingEventSource.Log.PluginCallStarted(plugin.Name, message.MessageId);
                    processedMessage = await plugin.AfterMessageReceive(message).ConfigureAwait(false);
                    MessagingEventSource.Log.PluginCallCompleted(plugin.Name, message.MessageId);
                }
                catch (Exception ex)
                {
                    MessagingEventSource.Log.PluginCallFailed(plugin.Name, message.MessageId, ex);
                    if (!plugin.ShouldContinueOnException)
                    {
                        throw;
                    }
                }
            }
            return processedMessage;
        }

        async Task<IList<Message>> ProcessMessages(IList<Message> messageList)
        {
            if (RegisteredPlugins.Count < 1)
            {
                return messageList;
            }

            var processedMessageList = new List<Message>();
            foreach (var message in messageList)
            {
                var processedMessage = await ProcessMessage(message).ConfigureAwait(false);
                processedMessageList.Add(processedMessage);
            }

            return processedMessageList;
        }

        async Task DisposeMessagesAsync(IEnumerable<Guid> lockTokens, Outcome outcome)
        {
            if(isSessionReceiver)
            {
                ThrowIfSessionLockLost();
            }

            var timeoutHelper = new TimeoutHelper(OperationTimeout, true);
            List<ArraySegment<byte>> deliveryTags = ConvertLockTokensToDeliveryTags(lockTokens);

            ReceivingAmqpLink receiveLink = null;
            try
            {
                if (!ReceiveLinkManager.TryGetOpenedObject(out receiveLink))
                {
                    MessagingEventSource.Log.CreatingNewLink(ClientId, isSessionReceiver, SessionIdInternal, false, LinkException);
                    receiveLink = await ReceiveLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
                }

                var disposeMessageTasks = new Task<Outcome>[deliveryTags.Count];
                var i = 0;
                foreach (ArraySegment<byte> deliveryTag in deliveryTags)
                {
                    disposeMessageTasks[i++] = Task.Factory.FromAsync(
                        (c, s) => receiveLink.BeginDisposeMessage(deliveryTag, outcome, true, timeoutHelper.RemainingTime(), c, s),
                        a => receiveLink.EndDisposeMessage(a),
                        this);
                }

                Outcome[] outcomes = await Task.WhenAll(disposeMessageTasks).ConfigureAwait(false);
                Error error = null;
                foreach (Outcome item in outcomes)
                {
                    var disposedOutcome = item.DescriptorCode == Rejected.Code && ((error = ((Rejected)item).Error) != null) ? item : null;
                    if (disposedOutcome != null)
                    {
                        if (error.Condition.Equals(AmqpErrorCode.NotFound))
                        {
                            if (isSessionReceiver)
                            {
                                throw new SessionLockLostException(Resources.SessionLockExpiredOnMessageSession);
                            }

                            throw new MessageLockLostException(Resources.MessageLockLost);
                        }

                        throw error.ToMessagingContractException();
                    }
                }
            }
            catch (Exception exception)
            {
                if (exception is OperationCanceledException &&
                    receiveLink != null && receiveLink.State != AmqpObjectState.Opened)
                {
                    // The link state is lost, We need to return a non-retriable error.
                    MessagingEventSource.Log.LinkStateLost(ClientId, receiveLink.Name, receiveLink.State, isSessionReceiver, exception);
                    if (isSessionReceiver)
                    {
                        throw new SessionLockLostException(Resources.SessionLockExpiredOnMessageSession);
                    }

                    throw new MessageLockLostException(Resources.MessageLockLost);
                }

                throw AmqpExceptionHelper.GetClientException(exception);
            }
        }

        async Task DisposeMessageRequestResponseAsync(IEnumerable<Guid> lockTokens, DispositionStatus dispositionStatus)
        {
            try
            {
                // Create an AmqpRequest Message to update disposition
                AmqpRequestMessage requestMessage = AmqpRequestMessage.CreateRequest(ManagementConstants.Operations.UpdateDispositionOperation, OperationTimeout, null);
                requestMessage.Map[ManagementConstants.Properties.LockTokens] = lockTokens.ToArray();
                requestMessage.Map[ManagementConstants.Properties.DispositionStatus] = dispositionStatus.ToString().ToLowerInvariant();

                AmqpResponseMessage amqpResponseMessage = await ExecuteRequestResponseAsync(requestMessage).ConfigureAwait(false);
                if (amqpResponseMessage.StatusCode != AmqpResponseStatusCode.OK)
                {
                    throw amqpResponseMessage.ToMessagingContractException();
                }
            }
            catch (Exception exception)
            {
                throw AmqpExceptionHelper.GetClientException(exception);
            }
        }

        async Task<ReceivingAmqpLink> CreateLinkAsync(TimeSpan timeout)
        {
            FilterSet filterMap = null;

            MessagingEventSource.Log.AmqpReceiveLinkCreateStart(ClientId, false, EntityType, Path);

            if (isSessionReceiver)
            {
                filterMap = new FilterSet { { AmqpClientConstants.SessionFilterName, SessionIdInternal } };
            }

            var linkSettings = new AmqpLinkSettings
            {
                Role = true,
                TotalLinkCredit = (uint)PrefetchCount,
                AutoSendFlow = PrefetchCount > 0,
                Source = new Source { Address = Path, FilterSet = filterMap },
                SettleType = (ReceiveMode == ReceiveMode.PeekLock) ? SettleMode.SettleOnDispose : SettleMode.SettleOnSend
            };

            if (EntityType != null)
            {
                linkSettings.AddProperty(AmqpClientConstants.EntityTypeName, (int)EntityType);
            }

            linkSettings.AddProperty(AmqpClientConstants.TimeoutName, (uint)timeout.TotalMilliseconds);

            var endPointAddress = new Uri(ServiceBusConnection.Endpoint, Path);
            var claims = new[] { ClaimConstants.Listen };
            var sendReceiveLinkCreator = new AmqpSendReceiveLinkCreator(Path, ServiceBusConnection, endPointAddress, claims, CbsTokenProvider, linkSettings, ClientId);
            Tuple<AmqpObject, DateTime> linkDetails = await sendReceiveLinkCreator.CreateAndOpenAmqpLinkAsync().ConfigureAwait(false);

            var receivingAmqpLink = (ReceivingAmqpLink) linkDetails.Item1;
            var activeSendReceiveClientLink = new ActiveSendReceiveClientLink(
                receivingAmqpLink,
                endPointAddress,
                endPointAddress.AbsoluteUri,
                claims,
                linkDetails.Item2);

            clientLinkManager.SetActiveSendReceiveLink(activeSendReceiveClientLink);

            MessagingEventSource.Log.AmqpReceiveLinkCreateStop(ClientId);

            return receivingAmqpLink;
        }

        // TODO: Consolidate the link creation paths
        async Task<RequestResponseAmqpLink> CreateRequestResponseLinkAsync(TimeSpan timeout)
        {
            var entityPath = Path + '/' + AmqpClientConstants.ManagementAddress;

            MessagingEventSource.Log.AmqpReceiveLinkCreateStart(ClientId, true, EntityType, entityPath);
            var linkSettings = new AmqpLinkSettings();
            linkSettings.AddProperty(AmqpClientConstants.EntityTypeName, AmqpClientConstants.EntityTypeManagement);

            var endPointAddress = new Uri(ServiceBusConnection.Endpoint, entityPath);
            string[] claims = { ClaimConstants.Manage, ClaimConstants.Listen };
            var requestResponseLinkCreator = new AmqpRequestResponseLinkCreator(entityPath, ServiceBusConnection, endPointAddress, claims, CbsTokenProvider, linkSettings, ClientId);
            Tuple<AmqpObject, DateTime> linkDetails = await requestResponseLinkCreator.CreateAndOpenAmqpLinkAsync().ConfigureAwait(false);

            var requestResponseAmqpLink = (RequestResponseAmqpLink)linkDetails.Item1;
            var activeRequestResponseClientLink = new ActiveRequestResponseLink(
                requestResponseAmqpLink,
                endPointAddress,
                endPointAddress.AbsoluteUri,
                claims,
                linkDetails.Item2);
            clientLinkManager.SetActiveRequestResponseLink(activeRequestResponseClientLink);

            MessagingEventSource.Log.AmqpReceiveLinkCreateStop(ClientId);
            return requestResponseAmqpLink;
        }

        void OnSessionReceiverLinkClosed(object sender, EventArgs e)
        {
            var amqpLink = (ReceivingAmqpLink)sender;
            if (amqpLink != null)
            {
                var exception = amqpLink.GetInnerException();
                if (!(exception is SessionLockLostException))
                {
                    exception = new SessionLockLostException("Session lock lost. Accept a new session", exception);
                }

                LinkException = exception;
                MessagingEventSource.Log.SessionReceiverLinkClosed(ClientId, SessionIdInternal, LinkException);
            }
        }

        List<ArraySegment<byte>> ConvertLockTokensToDeliveryTags(IEnumerable<Guid> lockTokens)
        {
            return lockTokens.Select(lockToken => new ArraySegment<byte>(lockToken.ToByteArray())).ToList();
        }

        void ThrowIfNotPeekLockMode()
        {
            if (ReceiveMode != ReceiveMode.PeekLock)
            {
                throw Fx.Exception.AsError(new InvalidOperationException("The operation is only supported in 'PeekLock' receive mode."));
            }
        }

        void ThrowIfSessionLockLost()
        {
            if (LinkException != null)
            {
                throw LinkException;
            }
        }
    }
}