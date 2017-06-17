// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Encoding;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Azure.ServiceBus.Amqp;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus.Core
{
    /// <summary>
    ///     The MessageReceiver can be used to receive messages from Queues and Subscriptions.
    /// </summary>
    /// <remarks>
    ///     The MessageReceiver provides advanced functionality that is not found in the
    ///     <see cref="QueueClient" /> or <see cref="SubscriptionClient" />. For instance,
    ///     <see cref="ReceiveAsync()" />, which allows you to receive messages on demand, but also requires
    ///     you to manually renew locks using <see cref="RenewLockAsync(string)" />.
    /// </remarks>
    public class MessageReceiver : ClientEntity, IMessageReceiver
    {
        const int DefaultPrefetchCount = 0;
        static readonly TimeSpan DefaultBatchFlushInterval = TimeSpan.FromMilliseconds(20);
        readonly bool isSessionReceiver;
        readonly object messageReceivePumpSyncLock;
        readonly bool ownsConnection;

        readonly ConcurrentExpiringSet<Guid> requestResponseLockedMessages;
        long lastPeekedSequenceNumber;

        int prefetchCount;
        MessageReceivePump receivePump;
        CancellationTokenSource receivePumpCancellationTokenSource;

        /// <summary>
        ///     Creates a new MessageReceiver from a <see cref="ServiceBusConnectionStringBuilder" />.
        /// </summary>
        /// <param name="connectionStringBuilder">
        ///     The <see cref="ServiceBusConnectionStringBuilder" /> used for the connection
        ///     details.
        /// </param>
        /// <param name="receiveMode">The <see cref="ServiceBus.ReceiveMode" /> used to specify how messages are received.</param>
        /// <param name="retryPolicy">The <see cref="RetryPolicy" /> that will be used when communicating with Service Bus</param>
        /// <param name="prefetchCount">
        ///     The <see cref="PrefetchCount" /> that specifies the upper limit of messages this receiver
        ///     will actively receive regardless of whether a receive operation is pending.
        /// </param>
        public MessageReceiver(
            ServiceBusConnectionStringBuilder connectionStringBuilder,
            ReceiveMode receiveMode = ReceiveMode.PeekLock,
            RetryPolicy retryPolicy = null,
            int prefetchCount = DefaultPrefetchCount)
            : this(connectionStringBuilder?.GetNamespaceConnectionString(), connectionStringBuilder.EntityPath, receiveMode, retryPolicy, prefetchCount)
        {
        }

        /// <summary>
        ///     Creates a new MessageReceiver from a specified connection string and entity path.
        /// </summary>
        /// <param name="connectionString">The connection string used to communicate with Service Bus.</param>
        /// <param name="entityPath">
        ///     The path of the entity for this receiver. For Queues this will be the name, but for
        ///     Subscriptions this will be the path. You can use
        ///     <see cref="EntityNameHelper.FormatSubscriptionPath(string, string)" />, to help create this path.
        /// </param>
        /// <param name="receiveMode">The <see cref="ServiceBus.ReceiveMode" /> used to specify how messages are received.</param>
        /// <param name="retryPolicy">The <see cref="RetryPolicy" /> that will be used when communicating with Service Bus</param>
        /// <param name="prefetchCount">
        ///     The <see cref="PrefetchCount" /> that specifies the upper limit of messages this receiver
        ///     will actively receive regardless of whether a receive operation is pending.
        /// </param>
        public MessageReceiver(
            string connectionString,
            string entityPath,
            ReceiveMode receiveMode = ReceiveMode.PeekLock,
            RetryPolicy retryPolicy = null,
            int prefetchCount = DefaultPrefetchCount)
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
            int prefetchCount = DefaultPrefetchCount,
            string sessionId = null,
            bool isSessionReceiver = false)
            : base(nameof(MessageReceiver) + StringUtility.GetRandomString(), retryPolicy ?? RetryPolicy.Default)
        {
            ReceiveMode = receiveMode;
            OperationTimeout = serviceBusConnection.OperationTimeout;
            Path = entityPath;
            EntityType = entityType;
            ServiceBusConnection = serviceBusConnection;
            CbsTokenProvider = cbsTokenProvider;
            SessionId = sessionId;
            this.isSessionReceiver = isSessionReceiver;
            ReceiveLinkManager = new FaultTolerantAmqpObject<ReceivingAmqpLink>(CreateLinkAsync, CloseSession);
            RequestResponseLinkManager = new FaultTolerantAmqpObject<RequestResponseAmqpLink>(CreateRequestResponseLinkAsync, CloseRequestResponseSession);
            requestResponseLockedMessages = new ConcurrentExpiringSet<Guid>();
            PrefetchCount = prefetchCount;
            messageReceivePumpSyncLock = new object();
        }

        /// <summary>
        ///     This constructor should only be used by inherited members, such as the <see cref="IMessageSession" />.
        /// </summary>
        /// <param name="receiveMode">The <see cref="ServiceBus.ReceiveMode" /> used to specify how messages are received.</param>
        /// <param name="operationTimeout">The default operation timeout to be used.</param>
        /// <param name="retryPolicy">The <see cref="RetryPolicy" /> to be used.</param>
        protected MessageReceiver(ReceiveMode receiveMode, TimeSpan operationTimeout, RetryPolicy retryPolicy)
            : base(nameof(MessageReceiver) + StringUtility.GetRandomString(), retryPolicy ?? RetryPolicy.Default)
        {
            ReceiveMode = receiveMode;
            OperationTimeout = operationTimeout;
            lastPeekedSequenceNumber = Constants.DefaultLastPeekedSequenceNumber;
            messageReceivePumpSyncLock = new object();
        }

        /// <summary>
        ///     Gets a list of currently registered plugins.
        /// </summary>
        public IList<ServiceBusPlugin> RegisteredPlugins { get; } = new List<ServiceBusPlugin>();

        /// <summary>
        ///     Gets the DateTime that the current receiver is locked until. This is only applicable when Sessions are used.
        /// </summary>
        public DateTime LockedUntilUtc { get; protected set; }

        /// <summary>
        ///     Gets the SessionId of the current receiver. This is only applicable when Sessions are used.
        /// </summary>
        public string SessionId { get; protected set; }

        internal TimeSpan OperationTimeout { get; }

        internal MessagingEntityType? EntityType { get; }

        ServiceBusConnection ServiceBusConnection { get; }

        ICbsTokenProvider CbsTokenProvider { get; }

        FaultTolerantAmqpObject<ReceivingAmqpLink> ReceiveLinkManager { get; }

        FaultTolerantAmqpObject<RequestResponseAmqpLink> RequestResponseLinkManager { get; }

        /// <summary>
        ///     Gets the <see cref="ServiceBus.ReceiveMode" /> of the current receiver.
        /// </summary>
        public ReceiveMode ReceiveMode { get; protected set; }

        /// <summary>Gets or sets the number of messages that the message receiver can simultaneously request.</summary>
        /// <value>The number of messages that the message receiver can simultaneously request.</value>
        /// <remarks> Takes effect on the next receive call to the server. </remarks>
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
                    link.SetTotalLinkCredit((uint) value, true, true);
                }
            }
        }

        /// <summary>Gets the sequence number of the last peeked message.</summary>
        /// <value>The sequence number of the last peeked message.</value>
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

        /// <summary>
        ///     The path of the entity for this receiver. For Queues this will be the name, but for Subscriptions this will be
        ///     the path.
        /// </summary>
        public virtual string Path { get; }

        /// <summary>
        ///     Asynchronously receives a message using the <see cref="MessageReceiver" />.
        /// </summary>
        /// <returns>The asynchronous operation.</returns>
        public Task<Message> ReceiveAsync()
        {
            return ReceiveAsync(OperationTimeout);
        }

        /// <summary>
        ///     Asynchronously receives a message. />.
        /// </summary>
        /// <param name="serverWaitTime">The time span the server waits for receiving a message before it times out.</param>
        /// <returns>The asynchronous operation.</returns>
        public async Task<Message> ReceiveAsync(TimeSpan serverWaitTime)
        {
            var messages = await ReceiveAsync(1, serverWaitTime).ConfigureAwait(false);
            if (messages != null && messages.Count > 0)
            {
                return messages[0];
            }

            return null;
        }

        /// <summary>
        ///     Asynchronously receives a message using the <see cref="MessageReceiver" />.
        /// </summary>
        /// <param name="maxMessageCount">The maximum number of messages that will be received.</param>
        /// <returns>The asynchronous operation.</returns>
        public Task<IList<Message>> ReceiveAsync(int maxMessageCount)
        {
            return ReceiveAsync(maxMessageCount, OperationTimeout);
        }

        /// <summary>
        ///     Asynchronously receives a message. />.
        /// </summary>
        /// <param name="maxMessageCount">The maximum number of messages that will be received.</param>
        /// <param name="serverWaitTime">The time span the server waits for receiving a message before it times out.</param>
        /// <returns>The asynchronous operation.</returns>
        public async Task<IList<Message>> ReceiveAsync(int maxMessageCount, TimeSpan serverWaitTime)
        {
            MessagingEventSource.Log.MessageReceiveStart(ClientId, maxMessageCount);

            IList<Message> unprocessedMessageList = null;
            try
            {
                await RetryPolicy.RunOperation(
                        async () => { unprocessedMessageList = await OnReceiveAsync(maxMessageCount, serverWaitTime).ConfigureAwait(false); }, serverWaitTime)
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
        ///     Receives a message using the <see cref="MessageReceiver" />.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number of the message that will be received.</param>
        /// <returns>The asynchronous operation.</returns>
        public async Task<Message> ReceiveBySequenceNumberAsync(long sequenceNumber)
        {
            var messages = await ReceiveBySequenceNumberAsync(new[] {sequenceNumber});
            if (messages != null && messages.Count > 0)
            {
                return messages[0];
            }

            return null;
        }

        /// <summary>
        ///     Receives an <see cref="IList{Message}" /> of messages using the <see cref="MessageReceiver" />.
        /// </summary>
        /// <param name="sequenceNumbers">An <see cref="IEnumerable{T}" /> containing the sequence numbers to receive.</param>
        /// <returns>The asynchronous operation.</returns>
        public async Task<IList<Message>> ReceiveBySequenceNumberAsync(IEnumerable<long> sequenceNumbers)
        {
            ThrowIfNotPeekLockMode();
            var count = ValidateSequenceNumbers(sequenceNumbers);

            MessagingEventSource.Log.MessageReceiveBySequenceNumberStart(ClientId, count, sequenceNumbers);

            IList<Message> messages = null;
            try
            {
                await RetryPolicy.RunOperation(
                        async () => { messages = await OnReceiveBySequenceNumberAsync(sequenceNumbers).ConfigureAwait(false); }, OperationTimeout)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageReceiveBySequenceNumberException(ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageReceiveBySequenceNumberStop(ClientId, messages?.Count ?? 0);

            return messages;
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
            return CompleteAsync(new[] {lockToken});
        }

        /// <summary>
        ///     Completes a series of <see cref="Message" /> using a list of lock tokens.
        /// </summary>
        /// <remarks>
        ///     A lock token can be found in <see cref="Message.SystemPropertiesCollection.LockToken" />, only when
        ///     <see cref="ReceiveMode" /> is set to <see cref="ReceiveMode.PeekLock" />.
        /// </remarks>
        /// <param name="lockTokens">
        ///     An <see cref="IEnumerable{T}" /> containing the lock tokens of the corresponding messages to
        ///     complete.
        /// </param>
        /// <returns>The asynchronous operation.</returns>
        public async Task CompleteAsync(IEnumerable<string> lockTokens)
        {
            ThrowIfNotPeekLockMode();
            var count = ValidateLockTokens(lockTokens);

            MessagingEventSource.Log.MessageCompleteStart(ClientId, count, lockTokens);

            try
            {
                await RetryPolicy.RunOperation(
                        async () => { await OnCompleteAsync(lockTokens).ConfigureAwait(false); }, OperationTimeout)
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
        ///     Abandons a <see cref="Message" /> using a lock token. This will make the message available again for processing.
        /// </summary>
        /// <param name="lockToken">The lock token of the corresponding message to abandon.</param>
        /// <remarks>
        ///     A lock token can be found in <see cref="Message.SystemPropertiesCollection.LockToken" />, only when
        ///     <see cref="ReceiveMode" /> is set to <see cref="ReceiveMode.PeekLock" />.
        /// </remarks>
        /// <returns>The asynchronous operation.</returns>
        public async Task AbandonAsync(string lockToken)
        {
            ThrowIfNotPeekLockMode();

            MessagingEventSource.Log.MessageAbandonStart(ClientId, 1, lockToken);
            try
            {
                await RetryPolicy.RunOperation(
                        async () => { await OnAbandonAsync(lockToken).ConfigureAwait(false); }, OperationTimeout)
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
        ///     A lock token can be found in <see cref="Message.SystemPropertiesCollection.LockToken" />, only when
        ///     <see cref="ReceiveMode" /> is set to <see cref="ReceiveMode.PeekLock" />.
        ///     In order to receive this message again in the future, you will need to use
        ///     <see cref="Message.SystemPropertiesCollection.SequenceNumber" />.
        /// </remarks>
        /// <returns>The asynchronous operation.</returns>
        public async Task DeferAsync(string lockToken)
        {
            ThrowIfNotPeekLockMode();

            MessagingEventSource.Log.MessageDeferStart(ClientId, 1, lockToken);

            try
            {
                await RetryPolicy.RunOperation(
                        async () => { await OnDeferAsync(lockToken).ConfigureAwait(false); }, OperationTimeout)
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
        public async Task DeadLetterAsync(string lockToken)
        {
            ThrowIfNotPeekLockMode();

            MessagingEventSource.Log.MessageDeadLetterStart(ClientId, 1, lockToken);

            try
            {
                await RetryPolicy.RunOperation(
                        async () => { await OnDeadLetterAsync(lockToken).ConfigureAwait(false); }, OperationTimeout)
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
        ///     Renews the lock on the message specified by the lock token. The lock will be renewed based on the setting specified
        ///     on the queue.
        /// </summary>
        /// <param name="lockToken">The lock token of the <see cref="Message" />.</param>
        /// <remarks>
        ///     A lock token can be found in <see cref="Message.SystemProperties" />, only when <see cref="ReceiveMode" /> is
        ///     set to <see cref="ReceiveMode.PeekLock" />.
        /// </remarks>
        /// <returns>The asynchronous operation.</returns>
        public async Task<DateTime> RenewLockAsync(string lockToken)
        {
            ThrowIfNotPeekLockMode();

            MessagingEventSource.Log.MessageRenewLockStart(ClientId, 1, lockToken);

            var lockedUntilUtc = DateTime.Now;
            try
            {
                await RetryPolicy.RunOperation(
                        async () => { lockedUntilUtc = await OnRenewLockAsync(lockToken).ConfigureAwait(false); }, OperationTimeout)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageRenewLockException(ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageRenewLockStop(ClientId);
            return lockedUntilUtc;
        }

        /// <summary>
        ///     Asynchronously reads the next message without changing the state of the receiver or the message source.
        /// </summary>
        /// <returns>
        ///     The asynchronous operation that returns the <see cref="Message" /> that represents the next message to be
        ///     read.
        /// </returns>
        public Task<Message> PeekAsync()
        {
            return PeekBySequenceNumberAsync(lastPeekedSequenceNumber + 1);
        }

        /// <summary>
        ///     Asynchronously reads the next batch of message without changing the state of the receiver or the message source.
        /// </summary>
        /// <param name="maxMessageCount">The number of messages.</param>
        /// <returns>The asynchronous operation that returns a list of <see cref="Message" /> to be read.</returns>
        public Task<IList<Message>> PeekAsync(int maxMessageCount)
        {
            return PeekBySequenceNumberAsync(lastPeekedSequenceNumber + 1, maxMessageCount);
        }

        /// <summary>
        ///     Asynchronously reads the next message without changing the state of the receiver or the message source.
        /// </summary>
        /// <param name="fromSequenceNumber">The sequence number from where to read the message.</param>
        /// <returns>
        ///     The asynchronous operation that returns the <see cref="Message" /> that represents the next message to be
        ///     read.
        /// </returns>
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
            IList<Message> messages = null;

            MessagingEventSource.Log.MessagePeekStart(ClientId, fromSequenceNumber, messageCount);
            try
            {
                await RetryPolicy.RunOperation(
                        async () => { messages = await OnPeekAsync(fromSequenceNumber, messageCount).ConfigureAwait(false); }, OperationTimeout)
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
        ///     Registers a message handler and begins a new thread to receive messages.
        /// </summary>
        /// <param name="handler">A <see cref="Func{T1, T2, TResult}" /> that processes messages.</param>
        public void RegisterMessageHandler(Func<Message, CancellationToken, Task> handler)
        {
            RegisterMessageHandler(handler, new MessageHandlerOptions());
        }

        /// <summary>
        ///     Registers a message handler and begins a new thread to receive messages.
        /// </summary>
        /// <param name="handler">A <see cref="Func{T1, T2, TResult}" /> that processes messages.</param>
        /// <param name="messageHandlerOptions">
        ///     The <see cref="MessageHandlerOptions" /> options used to register a message
        ///     handler.
        /// </param>
        public void RegisterMessageHandler(Func<Message, CancellationToken, Task> handler, MessageHandlerOptions messageHandlerOptions)
        {
            messageHandlerOptions.MessageClientEntity = this;
            OnMessageHandlerAsync(messageHandlerOptions, handler).GetAwaiter().GetResult();
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
                    MessagingEventSource.Log.PluginCallFailed(plugin.Name, message.MessageId, ex.Message);
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

        /// <summary></summary>
        /// <returns>The asynchronous operation.</returns>
        protected override async Task OnClosingAsync()
        {
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

        internal async Task GetSessionReceiverLinkAsync(TimeSpan serverWaitTime)
        {
            var timeoutHelper = new TimeoutHelper(serverWaitTime, true);
            var receivingAmqpLink = await ReceiveLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
            var source = (Source) receivingAmqpLink.Settings.Source;
            string tempSessionId;
            if (!source.FilterSet.TryGetValue(AmqpClientConstants.SessionFilterName, out tempSessionId))
            {
                receivingAmqpLink.Session.SafeClose();
                throw new ServiceBusException(false, Resources.AmqpFieldSessionId);
            }
            if (!string.IsNullOrWhiteSpace(tempSessionId))
            {
                SessionId = tempSessionId;
            }
            long lockedUntilUtcTicks;
            LockedUntilUtc = receivingAmqpLink.Settings.Properties.TryGetValue(AmqpClientConstants.LockedUntilUtc, out lockedUntilUtcTicks) ? new DateTime(lockedUntilUtcTicks, DateTimeKind.Utc) : DateTime.MinValue;
        }

        internal async Task<AmqpResponseMessage> ExecuteRequestResponseAsync(AmqpRequestMessage amqpRequestMessage)
        {
            var amqpMessage = amqpRequestMessage.AmqpMessage;
            var timeoutHelper = new TimeoutHelper(OperationTimeout, true);
            var requestResponseAmqpLink = await RequestResponseLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);

            var responseAmqpMessage = await Task.Factory.FromAsync(
                (c, s) => requestResponseAmqpLink.BeginRequest(amqpMessage, timeoutHelper.RemainingTime(), c, s),
                a => requestResponseAmqpLink.EndRequest(a),
                this).ConfigureAwait(false);

            var responseMessage = AmqpResponseMessage.CreateResponse(responseAmqpMessage);
            return responseMessage;
        }

        /// <summary></summary>
        /// <param name="maxMessageCount"></param>
        /// <param name="serverWaitTime"></param>
        /// <returns>The asynchronous operation.</returns>
        protected virtual async Task<IList<Message>> OnReceiveAsync(int maxMessageCount, TimeSpan serverWaitTime)
        {
            ReceivingAmqpLink receiveLink = null;
            try
            {
                var timeoutHelper = new TimeoutHelper(serverWaitTime, true);
                receiveLink = await ReceiveLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);

                IEnumerable<AmqpMessage> amqpMessages = null;
                var hasMessages = await Task.Factory.FromAsync(
                    (c, s) => receiveLink.BeginReceiveRemoteMessages(maxMessageCount, DefaultBatchFlushInterval, timeoutHelper.RemainingTime(), c, s),
                    a => receiveLink.EndReceiveMessages(a, out amqpMessages),
                    this).ConfigureAwait(false);

                if (receiveLink.TerminalException != null)
                {
                    throw receiveLink.TerminalException;
                }

                if (hasMessages && amqpMessages != null)
                {
                    IList<Message> brokeredMessages = null;
                    foreach (var amqpMessage in amqpMessages)
                    {
                        if (brokeredMessages == null)
                        {
                            brokeredMessages = new List<Message>();
                        }

                        if (ReceiveMode == ReceiveMode.ReceiveAndDelete)
                        {
                            receiveLink.DisposeDelivery(amqpMessage, true, AmqpConstants.AcceptedOutcome);
                        }

                        var message = AmqpMessageConverter.AmqpMessageToSBMessage(amqpMessage);
                        brokeredMessages.Add(message);
                    }

                    return brokeredMessages;
                }

                return null;
            }
            catch (Exception exception)
            {
                throw AmqpExceptionHelper.GetClientException(exception, receiveLink?.GetTrackingId());
            }
        }

        /// <summary></summary>
        /// <param name="fromSequenceNumber"></param>
        /// <param name="messageCount"></param>
        /// <returns>The asynchronous operation.</returns>
        protected virtual async Task<IList<Message>> OnPeekAsync(long fromSequenceNumber, int messageCount = 1)
        {
            try
            {
                var requestMessage =
                    AmqpRequestMessage.CreateRequest(
                        ManagementConstants.Operations.PeekMessageOperation,
                        OperationTimeout,
                        null);

                requestMessage.Map[ManagementConstants.Properties.FromSequenceNumber] = fromSequenceNumber;
                requestMessage.Map[ManagementConstants.Properties.MessageCount] = messageCount;

                if (!string.IsNullOrWhiteSpace(SessionId))
                {
                    requestMessage.Map[ManagementConstants.Properties.SessionId] = SessionId;
                }

                var messages = new List<Message>();

                var response = await ExecuteRequestResponseAsync(requestMessage).ConfigureAwait(false);
                if (response.StatusCode == AmqpResponseStatusCode.OK)
                {
                    Message message = null;
                    var messageList = response.GetListValue<AmqpMap>(ManagementConstants.Properties.Messages);
                    foreach (var entry in messageList)
                    {
                        var payload = (ArraySegment<byte>) entry[ManagementConstants.Properties.Message];
                        var amqpMessage =
                            AmqpMessage.CreateAmqpStreamMessage(new BufferListStream(new[] {payload}), true);
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
                    response.StatusCode == AmqpResponseStatusCode.NotFound && Equals(AmqpClientConstants.MessageNotFoundError, response.GetResponseErrorCondition()))
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

        /// <summary></summary>
        /// <param name="sequenceNumbers"></param>
        /// <returns>The asynchronous operation.</returns>
        protected virtual async Task<IList<Message>> OnReceiveBySequenceNumberAsync(IEnumerable<long> sequenceNumbers)
        {
            var messages = new List<Message>();
            try
            {
                var requestMessage = AmqpRequestMessage.CreateRequest(ManagementConstants.Operations.ReceiveBySequenceNumberOperation, OperationTimeout, null);
                requestMessage.Map[ManagementConstants.Properties.SequenceNumbers] = sequenceNumbers.ToArray();
                requestMessage.Map[ManagementConstants.Properties.ReceiverSettleMode] = (uint) (ReceiveMode == ReceiveMode.ReceiveAndDelete ? 0 : 1);

                var response = await ExecuteRequestResponseAsync(requestMessage).ConfigureAwait(false);

                if (response.StatusCode == AmqpResponseStatusCode.OK)
                {
                    var amqpMapList = response.GetListValue<AmqpMap>(ManagementConstants.Properties.Messages);
                    foreach (var entry in amqpMapList)
                    {
                        var payload = (ArraySegment<byte>) entry[ManagementConstants.Properties.Message];
                        var amqpMessage = AmqpMessage.CreateAmqpStreamMessage(new BufferListStream(new[] {payload}), true);
                        var message = AmqpMessageConverter.AmqpMessageToSBMessage(amqpMessage);
                        Guid lockToken;
                        if (entry.TryGetValue(ManagementConstants.Properties.LockToken, out lockToken))
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

        /// <summary></summary>
        /// <param name="lockTokens"></param>
        /// <returns>The asynchronous operation.</returns>
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

        /// <summary></summary>
        /// <param name="lockToken"></param>
        /// <returns>The asynchronous operation.</returns>
        protected virtual async Task OnAbandonAsync(string lockToken)
        {
            IEnumerable<Guid> lockTokens = new[] {new Guid(lockToken)};
            if (lockTokens.Any(lt => requestResponseLockedMessages.Contains(lt)))
            {
                await DisposeMessageRequestResponseAsync(lockTokens, DispositionStatus.Abandoned).ConfigureAwait(false);
            }
            else
            {
                await DisposeMessagesAsync(lockTokens, new Modified()).ConfigureAwait(false);
            }
        }

        /// <summary></summary>
        /// <param name="lockToken"></param>
        /// <returns>The asynchronous operation.</returns>
        protected virtual async Task OnDeferAsync(string lockToken)
        {
            IEnumerable<Guid> lockTokens = new[] {new Guid(lockToken)};
            if (lockTokens.Any(lt => requestResponseLockedMessages.Contains(lt)))
            {
                await DisposeMessageRequestResponseAsync(lockTokens, DispositionStatus.Defered).ConfigureAwait(false);
            }
            else
            {
                await DisposeMessagesAsync(lockTokens, new Modified
                {
                    UndeliverableHere = true
                }).ConfigureAwait(false);
            }
        }

        /// <summary></summary>
        /// <param name="lockToken"></param>
        /// <returns>The asynchronous operation.</returns>
        protected virtual async Task OnDeadLetterAsync(string lockToken)
        {
            IEnumerable<Guid> lockTokens = new[] {new Guid(lockToken)};
            if (lockTokens.Any(lt => requestResponseLockedMessages.Contains(lt)))
            {
                await DisposeMessageRequestResponseAsync(lockTokens, DispositionStatus.Suspended).ConfigureAwait(false);
            }
            else
            {
                await DisposeMessagesAsync(lockTokens, AmqpConstants.RejectedOutcome).ConfigureAwait(false);
            }
        }

        /// <summary></summary>
        /// <param name="lockToken"></param>
        /// <returns>The asynchronour operation.</returns>
        protected virtual async Task<DateTime> OnRenewLockAsync(string lockToken)
        {
            var lockedUntilUtc = DateTime.MinValue;
            try
            {
                // Create an AmqpRequest Message to renew  lock
                var requestMessage = AmqpRequestMessage.CreateRequest(ManagementConstants.Operations.RenewLockOperation, OperationTimeout, null);
                requestMessage.Map[ManagementConstants.Properties.LockTokens] = new[] {new Guid(lockToken)};

                var response = await ExecuteRequestResponseAsync(requestMessage).ConfigureAwait(false);

                if (response.StatusCode == AmqpResponseStatusCode.OK)
                {
                    var lockedUntilUtcTimes = response.GetValue<IEnumerable<DateTime>>(ManagementConstants.Properties.Expirations);
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

        async Task DisposeMessagesAsync(IEnumerable<Guid> lockTokens, Outcome outcome)
        {
            var timeoutHelper = new TimeoutHelper(OperationTimeout, true);
            var deliveryTags = ConvertLockTokensToDeliveryTags(lockTokens);

            ReceivingAmqpLink receiveLink = null;
            try
            {
                receiveLink = await ReceiveLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
                var disposeMessageTasks = new Task<Outcome>[deliveryTags.Count];
                var i = 0;
                foreach (var deliveryTag in deliveryTags)
                {
                    disposeMessageTasks[i++] = Task.Factory.FromAsync(
                        (c, s) => receiveLink.BeginDisposeMessage(deliveryTag, outcome, true, timeoutHelper.RemainingTime(), c, s),
                        a => receiveLink.EndDisposeMessage(a),
                        this);
                }

                var outcomes = await Task.WhenAll(disposeMessageTasks).ConfigureAwait(false);
                Error error = null;
                foreach (var item in outcomes)
                {
                    var disposedOutcome = item.DescriptorCode == Rejected.Code && (error = ((Rejected) item).Error) != null ? item : null;
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

                        throw AmqpExceptionHelper.ToMessagingContractException(error);
                    }
                }
            }
            catch (Exception exception)
            {
                if (exception is OperationCanceledException &&
                    receiveLink != null && receiveLink.State != AmqpObjectState.Opened)
                {
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
                var requestMessage = AmqpRequestMessage.CreateRequest(ManagementConstants.Operations.UpdateDispositionOperation, OperationTimeout, null);
                requestMessage.Map[ManagementConstants.Properties.LockTokens] = lockTokens.ToArray();
                requestMessage.Map[ManagementConstants.Properties.DispositionStatus] = dispositionStatus.ToString().ToLowerInvariant();

                var amqpResponseMessage = await ExecuteRequestResponseAsync(requestMessage).ConfigureAwait(false);
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

        IList<ArraySegment<byte>> ConvertLockTokensToDeliveryTags(IEnumerable<Guid> lockTokens)
        {
            return lockTokens.Select(lockToken => new ArraySegment<byte>(lockToken.ToByteArray())).ToList();
        }

        async Task<ReceivingAmqpLink> CreateLinkAsync(TimeSpan timeout)
        {
            FilterSet filterMap = null;

            MessagingEventSource.Log.AmqpReceiveLinkCreateStart(ClientId, false, EntityType, Path);

            if (isSessionReceiver)
            {
                filterMap = new FilterSet
                {
                    {AmqpClientConstants.SessionFilterName, SessionId}
                };
            }

            var linkSettings = new AmqpLinkSettings
            {
                Role = true,
                TotalLinkCredit = (uint) PrefetchCount,
                AutoSendFlow = PrefetchCount > 0,
                Source = new Source
                {
                    Address = Path,
                    FilterSet = filterMap
                },
                SettleType = ReceiveMode == ReceiveMode.PeekLock ? SettleMode.SettleOnDispose : SettleMode.SettleOnSend
            };

            if (EntityType != null)
            {
                linkSettings.AddProperty(AmqpClientConstants.EntityTypeName, (int) EntityType);
            }

            linkSettings.AddProperty(AmqpClientConstants.TimeoutName, (uint) timeout.TotalMilliseconds);

            var sendReceiveLinkCreator = new AmqpSendReceiveLinkCreator(Path, ServiceBusConnection, new[] {ClaimConstants.Listen}, CbsTokenProvider, linkSettings);
            var receivingAmqpLink = (ReceivingAmqpLink) await sendReceiveLinkCreator.CreateAndOpenAmqpLinkAsync().ConfigureAwait(false);

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

            var requestResponseLinkCreator = new AmqpRequestResponseLinkCreator(entityPath, ServiceBusConnection, new[] {ClaimConstants.Manage, ClaimConstants.Listen}, CbsTokenProvider, linkSettings);
            var requestResponseAmqpLink = (RequestResponseAmqpLink) await requestResponseLinkCreator.CreateAndOpenAmqpLinkAsync().ConfigureAwait(false);

            MessagingEventSource.Log.AmqpReceiveLinkCreateStop(ClientId);
            return requestResponseAmqpLink;
        }

        void CloseSession(ReceivingAmqpLink link)
        {
            link.Session.SafeClose();
        }

        void CloseRequestResponseSession(RequestResponseAmqpLink requestResponseAmqpLink)
        {
            requestResponseAmqpLink.Session.SafeClose();
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

        void ThrowIfNotPeekLockMode()
        {
            if (ReceiveMode != ReceiveMode.PeekLock)
            {
                throw Fx.Exception.AsError(new InvalidOperationException("The operation is only supported in 'PeekLock' receive mode."));
            }
        }

        async Task OnMessageHandlerAsync(
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
                receivePump = new MessageReceivePump(this, registerHandlerOptions, callback, receivePumpCancellationTokenSource.Token);
            }

            try
            {
                await receivePump.StartPumpAsync().ConfigureAwait(false);
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

        /// <summary>
        ///     Registers a <see cref="ServiceBusPlugin" /> to be used for receiving messages from Service Bus.
        /// </summary>
        /// <param name="serviceBusPlugin">The <see cref="ServiceBusPlugin" /> to register</param>
        public void RegisterPlugin(ServiceBusPlugin serviceBusPlugin)
        {
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
        ///     Unregisters a <see cref="ServiceBusPlugin" />.
        /// </summary>
        /// <param name="serviceBusPluginName">The name <see cref="ServiceBusPlugin.Name" /> to be unregistered</param>
        public void UnregisterPlugin(string serviceBusPluginName)
        {
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
    }
}