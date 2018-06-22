// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Management
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Microsoft.Azure.ServiceBus.Primitives;

    // TODO: Document all exceptions that might be thrown
    public class ManagementClient : IManagementClient
    {
        private HttpClient httpClient;
        private readonly RetryPolicy retryPolicy;
        private readonly string endpointFQDN;
        private readonly ITokenProvider tokenProvider;
        private readonly TimeSpan operationTimeout;
        private readonly int port;
        private readonly string clientId;

        public ManagementClient(string connectionString, RetryPolicy retryPolicy = default)
            : this(new ServiceBusConnectionStringBuilder(connectionString), retryPolicy:retryPolicy)
        {
        }

        public ManagementClient(string endpoint, ITokenProvider tokenProvider, RetryPolicy retryPolicy = default )
            : this(new ServiceBusConnectionStringBuilder { Endpoint = endpoint}, tokenProvider, retryPolicy)
        {
        }

        public ManagementClient(ServiceBusConnectionStringBuilder connectionStringBuilder, ITokenProvider tokenProvider = default, RetryPolicy retryPolicy = default)
        {
            this.httpClient = new HttpClient();
            this.endpointFQDN = connectionStringBuilder.Endpoint;
            this.retryPolicy = retryPolicy ?? RetryPolicy.Default;
            this.tokenProvider = tokenProvider ?? CreateTokenProvider(connectionStringBuilder);
            this.operationTimeout = Constants.DefaultOperationTimeout;
            this.port = GetPort(connectionStringBuilder.Endpoint);
            this.clientId = nameof(ManagementClient) + Guid.NewGuid().ToString("N").Substring(0, 6);

            MessagingEventSource.Log.ManagementClientCreated(this.clientId, this.operationTimeout.TotalSeconds, this.tokenProvider.ToString(), this.retryPolicy.ToString());
        }

        private static ITokenProvider CreateTokenProvider(ServiceBusConnectionStringBuilder builder)
        {
            if (builder.SasToken != null)
            {
                return new SharedAccessSignatureTokenProvider(builder.SasToken);
            }

            if (builder.SasKeyName != null || builder.SasKey != null)
            {
                return new SharedAccessSignatureTokenProvider(builder.SasKeyName, builder.SasKey);
            }

            throw new Exception("Could not create token provider. Either ITokenProvider has to be passed into constructor or connection string should contain information such as SAS token / SAS key name and SAS key.");
        }

        #region DeleteEntity

        public Task DeleteQueueAsync(string queueName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidQueueName(queueName);
            return DeleteEntity(queueName, cancellationToken);
        }

        public Task DeleteTopicAsync(string topicName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);
            return DeleteEntity(topicName, cancellationToken);
        }

        public Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);
            EntityNameHelper.CheckValidSubscriptionName(subscriptionName);

            return DeleteEntity(EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName), cancellationToken);
        }

        public Task DeleteRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);
            EntityNameHelper.CheckValidSubscriptionName(subscriptionName);
            EntityNameHelper.CheckValidRuleName(ruleName);

            return DeleteEntity($"{topicName}/Subscriptions/{subscriptionName}/rules/{ruleName}", cancellationToken);
        }

        private async Task DeleteEntity(string path, CancellationToken cancellationToken)
        {
            MessagingEventSource.Log.ManagementOperationStart(this.clientId, nameof(DeleteEntity), path);

            var uri = new UriBuilder(this.endpointFQDN)
            {
                Path = path,
                Scheme = Uri.UriSchemeHttps,
                Port = this.port,
                Query = $"{ManagementClientConstants.apiVersionQuery}&enrich=false"
            }.Uri;

            var request = new HttpRequestMessage(HttpMethod.Delete, uri);

            await this.retryPolicy.RunOperation(
                async () =>
                {
                    await SendHttpRequest(request, cancellationToken).ConfigureAwait(false);
                }, this.operationTimeout).ConfigureAwait(false);

            MessagingEventSource.Log.ManagementOperationEnd(this.clientId, nameof(DeleteEntity), path);
        }

        #endregion

        #region GetEntity

        public async Task<QueueDescription> GetQueueAsync(string queueName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidQueueName(queueName);

            var content = await GetEntity(queueName, null, false, cancellationToken).ConfigureAwait(false);

            return QueueDescriptionExtensions.ParseFromContent(content);
        }

        public async Task<TopicDescription> GetTopicAsync(string topicName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);

            var content = await GetEntity(topicName, null, false, cancellationToken).ConfigureAwait(false);

            return TopicDescriptionExtensions.ParseFromContent(content);
        }

        public async Task<SubscriptionDescription> GetSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);
            EntityNameHelper.CheckValidSubscriptionName(subscriptionName);

            var content = await GetEntity(EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName), null, false, cancellationToken).ConfigureAwait(false);

            return SubscriptionDescriptionExtensions.ParseFromContent(topicName, content);
        }

        public async Task<RuleDescription> GetRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);
            EntityNameHelper.CheckValidSubscriptionName(subscriptionName);
            EntityNameHelper.CheckValidRuleName(ruleName);

            var content = await GetEntity($"{topicName}/Subscriptions/{subscriptionName}/rules/{ruleName}", null, false, cancellationToken).ConfigureAwait(false);

            return RuleDescriptionExtensions.ParseFromContent(content);
        }

        #endregion

        #region GetRuntimeInfo

        public async Task<QueueRuntimeInfo> GetQueueRuntimeInfoAsync(string queueName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidQueueName(queueName);

            var content = await GetEntity(queueName, null, true, cancellationToken).ConfigureAwait(false);

            return QueueRuntimeInfoExtensions.ParseFromContent(content);
        }

        public async Task<TopicRuntimeInfo> GetTopicRuntimeInfoAsync(string topicName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);

            var content = await GetEntity(topicName, null, true, cancellationToken).ConfigureAwait(false);

            return TopicRuntimeInfoExtensions.ParseFromContent(content);
        }

        public async Task<SubscriptionRuntimeInfo> GetSubscriptionRuntimeInfoAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);
            EntityNameHelper.CheckValidSubscriptionName(subscriptionName);

            var content = await GetEntity(EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName), null, true, cancellationToken).ConfigureAwait(false);

            return SubscriptionRuntimeInfoExtensions.ParseFromContent(topicName, content);
        }

        #endregion

        #region GetEntities

        public async Task<IList<QueueDescription>> GetQueuesAsync(int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            if (count > 100 || count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Value should be between 1 and 100");
            }
            if (skip < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(skip), "Value cannot be negative");
            }

            var content = await GetEntity("$Resources/queues", $"$skip={skip}&$top={count}", false, cancellationToken).ConfigureAwait(false);
            return QueueDescriptionExtensions.ParseCollectionFromContent(content);
        }

        public async Task<IList<TopicDescription>> GetTopicsAsync(int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            if (count > 100 || count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Value should be between 1 and 100");
            }
            if (skip < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(skip), "Value cannot be negative");
            }

            var content = await GetEntity("$Resources/topics", $"$skip={skip}&$top={count}", false, cancellationToken).ConfigureAwait(false);
            return TopicDescriptionExtensions.ParseCollectionFromContent(content);
        }

        public async Task<IList<SubscriptionDescription>> GetSubscriptionsAsync(string topicName, int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);
            if (count > 100 || count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Value should be between 1 and 100");
            }
            if (skip < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(skip), "Value cannot be negative");
            }

            var content = await GetEntity($"{topicName}/Subscriptions", $"$skip={skip}&$top={count}", false, cancellationToken).ConfigureAwait(false);
            return SubscriptionDescriptionExtensions.ParseCollectionFromContent(topicName, content);
        }

        public async Task<IList<RuleDescription>> GetRulesAsync(string topicName, string subscriptionName, int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);
            EntityNameHelper.CheckValidSubscriptionName(subscriptionName);
            if (count > 100 || count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Value should be between 1 and 100");
            }
            if (skip < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(skip), "Value cannot be negative");
            }

            var content = await GetEntity($"{topicName}/Subscriptions/{subscriptionName}/rules", $"$skip={skip}&$top={count}", false, cancellationToken).ConfigureAwait(false);
            return RuleDescriptionExtensions.ParseCollectionFromContent(content);
        }

        private async Task<string> GetEntity(string path, string query, bool enrich, CancellationToken cancellationToken)
        {
            MessagingEventSource.Log.ManagementOperationStart(this.clientId, nameof(GetEntity), $"path:{path},query:{query},enrich:{enrich}");

            var queryString = $"{ManagementClientConstants.apiVersionQuery}&enrich={enrich}";
            if (query!=null)
            {
                queryString = queryString + "&" + query;
            }
            var uri = new UriBuilder(this.endpointFQDN)
            {
                Path = path,
                Scheme = Uri.UriSchemeHttps,
                Port = this.port,
                Query = queryString
            }.Uri;

            var request = new HttpRequestMessage(HttpMethod.Get, uri);

            HttpResponseMessage response = null;
            await this.retryPolicy.RunOperation(
                async () =>
                {
                    response = await SendHttpRequest(request, cancellationToken).ConfigureAwait(false);
                }, this.operationTimeout).ConfigureAwait(false);

            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            MessagingEventSource.Log.ManagementOperationEnd(this.clientId, nameof(GetEntity), $"path:{path},query:{query},enrich:{enrich}");
            return result;
        }

        #endregion

        #region CreateEntity

        /// <summary>
        /// Creates a new queue in the service namespace with the given name.
        /// </summary>
        /// <remarks>Throws if a queue already exists.</remarks>
        /// <param name="queueName">The name of the queue relative to the service namespace base address.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The <see cref="QueueDescription"/> of the newly created queue.</returns>
        /// <exception cref="ArgumentNullException">Queue name is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="queueName"/> is greater than 260 characters.</exception>
        /// <exception cref="MessagingEntityAlreadyExistsException">A queue with the same nameexists under the same service namespace.</exception>
        /// <exception cref="ServiceBusTimeoutException">The operation times out.</exception>
        /// <exception cref="UnauthorizedAccessException">No sufficient permission to perform this operation. You should check to ensure that your <see cref="ManagementClient"/> has the correct <see cref="TokenProvider"/> credentials to perform this operation.</exception>
        /// <exception cref="ServerBusyException">The server is overloaded with logical operations. You can consider any of the following actions:Wait and retry calling this function.Remove entities before retry (for example, receive messages before sending any more).</exception>
        /// <exception cref="ServiceBusException">An internal error or unexpected exception occurs.</exception>
        public Task<QueueDescription> CreateQueueAsync(string queueName, CancellationToken cancellationToken = default)
        {
            return this.CreateQueueAsync(new QueueDescription(queueName), cancellationToken);
        }

        /// <summary>
        /// Creates a new queue in the service namespace with the given name.
        /// </summary>
        /// <remarks>Throws if a queue already exists.</remarks>
        /// <param name="queueDescription">A <see cref="QueueDescription"/> object describing the attributes with which the new queue will be created.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The <see cref="QueueDescription"/> of the newly created queue.</returns>
        /// <exception cref="ArgumentNullException">Queue name is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The length of name is greater than 260 characters.</exception>
        /// <exception cref="MessagingEntityAlreadyExistsException">A queue with the same nameexists under the same service namespace.</exception>
        /// <exception cref="ServiceBusTimeoutException">The operation times out.</exception>
        /// <exception cref="UnauthorizedAccessException">No sufficient permission to perform this operation. You should check to ensure that your <see cref="ManagementClient"/> has the correct <see cref="TokenProvider"/> credentials to perform this operation.</exception>
        /// <exception cref="QuotaExceededException">Either the specified size in the description is not supported or the maximum allowable quota has been reached. You must specify one of the supported size values, delete existing entities, or increase your quota size.</exception>
        /// <exception cref="ServerBusyException">The server is overloaded with logical operations. You can consider any of the following actions:Wait and retry calling this function.Remove entities before retry (for example, receive messages before sending any more).</exception>
        /// <exception cref="ServiceBusException">An internal error or unexpected exception occurs.</exception>
        public async Task<QueueDescription> CreateQueueAsync(QueueDescription queueDescription, CancellationToken cancellationToken = default)
        {
            queueDescription.NormalizeDescription(this.endpointFQDN);
            var atomRequest = queueDescription.Serialize().ToString();
            var content = await PutEntity(
                queueDescription.Path, 
                atomRequest, 
                false, 
                queueDescription.ForwardTo, 
                queueDescription.ForwardDeadLetteredMessagesTo, 
                cancellationToken).ConfigureAwait(false);
            return QueueDescriptionExtensions.ParseFromContent(content);
        }


        /// <summary>
        /// Creates a new topic inside the service namespace with the given name.
        /// </summary>
        /// <param name="topicName">The name of the topic relative to the service namespace base address.</param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="ArgumentException"><paramref name="topicName"/> is null or empty, or path begins or ends with "/".</exception>
        /// <exception cref="ArgumentOutOfRangeException">Length of <paramref name="topicName"/> is greater than 260 characters.</exception>
        /// <exception cref="ServiceBusTimeoutException">The operation times out. The timeout period is initialized through the <see cref="ServiceBusConnection"/> class. You may need to increase the value to avoid this exception if the timeout value is relatively low.</exception>
        /// <exception cref="MessagingEntityAlreadyExistsException">A topic with the same name exists under the same service namespace.</exception>
        public Task<TopicDescription> CreateTopicAsync(string topicName, CancellationToken cancellationToken = default)
        {
            return this.CreateTopicAsync(new TopicDescription(topicName), cancellationToken);
        }

        /// <summary>
        /// Creates a new topic inside the service namespace with the given name.
        /// </summary>
        /// <param name="topicDescription">A <see cref="TopicDescription"/> object describing the attributes with which the new topic will be created.</param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="ServiceBusTimeoutException">The operation times out. The timeout period is initialized through the <see cref="ServiceBusConnection"/> class. You may need to increase the value to avoid this exception if the timeout value is relatively low.</exception>
        /// <exception cref="MessagingEntityAlreadyExistsException">A topic with the same name exists under the same service namespace.</exception>
        public async Task<TopicDescription> CreateTopicAsync(TopicDescription topicDescription, CancellationToken cancellationToken = default)
        {
            var atomRequest = topicDescription.Serialize().ToString();

            var content = await PutEntity(topicDescription.Path, atomRequest, false, null, null, cancellationToken).ConfigureAwait(false);

            return TopicDescriptionExtensions.ParseFromContent(content);
        }

        /// <summary>
        /// Creates a new subscription in the service namespace with the specified topic and subscription name.
        /// </summary>
        /// <param name="topicName">The topic name relative to the service namespace base address.</param>
        /// <param name="subscriptionName">The name of the subscription.</param>
        /// <param name="cancellationToken"></param>
        /// <remarks>Be default, A "pass-through" filter is created for this subscription, which means it will allow all message to go to this subscription. The name of the filter is represented by <see cref="RuleDescription.DefaultRuleName"/>.</remarks>
        public Task<SubscriptionDescription> CreateSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            return this.CreateSubscriptionAsync(new SubscriptionDescription(topicName, subscriptionName), cancellationToken);
        }

        /// <summary>
        /// Creates a new subscription in the service namespace with the specified subscription description.
        /// </summary>
        /// <param name="subscriptionDescription">A <see cref="SubscriptionDescription"/> object describing the attributes with which the new subscription will be created.</param>
        /// <param name="cancellationToken"></param>
        /// <remarks>Be default, A "pass-through" filter is created for this subscription, which means it will allow all message to go to this subscription. The name of the filter is represented by <see cref="RuleDescription.DefaultRuleName"/>.</remarks>
        public Task<SubscriptionDescription> CreateSubscriptionAsync(SubscriptionDescription subscriptionDescription, CancellationToken cancellationToken = default)
        {
            return this.CreateSubscriptionAsync(subscriptionDescription, null, cancellationToken);
        }

        /// <summary>
        /// Creates a new subscription in the service namespace with the specified subscription description and rule description.
        /// </summary>
        /// <param name="subscriptionDescription">A <see cref="SubscriptionDescription"/> object describing the attributes with which the new subscription will be created.</param>
        /// <param name="defaultRule">A <see cref="RuleDescription"/> object describing the attributes with which the messages are matched and acted upon.</param>
        /// <param name="cancellationToken"></param>
        /// <remarks>
        /// A default rule will be created using data from ruleDescription. If Name is null or white space, then the name of the rule created will be <see cref="RuleDescription.DefaultRuleName"/>.
        /// To avoid "pass-through" filter, pass <see cref="FalseFilter"/>.
        /// </remarks>
        public async Task<SubscriptionDescription> CreateSubscriptionAsync(SubscriptionDescription subscriptionDescription, RuleDescription defaultRule, CancellationToken cancellationToken = default)
        {
            subscriptionDescription.NormalizeDescription(this.endpointFQDN);
            subscriptionDescription.DefaultRuleDescription = defaultRule;
            var atomRequest = subscriptionDescription.Serialize().ToString();
            var content = await PutEntity(
                EntityNameHelper.FormatSubscriptionPath(subscriptionDescription.TopicPath, subscriptionDescription.SubscriptionName),
                atomRequest,
                false,
                subscriptionDescription.ForwardTo,
                subscriptionDescription.ForwardDeadLetteredMessagesTo,
                cancellationToken).ConfigureAwait(false);
            return SubscriptionDescriptionExtensions.ParseFromContent(subscriptionDescription.TopicPath, content);
        }

        /// <summary>
        /// Adds a new rule to the subscription under given topic.
        /// </summary>
        /// <param name="topicName">The topic name relative to the service namespace base address.</param>
        /// <param name="subscriptionName">The name of the subscription.</param>
        /// <param name="ruleDescription">A <see cref="RuleDescription"/> object describing the attributes with which the messages are matched and acted upon.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<RuleDescription> CreateRuleAsync(string topicName, string subscriptionName, RuleDescription ruleDescription, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);
            EntityNameHelper.CheckValidSubscriptionName(topicName);

            var atomRequest = ruleDescription.Serialize().ToString();

            var content = await PutEntity(
                EntityNameHelper.FormatRulePath(topicName, subscriptionName, ruleDescription.Name),
                atomRequest,
                false,
                null, 
                null,
                cancellationToken).ConfigureAwait(false);

            return RuleDescriptionExtensions.ParseFromContent(content);
        }

        #endregion CreateEntity

        #region UpdateEntity

        public async Task<QueueDescription> UpdateQueueAsync(QueueDescription queueDescription, CancellationToken cancellationToken = default)
        {
            queueDescription.NormalizeDescription(this.endpointFQDN);

            var atomRequest = queueDescription.Serialize().ToString();

            var content = await PutEntity(
                queueDescription.Path, 
                atomRequest, 
                true, 
                queueDescription.ForwardTo,
                queueDescription.ForwardDeadLetteredMessagesTo, 
                cancellationToken).ConfigureAwait(false);

            return QueueDescriptionExtensions.ParseFromContent(content);
        }

        public async Task<TopicDescription> UpdateTopicAsync(TopicDescription topicDescription, CancellationToken cancellationToken = default)
        {
            var atomRequest = topicDescription.Serialize().ToString();

            var content = await PutEntity(topicDescription.Path, atomRequest, true, null, null, cancellationToken).ConfigureAwait(false);

            return TopicDescriptionExtensions.ParseFromContent(content);
        }

        public async Task<SubscriptionDescription> UpdateSubscriptionAsync(SubscriptionDescription subscriptionDescription, CancellationToken cancellationToken = default)
        {
            subscriptionDescription.NormalizeDescription(this.endpointFQDN);
            var atomRequest = subscriptionDescription.Serialize().ToString();
            var content = await PutEntity(
                EntityNameHelper.FormatSubscriptionPath(subscriptionDescription.TopicPath, subscriptionDescription.SubscriptionName),
                atomRequest,
                true,
                subscriptionDescription.ForwardTo,
                subscriptionDescription.ForwardDeadLetteredMessagesTo,
                cancellationToken).ConfigureAwait(false);
            return SubscriptionDescriptionExtensions.ParseFromContent(subscriptionDescription.TopicPath, content);
        }

        public async Task<RuleDescription> UpdateRuleAsync(string topicName, string subscriptionName, RuleDescription ruleDescription, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidRuleName(ruleDescription.Name);

            var atomRequest = ruleDescription.Serialize().ToString();
            var content = await PutEntity(
                EntityNameHelper.FormatRulePath(topicName, subscriptionName, ruleDescription.Name),
                atomRequest,
                true,
                null, null,
                cancellationToken).ConfigureAwait(false);

            return RuleDescriptionExtensions.ParseFromContent(content);
        }

        private async Task<string> PutEntity(string path, string requestBody, bool isUpdate, string forwardTo, string fwdDeadLetterTo, CancellationToken cancellationToken)
        {
            MessagingEventSource.Log.ManagementOperationStart(this.clientId, nameof(PutEntity), $"path:{path},isUpdate:{isUpdate}");

            var uri = new UriBuilder(this.endpointFQDN)
            {
                Path = path,
                Port = this.port,
                Scheme = Uri.UriSchemeHttps,
                Query = $"{ManagementClientConstants.apiVersionQuery}"
            }.Uri;

            var request = new HttpRequestMessage(HttpMethod.Put, uri);
            request.Content = new StringContent(
                requestBody,
                Encoding.UTF8,
                ManagementClientConstants.AtomContentType
            );

            if (isUpdate)
            {
                request.Headers.Add("If-Match", "*"); 
            }

            if (!string.IsNullOrWhiteSpace(forwardTo))
            {
                var token = await this.GetToken(forwardTo).ConfigureAwait(false);
                request.Headers.Add(ManagementClientConstants.ServiceBusSupplementartyAuthorizationHeaderName, token);
            }

            if (!string.IsNullOrWhiteSpace(fwdDeadLetterTo))
            {
                var token = await this.GetToken(fwdDeadLetterTo).ConfigureAwait(false);
                request.Headers.Add(ManagementClientConstants.ServiceBusDlqSupplementaryAuthorizationHeaderName, token);
            }

            HttpResponseMessage response = null;
            await this.retryPolicy.RunOperation(
                async () =>
                {
                    response = await SendHttpRequest(request, cancellationToken).ConfigureAwait(false);
                }, this.operationTimeout).ConfigureAwait(false);

            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            MessagingEventSource.Log.ManagementOperationEnd(this.clientId, nameof(PutEntity), $"path:{path},isUpdate:{isUpdate}");
            return result;
        }

        #endregion

        #region Exists

        public async Task<bool> QueueExistsAsync(string queueName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidQueueName(queueName);
            
            try
            {
                // TODO: Optimize by removing deserialization costs.
                var qd = await GetQueueAsync(queueName, cancellationToken).ConfigureAwait(false);
            }
            catch (MessagingEntityNotFoundException)
            {
                return false;
            }

            return true;
        }

        public async Task<bool> TopicExistsAsync(string topicName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);
            
            try
            {
                // TODO: Optimize by removing deserialization costs.
                var td = await GetTopicAsync(topicName, cancellationToken).ConfigureAwait(false);
            }
            catch (MessagingEntityNotFoundException)
            {
                return false;
            }

            return true;
        }

        public async Task<bool> SubscriptionExistsAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);
            EntityNameHelper.CheckValidSubscriptionName(subscriptionName);

            try
            {
                // TODO: Optimize by removing deserialization costs.
                var sd = await GetSubscriptionAsync(topicName, subscriptionName, cancellationToken).ConfigureAwait(false);
            }
            catch (MessagingEntityNotFoundException)
            {
                return false;
            }

            return true;
        }

        public Task CloseAsync()
        {
            httpClient?.Dispose();
            httpClient = null;

            return Task.CompletedTask;
        }

        #endregion

        private async Task<HttpResponseMessage> SendHttpRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = await this.GetToken(request.RequestUri).ConfigureAwait(false);
            request.Headers.Add("Authorization", token);
            request.Headers.Add("UserAgent", $"SERVICEBUS/{ManagementClientConstants.ApiVersion}(api-origin={ClientInfo.Framework};os={ClientInfo.Platform};version={ClientInfo.Version};product={ClientInfo.Product})");
            HttpResponseMessage response;
            try
            {
                response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                MessagingEventSource.Log.ManagementOperationException(this.clientId, nameof(SendHttpRequest), exception);
                throw new ServiceBusException(true, exception);
            }

            var exceptionReturned = await ValidateHttpResponse(response).ConfigureAwait(false);
            if (exceptionReturned == null)
            {
                return response;
            }
            else
            {
                MessagingEventSource.Log.ManagementOperationException(this.clientId, nameof(SendHttpRequest), exceptionReturned);
                throw exceptionReturned;
            }
        }

        private static int GetPort(string endpoint)
        {
            // used for internal testing
            if (endpoint.EndsWith("onebox.windows-int.net", StringComparison.InvariantCultureIgnoreCase))
            {
                return 4446;
            }

            return -1;
        }

        private static async Task<Exception> ValidateHttpResponse(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return null;
            }

            var exceptionMessage = await response.Content?.ReadAsStringAsync();
            exceptionMessage = ParseDetailIfAvailable(exceptionMessage) ?? response.ReasonPhrase;

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new UnauthorizedException(exceptionMessage);
            }

            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
            {
                return new MessagingEntityNotFoundException(exceptionMessage);
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                if (response.RequestMessage.Method.Equals(HttpMethod.Delete))
                {
                    return new ServiceBusException(true, exceptionMessage);
                }

                if (response.RequestMessage.Method.Equals(HttpMethod.Put) && response.RequestMessage.Headers.IfMatch.Count > 0)
                {
                    // response.RequestMessage.Headers.IfMatch.Count > 0 is true for UpdateEntity scenario
                    return new ServiceBusException(true, exceptionMessage);
                }

                if (exceptionMessage.Contains(ManagementClientConstants.ConflictOperationInProgressSubCode))
                {
                    return new ServiceBusException(true, exceptionMessage);
                }

                return new MessagingEntityAlreadyExistsException(exceptionMessage);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                if (exceptionMessage.Contains(ManagementClientConstants.ForbiddenInvalidOperationSubCode))
                {
                    return new InvalidOperationException(exceptionMessage);
                }

                return new QuotaExceededException(exceptionMessage);
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return new ServiceBusException(false, new ArgumentException(exceptionMessage));
            }

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                return new ServerBusyException(exceptionMessage);
            }

            return new ServiceBusException(true, exceptionMessage + "; response status code: " + response.StatusCode);
        }

        private static string ParseDetailIfAvailable(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                var errorContentXml = XElement.Parse(content);
                var detail = errorContentXml.Element("Detail");

                return detail?.Value ?? content;
            }
            catch (Exception)
            {
                return content;
            }
        }

        Task<string> GetToken(Uri requestUri)
        {
            return this.GetToken(requestUri.GetLeftPart(UriPartial.Path));
        }

        async Task<string> GetToken(string requestUri)
        {
            var token = await this.tokenProvider.GetTokenAsync(requestUri, TimeSpan.FromHours(1)).ConfigureAwait(false);
            return token.TokenValue;
        }
    }
}
