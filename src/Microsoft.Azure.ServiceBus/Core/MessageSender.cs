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
    ///     The MessageSender can be used to send messages to Queues or Topics.
    /// </summary>
    public class MessageSender : ClientEntity, IMessageSender
    {
        readonly bool ownsConnection;
        int deliveryCount;

        /// <summary>
        ///     Creates a new MessageSender.
        /// </summary>
        /// <param name="connectionStringBuilder">
        ///     The <see cref="ServiceBusConnectionStringBuilder" /> used for the connection
        ///     details
        /// </param>
        /// <param name="retryPolicy">The <see cref="RetryPolicy" /> that will be used when communicating with Service Bus</param>
        public MessageSender(
            ServiceBusConnectionStringBuilder connectionStringBuilder,
            RetryPolicy retryPolicy = null)
            : this(connectionStringBuilder?.GetNamespaceConnectionString(), connectionStringBuilder.EntityPath, retryPolicy)
        {
        }

        /// <summary>
        ///     Creates a new MessageSender.
        /// </summary>
        /// <param name="connectionString">The connection string used to communicate with Service Bus.</param>
        /// <param name="entityPath">The path of the entity for this sender.</param>
        /// <param name="retryPolicy">The <see cref="RetryPolicy" /> that will be used when communicating with Service Bus</param>
        public MessageSender(
            string connectionString,
            string entityPath,
            RetryPolicy retryPolicy = null)
            : this(entityPath, null, new ServiceBusNamespaceConnection(connectionString), null, retryPolicy)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw Fx.Exception.ArgumentNullOrWhiteSpace(connectionString);
            if (string.IsNullOrWhiteSpace(entityPath))
                throw Fx.Exception.ArgumentNullOrWhiteSpace(entityPath);

            ownsConnection = true;
            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(
                ServiceBusConnection.SasKeyName,
                ServiceBusConnection.SasKey);
            CbsTokenProvider = new TokenProviderAdapter(tokenProvider, ServiceBusConnection.OperationTimeout);
        }

        internal MessageSender(
            string entityPath,
            MessagingEntityType? entityType,
            ServiceBusConnection serviceBusConnection,
            ICbsTokenProvider cbsTokenProvider,
            RetryPolicy retryPolicy)
            : base(nameof(MessageSender) + StringUtility.GetRandomString(), retryPolicy ?? RetryPolicy.Default)
        {
            OperationTimeout = serviceBusConnection.OperationTimeout;
            Path = entityPath;
            EntityType = entityType;
            ServiceBusConnection = serviceBusConnection;
            CbsTokenProvider = cbsTokenProvider;
            SendLinkManager = new FaultTolerantAmqpObject<SendingAmqpLink>(CreateLinkAsync, CloseSession);
            RequestResponseLinkManager = new FaultTolerantAmqpObject<RequestResponseAmqpLink>(CreateRequestResponseLinkAsync, CloseRequestResponseSession);
        }

        /// <summary>
        ///     Gets a list of currently registered plugins.
        /// </summary>
        public IList<ServiceBusPlugin> RegisteredPlugins { get; } = new List<ServiceBusPlugin>();

        internal TimeSpan OperationTimeout { get; }

        internal MessagingEntityType? EntityType { get; }

        /// <summary>
        ///     Gets the path of the MessageSender.
        /// </summary>
        public virtual string Path { get; }

        ServiceBusConnection ServiceBusConnection { get; }

        ICbsTokenProvider CbsTokenProvider { get; }

        FaultTolerantAmqpObject<SendingAmqpLink> SendLinkManager { get; }

        FaultTolerantAmqpObject<RequestResponseAmqpLink> RequestResponseLinkManager { get; }

        /// <summary>
        ///     Sends a message to the path of the <see cref="MessageSender" />.
        /// </summary>
        /// <param name="message">The <see cref="Message" /> to send</param>
        /// <returns>An asynchronous operation</returns>
        public Task SendAsync(Message message)
        {
            return SendAsync(new[] {message});
        }

        /// <summary>
        ///     Sends a list of messages to the path of the <see cref="MessageSender" />.
        /// </summary>
        /// <param name="messageList">The <see cref="IList{Message}" /> to send</param>
        /// <returns>An asynchronous operation</returns>
        public async Task SendAsync(IList<Message> messageList)
        {
            var count = ValidateMessages(messageList);
            MessagingEventSource.Log.MessageSendStart(ClientId, count);

            var processedMessages = await ProcessMessages(messageList).ConfigureAwait(false);

            try
            {
                await RetryPolicy.RunOperation(
                        async () => { await OnSendAsync(processedMessages).ConfigureAwait(false); }, OperationTimeout)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageSendException(ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageSendStop(ClientId);
        }

        /// <summary>
        ///     Schedules a message to appear on Service Bus.
        /// </summary>
        /// <param name="message">The <see cref="Message" /></param>
        /// <param name="scheduleEnqueueTimeUtc">The UTC time that the message should be available for processing</param>
        /// <returns>An asynchronous operation</returns>
        public async Task<long> ScheduleMessageAsync(Message message, DateTimeOffset scheduleEnqueueTimeUtc)
        {
            if (message == null)
                throw Fx.Exception.ArgumentNull(nameof(message));

            if (scheduleEnqueueTimeUtc.CompareTo(DateTimeOffset.UtcNow) < 0)
                throw Fx.Exception.ArgumentOutOfRange(
                    nameof(scheduleEnqueueTimeUtc),
                    scheduleEnqueueTimeUtc.ToString(),
                    "Cannot schedule messages in the past");

            message.ScheduledEnqueueTimeUtc = scheduleEnqueueTimeUtc.UtcDateTime;
            ValidateMessage(message);
            MessagingEventSource.Log.ScheduleMessageStart(ClientId, scheduleEnqueueTimeUtc);
            long result = 0;

            var processedMessage = await ProcessMessage(message).ConfigureAwait(false);

            try
            {
                await RetryPolicy.RunOperation(
                        async () => { result = await OnScheduleMessageAsync(processedMessage).ConfigureAwait(false); }, OperationTimeout)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.ScheduleMessageException(ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.ScheduleMessageStop(ClientId);
            return result;
        }

        /// <summary>
        ///     Cancels a message that was scheduled.
        /// </summary>
        /// <param name="sequenceNumber">
        ///     The <see cref="Message.SystemPropertiesCollection.SequenceNumber" /> of the message to be
        ///     cancelled.
        /// </param>
        /// <returns>An asynchronous operation</returns>
        public async Task CancelScheduledMessageAsync(long sequenceNumber)
        {
            MessagingEventSource.Log.CancelScheduledMessageStart(ClientId, sequenceNumber);

            try
            {
                await RetryPolicy.RunOperation(
                        async () => { await OnCancelScheduledMessageAsync(sequenceNumber).ConfigureAwait(false); }, OperationTimeout)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.CancelScheduledMessageException(ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.CancelScheduledMessageStop(ClientId);
        }

        /// <summary></summary>
        /// <returns></returns>
        protected override async Task OnClosingAsync()
        {
            await SendLinkManager.CloseAsync().ConfigureAwait(false);
            await RequestResponseLinkManager.CloseAsync().ConfigureAwait(false);

            if (ownsConnection)
                await ServiceBusConnection.CloseAsync().ConfigureAwait(false);
        }

        async Task<Message> ProcessMessage(Message message)
        {
            var processedMessage = message;
            foreach (var plugin in RegisteredPlugins)
                try
                {
                    MessagingEventSource.Log.PluginCallStarted(plugin.Name, message.MessageId);
                    processedMessage = await plugin.BeforeMessageSend(message).ConfigureAwait(false);
                    MessagingEventSource.Log.PluginCallCompleted(plugin.Name, message.MessageId);
                }
                catch (Exception ex)
                {
                    MessagingEventSource.Log.PluginCallFailed(plugin.Name, message.MessageId, ex.Message);
                    if (!plugin.ShouldContinueOnException)
                        throw;
                }
            return processedMessage;
        }

        async Task<IList<Message>> ProcessMessages(IList<Message> messageList)
        {
            if (RegisteredPlugins.Count < 1)
                return messageList;

            var processedMessageList = new List<Message>();
            foreach (var message in messageList)
            {
                var processedMessage = await ProcessMessage(message).ConfigureAwait(false);
                processedMessageList.Add(processedMessage);
            }

            return processedMessageList;
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

        async Task OnSendAsync(IList<Message> messageList)
        {
            var timeoutHelper = new TimeoutHelper(OperationTimeout, true);
            using (var amqpMessage = AmqpMessageConverter.BatchSBMessagesAsAmqpMessage(messageList, true))
            {
                SendingAmqpLink amqpLink = null;
                try
                {
                    amqpLink = await SendLinkManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
                    if (amqpLink.Settings.MaxMessageSize.HasValue)
                    {
                        var size = (ulong) amqpMessage.SerializedMessageSize;
                        if (size > amqpLink.Settings.MaxMessageSize.Value)
                            throw new NotImplementedException("MessageSizeExceededException: " + Resources.AmqpMessageSizeExceeded.FormatForUser(amqpMessage.DeliveryId.Value, size, amqpLink.Settings.MaxMessageSize.Value));
                    }

                    var outcome = await amqpLink.SendMessageAsync(amqpMessage, GetNextDeliveryTag(), AmqpConstants.NullBinary, timeoutHelper.RemainingTime()).ConfigureAwait(false);
                    if (outcome.DescriptorCode != Accepted.Code)
                    {
                        var rejected = (Rejected) outcome;
                        throw Fx.Exception.AsError(AmqpExceptionHelper.ToMessagingContractException(rejected.Error));
                    }
                }
                catch (Exception exception)
                {
                    throw AmqpExceptionHelper.GetClientException(exception, amqpLink?.GetTrackingId());
                }
            }
        }

        async Task<long> OnScheduleMessageAsync(Message message)
        {
            // TODO: Ensure System.Transactions.Transaction.Current is null. Transactions are not supported by 1.0.0 version of dotnet core.
            using (var amqpMessage = AmqpMessageConverter.SBMessageToAmqpMessage(message))
            {
                var request = AmqpRequestMessage.CreateRequest(
                    ManagementConstants.Operations.ScheduleMessageOperation,
                    OperationTimeout,
                    null);

                var payload = amqpMessage.GetPayload();
                var buffer = new BufferListStream(payload);
                var value = buffer.ReadBytes((int) buffer.Length);

                var entry = new AmqpMap();
                {
                    entry[ManagementConstants.Properties.Message] = value;
                    entry[ManagementConstants.Properties.MessageId] = message.MessageId;

                    if (!string.IsNullOrWhiteSpace(message.SessionId))
                        entry[ManagementConstants.Properties.SessionId] = message.SessionId;

                    if (!string.IsNullOrWhiteSpace(message.PartitionKey))
                        entry[ManagementConstants.Properties.PartitionKey] = message.PartitionKey;
                }

                request.Map[ManagementConstants.Properties.Messages] = new List<AmqpMap>
                {
                    entry
                };

                IEnumerable<long> sequenceNumbers = null;
                var response = await ExecuteRequestResponseAsync(request);
                if (response.StatusCode == AmqpResponseStatusCode.OK)
                    sequenceNumbers = response.GetValue<long[]>(ManagementConstants.Properties.SequenceNumbers);
                else
                    response.ToMessagingContractException();

                return sequenceNumbers?.FirstOrDefault() ?? 0;
            }
        }

        async Task OnCancelScheduledMessageAsync(long sequenceNumber)
        {
            // TODO: Ensure System.Transactions.Transaction.Current is null. Transactions are not supported by 1.0.0 version of dotnet core.
            var request =
                AmqpRequestMessage.CreateRequest(
                    ManagementConstants.Operations.CancelScheduledMessageOperation,
                    OperationTimeout,
                    null);
            request.Map[ManagementConstants.Properties.SequenceNumbers] = new[] {sequenceNumber};

            var response = await ExecuteRequestResponseAsync(request).ConfigureAwait(false);

            if (response.StatusCode != AmqpResponseStatusCode.OK)
                throw response.ToMessagingContractException();
        }

        ArraySegment<byte> GetNextDeliveryTag()
        {
            var deliveryId = Interlocked.Increment(ref deliveryCount);
            return new ArraySegment<byte>(BitConverter.GetBytes(deliveryId));
        }

        async Task<SendingAmqpLink> CreateLinkAsync(TimeSpan timeout)
        {
            MessagingEventSource.Log.AmqpSendLinkCreateStart(ClientId, EntityType, Path);

            var linkSettings = new AmqpLinkSettings
            {
                Role = false,
                InitialDeliveryCount = 0,
                Target = new Target
                {
                    Address = Path
                },
                Source = new Source
                {
                    Address = ClientId
                }
            };
            if (EntityType != null)
                linkSettings.AddProperty(AmqpClientConstants.EntityTypeName, (int) EntityType);

            var sendReceiveLinkCreator = new AmqpSendReceiveLinkCreator(Path, ServiceBusConnection, new[] {ClaimConstants.Send}, CbsTokenProvider, linkSettings);
            var sendingAmqpLink = (SendingAmqpLink) await sendReceiveLinkCreator.CreateAndOpenAmqpLinkAsync().ConfigureAwait(false);

            MessagingEventSource.Log.AmqpSendLinkCreateStop(ClientId);
            return sendingAmqpLink;
        }

        async Task<RequestResponseAmqpLink> CreateRequestResponseLinkAsync(TimeSpan timeout)
        {
            var entityPath = Path + '/' + AmqpClientConstants.ManagementAddress;
            var linkSettings = new AmqpLinkSettings();
            linkSettings.AddProperty(AmqpClientConstants.EntityTypeName, AmqpClientConstants.EntityTypeManagement);

            var requestResponseLinkCreator = new AmqpRequestResponseLinkCreator(
                entityPath,
                ServiceBusConnection,
                new[] {ClaimConstants.Manage, ClaimConstants.Send},
                CbsTokenProvider,
                linkSettings);

            var requestResponseAmqpLink =
                (RequestResponseAmqpLink) await requestResponseLinkCreator.CreateAndOpenAmqpLinkAsync()
                    .ConfigureAwait(false);
            return requestResponseAmqpLink;
        }

        void CloseSession(SendingAmqpLink link)
        {
            // Note we close the session (which includes the link).
            link.Session.SafeClose();
        }

        void CloseRequestResponseSession(RequestResponseAmqpLink requestResponseAmqpLink)
        {
            requestResponseAmqpLink.Session.SafeClose();
        }

        static int ValidateMessages(IList<Message> messageList)
        {
            var count = 0;
            if (messageList == null)
                throw Fx.Exception.ArgumentNull(nameof(messageList));

            foreach (var message in messageList)
            {
                count++;
                ValidateMessage(message);
            }

            return count;
        }

        static void ValidateMessage(Message message)
        {
            if (message.SystemProperties.IsLockTokenSet)
                throw Fx.Exception.Argument(nameof(message), "Cannot send a message that was already received.");
        }

        /// <summary>
        ///     Registers a <see cref="ServiceBusPlugin" /> to be used for sending messages to Service Bus.
        /// </summary>
        /// <param name="serviceBusPlugin">The <see cref="ServiceBusPlugin" /> to register</param>
        public void RegisterPlugin(ServiceBusPlugin serviceBusPlugin)
        {
            if (serviceBusPlugin == null)
                throw new ArgumentNullException(nameof(serviceBusPlugin), Resources.ArgumentNullOrWhiteSpace.FormatForUser(nameof(serviceBusPlugin)));

            if (RegisteredPlugins.Any(p => p.GetType() == serviceBusPlugin.GetType()))
                throw new ArgumentException(nameof(serviceBusPlugin), Resources.PluginAlreadyRegistered.FormatForUser(nameof(serviceBusPlugin)));
            RegisteredPlugins.Add(serviceBusPlugin);
        }

        /// <summary>
        ///     Unregisters a <see cref="ServiceBusPlugin" />.
        /// </summary>
        /// <param name="serviceBusPluginName">The name <see cref="ServiceBusPlugin.Name" /> to be unregistered</param>
        public void UnregisterPlugin(string serviceBusPluginName)
        {
            if (serviceBusPluginName == null)
                throw new ArgumentNullException(nameof(serviceBusPluginName), Resources.ArgumentNullOrWhiteSpace.FormatForUser(nameof(serviceBusPluginName)));
            if (RegisteredPlugins.Any(p => p.Name == serviceBusPluginName))
            {
                var plugin = RegisteredPlugins.First(p => p.Name == serviceBusPluginName);
                RegisteredPlugins.Remove(plugin);
            }
        }
    }
}