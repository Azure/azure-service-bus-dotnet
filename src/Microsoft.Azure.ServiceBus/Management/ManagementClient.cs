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

            return QueueRuntimeInfo.ParseFromContent(content);
        }

        public async Task<TopicRuntimeInfo> GetTopicRuntimeInfoAsync(string topicName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);

            var content = await GetEntity(topicName, null, true, cancellationToken).ConfigureAwait(false);

            return TopicRuntimeInfo.ParseFromContent(content);
        }

        public async Task<SubscriptionRuntimeInfo> GetSubscriptionRuntimeInfoAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);
            EntityNameHelper.CheckValidSubscriptionName(subscriptionName);

            var content = await GetEntity(EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName), null, true, cancellationToken).ConfigureAwait(false);

            return SubscriptionRuntimeInfo.ParseFromContent(topicName, content);
        }

        #endregion

        #region GetEntities

        public async Task<IList<QueueDescription>> GetQueuesAsync(int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            var content = await GetEntity("$Resources/queues", $"$skip={skip}&$top={count}", false, cancellationToken).ConfigureAwait(false);
            return QueueDescriptionExtensions.ParseCollectionFromContent(content);
        }

        public async Task<IList<TopicDescription>> GetTopicsAsync(int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            var content = await GetEntity("$Resources/topics", $"$skip={skip}&$top={count}", false, cancellationToken).ConfigureAwait(false);

            return TopicDescriptionExtensions.ParseCollectionFromContent(content);
        }

        public async Task<IList<SubscriptionDescription>> GetSubscriptionsAsync(string topicName, int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);

            var content = await GetEntity($"{topicName}/Subscriptions", $"$skip={skip}&$top={count}", false, cancellationToken).ConfigureAwait(false);

            return SubscriptionDescriptionExtensions.ParseCollectionFromContent(topicName, content);
        }

        public async Task<IList<RuleDescription>> GetRulesAsync(string topicName, string subscriptionName, int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            EntityNameHelper.CheckValidTopicName(topicName);
            EntityNameHelper.CheckValidSubscriptionName(subscriptionName);

            var content = await GetEntity($"{topicName}/Subscriptions/{subscriptionName}/rules", $"$skip={skip}&$top={count}", false, cancellationToken).ConfigureAwait(false);

            return RuleDescriptionExtensions.ParseCollectionFromContent(content);
        }

        private async Task<string> GetEntity(string path, string query, bool enrich, CancellationToken cancellationToken)
        {
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

            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        #endregion

        #region CreateEntity

        public Task<QueueDescription> CreateQueueAsync(string queueName, CancellationToken cancellationToken = default)
        {
            return this.CreateQueueAsync(new QueueDescription(queueName), cancellationToken);
        }

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

        public Task<TopicDescription> CreateTopicAsync(string topicName, CancellationToken cancellationToken = default)
        {
            return this.CreateTopicAsync(new TopicDescription(topicName), cancellationToken);
        }

        public async Task<TopicDescription> CreateTopicAsync(TopicDescription topicDescription, CancellationToken cancellationToken = default)
        {
            var atomRequest = topicDescription.Serialize().ToString();

            var content = await PutEntity(topicDescription.Path, atomRequest, false, null, null, cancellationToken).ConfigureAwait(false);

            return TopicDescriptionExtensions.ParseFromContent(content);
        }

        public Task<SubscriptionDescription> CreateSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            return this.CreateSubscriptionAsync(new SubscriptionDescription(topicName, subscriptionName), cancellationToken);
        }

        public Task<SubscriptionDescription> CreateSubscriptionAsync(SubscriptionDescription subscriptionDescription, CancellationToken cancellationToken = default)
        {
            return this.CreateSubscriptionAsync(subscriptionDescription, null, cancellationToken);
        }

        public async Task<SubscriptionDescription> CreateSubscriptionAsync(SubscriptionDescription subscriptionDescription, RuleDescription defaultRule, CancellationToken cancellationToken = default)
        {
            subscriptionDescription.NormalizeDescription(this.csBuilder.Endpoint);
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

            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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
                throw new ServiceBusException(true, exception);
            }

            await ValidateHttpResponse(response).ConfigureAwait(false);
            return response;
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

        private static async Task ValidateHttpResponse(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var exceptionMessage = await response.Content?.ReadAsStringAsync();
            exceptionMessage = ParseDetailIfAvailable(exceptionMessage) ?? response.ReasonPhrase;

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new UnauthorizedException(exceptionMessage);
            }

            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
            {
                throw new MessagingEntityNotFoundException(exceptionMessage);
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                if (response.RequestMessage.Method.Equals(HttpMethod.Delete))
                {
                    throw new ServiceBusException(true, exceptionMessage);
                }

                if (response.RequestMessage.Method.Equals(HttpMethod.Put) && response.RequestMessage.Headers.IfMatch.Count > 0)
                {
                    // response.RequestMessage.Headers.IfMatch.Count > 0 is true for UpdateEntity scenario
                    throw new ServiceBusException(true, exceptionMessage);
                }

                if (exceptionMessage.Contains(ManagementClientConstants.ConflictOperationInProgressSubCode))
                {
                    throw new ServiceBusException(true, exceptionMessage);
                }

                throw new MessagingEntityAlreadyExistsException(exceptionMessage);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                if (exceptionMessage.Contains(ManagementClientConstants.ForbiddenInvalidOperationSubCode))
                {
                    throw new InvalidOperationException(exceptionMessage);
                }
                
                throw new QuotaExceededException(exceptionMessage);
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new ServiceBusException(false, new ArgumentException(exceptionMessage));
            }

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                throw new ServerBusyException(exceptionMessage);
            }

            throw new ServiceBusException(true, exceptionMessage);
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
