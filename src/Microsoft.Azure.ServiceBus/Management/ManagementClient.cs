using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus.Management
{
    // TODO: Document all exceptions that might be thrown
    public class ManagementClient : IManagementClient
    {
        private HttpClient httpClient;
        private ServiceBusConnectionStringBuilder csBuilder;
        private readonly int port;
        private readonly InternalClient internalClient;
            this.port = GetPort(connectionStringBuilder.Endpoint);

        public ManagementClient(string connectionString, RetryPolicy retryPolicy = null)
            : this(new ServiceBusConnectionStringBuilder(connectionString), retryPolicy)
        {
        }

        public ManagementClient(string endpoint, ITokenProvider tokenProvider, RetryPolicy retryPolicy = null)
            : this(new ServiceBusConnectionStringBuilder() { Endpoint = endpoint}, retryPolicy)
        {
            this.internalClient.ServiceBusConnection.TokenProvider = tokenProvider;
        }

        public ManagementClient(ServiceBusConnectionStringBuilder connectionStringBuilder, RetryPolicy retryPolicy = null)
        {
            this.csBuilder = connectionStringBuilder;
            this.httpClient = new HttpClient();

            this.internalClient = new InternalClient(nameof(ManagementClient), string.Empty, retryPolicy ?? RetryPolicy.Default, connectionStringBuilder);
        }

        #region DeleteEntity

        public Task DeleteQueueAsync(string queueName, CancellationToken cancellationToken = default)
        {
            CheckValidQueueName(queueName);
            return DeleteEntity(queueName, cancellationToken);
        }

        public Task DeleteTopicAsync(string topicName, CancellationToken cancellationToken = default)
        {
            CheckValidTopicName(topicName);
            return DeleteEntity(topicName, cancellationToken);
        }

        public Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            CheckValidTopicName(topicName);
            CheckValidSubscriptionName(subscriptionName);
            return DeleteEntity(EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName), cancellationToken);
        }

        public Task DeleteRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default)
        {
            CheckValidTopicName(topicName);
            CheckValidSubscriptionName(subscriptionName);
            CheckValidRuleName(ruleName);
            return DeleteEntity($"{topicName}/Subscriptions/{subscriptionName}/rules/{ruleName}", cancellationToken);
        }

        private async Task DeleteEntity(string path, CancellationToken cancellationToken)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = path,
                Scheme = Uri.UriSchemeHttps,
                Port = this.port,
                Query = $"{ManagementClientConstants.apiVersionQuery}&enrich=false"
            }.Uri;

            var request = new HttpRequestMessage(HttpMethod.Delete, uri);

            await this.internalClient.RetryPolicy.RunOperation(
                async () =>
                {
                    await SendHttpRequest(request, cancellationToken).ConfigureAwait(false);
                }, this.internalClient.ServiceBusConnection.OperationTimeout).ConfigureAwait(false);
        }

        #endregion

        #region GetEntity

        public async Task<QueueDescription> GetQueueAsync(string queueName, CancellationToken cancellationToken = default)
        {
            CheckValidQueueName(queueName);
            var content = await GetEntity(queueName, null, false, cancellationToken).ConfigureAwait(false);
            return QueueDescription.ParseFromContent(content);
        }

        public async Task<TopicDescription> GetTopicAsync(string topicName, CancellationToken cancellationToken = default)
        {
            CheckValidTopicName(topicName);
            var content = await GetEntity(topicName, null, false, cancellationToken).ConfigureAwait(false);
            return TopicDescription.ParseFromContent(content);
        }

        public async Task<SubscriptionDescription> GetSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            CheckValidTopicName(topicName);
            CheckValidSubscriptionName(subscriptionName);
            var content = await GetEntity(EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName), null, false, cancellationToken).ConfigureAwait(false);
            return SubscriptionDescription.ParseFromContent(topicName, content);
        }

        public async Task<RuleDescription> GetRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default)
        {
            CheckValidTopicName(topicName);
            CheckValidSubscriptionName(subscriptionName);
            CheckValidRuleName(ruleName);
            var content = await GetEntity($"{topicName}/Subscriptions/{subscriptionName}/rules/{ruleName}", null, false, cancellationToken).ConfigureAwait(false);
            return RuleDescription.ParseFromContent(content);
        }

        #endregion

        #region GetRuntimeInfo

        public async Task<QueueRuntimeInfo> GetQueueRuntimeInfoAsync(string queueName, CancellationToken cancellationToken = default)
        {
            CheckValidQueueName(queueName);
            var content = await GetEntity(queueName, null, true, cancellationToken).ConfigureAwait(false);
            return QueueRuntimeInfo.ParseFromContent(content);
        }

        public async Task<TopicRuntimeInfo> GetTopicRuntimeInfoAsync(string topicName, CancellationToken cancellationToken = default)
        {
            CheckValidTopicName(topicName);
            var content = await GetEntity(topicName, null, true, cancellationToken).ConfigureAwait(false);
            return TopicRuntimeInfo.ParseFromContent(content);
        }

        public async Task<SubscriptionRuntimeInfo> GetSubscriptionRuntimeInfoAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            CheckValidTopicName(topicName);
            CheckValidSubscriptionName(subscriptionName);
            var content = await GetEntity(EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName), null, true, cancellationToken).ConfigureAwait(false);
            return SubscriptionRuntimeInfo.ParseFromContent(topicName, content);
        }

        #endregion

        #region GetEntities

        public async Task<IList<QueueDescription>> GetQueuesAsync(int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            var content = await GetEntity("$Resources/queues", $"$skip={skip}&$top={count}", false, cancellationToken).ConfigureAwait(false);
            return QueueDescription.ParseCollectionFromContent(content);
        }

        public async Task<IList<TopicDescription>> GetTopicsAsync(int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            var content = await GetEntity("$Resources/topics", $"$skip={skip}&$top={count}", false, cancellationToken).ConfigureAwait(false);
            return TopicDescription.ParseCollectionFromContent(content);
        }

        public async Task<IList<SubscriptionDescription>> GetSubscriptionsAsync(string topicName, int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            CheckValidTopicName(topicName);
            var content = await GetEntity($"{topicName}/Subscriptions", $"$skip={skip}&$top={count}", false, cancellationToken).ConfigureAwait(false);
            return SubscriptionDescription.ParseCollectionFromContent(topicName, content);
        }

        public async Task<IList<RuleDescription>> GetRulesAsync(string topicName, string subscriptionName, int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            CheckValidTopicName(topicName);
            CheckValidSubscriptionName(subscriptionName);
            var content = await GetEntity($"{topicName}/Subscriptions/{subscriptionName}/rules", $"$skip={skip}&$top={count}", false, cancellationToken).ConfigureAwait(false);
            return RuleDescription.ParseCollectionFromContent(content);
        }

        private async Task<string> GetEntity(string path, string query, bool enrich, CancellationToken cancellationToken)
        {
            var queryString = $"{ManagementClientConstants.apiVersionQuery}&enrich={enrich}";
            if (query!=null)
            {
                queryString = queryString + "&" + query;
            }
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = path,
                Scheme = Uri.UriSchemeHttps,
                Port = this.port,
                Query = queryString
            }.Uri;

            var request = new HttpRequestMessage(HttpMethod.Get, uri);

            HttpResponseMessage response = null;
            await this.internalClient.RetryPolicy.RunOperation(
                async () =>
                {
                    response = await SendHttpRequest(request, cancellationToken).ConfigureAwait(false);
                }, this.internalClient.ServiceBusConnection.OperationTimeout).ConfigureAwait(false);

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
            queueDescription.NormalizeDescription(this.csBuilder.Endpoint);
            var atomRequest = queueDescription.Serialize().ToString();
            var content = await PutEntity(
                queueDescription.Path, 
                atomRequest, 
                false, 
                queueDescription.ForwardTo, 
                queueDescription.ForwardDeadLetteredMessagesTo, 
                cancellationToken).ConfigureAwait(false);
            return QueueDescription.ParseFromContent(content);
        }

        public Task<TopicDescription> CreateTopicAsync(string topicName, CancellationToken cancellationToken = default)
        {
            return this.CreateTopicAsync(new TopicDescription(topicName), cancellationToken);
        }

        public async Task<TopicDescription> CreateTopicAsync(TopicDescription topicDescription, CancellationToken cancellationToken = default)
        {
            var atomRequest = topicDescription.Serialize().ToString();
            var content = await PutEntity(topicDescription.Path, atomRequest, false, null, null, cancellationToken).ConfigureAwait(false);
            return TopicDescription.ParseFromContent(content);
        }

        public Task<SubscriptionDescription> CreateSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            return this.CreateSubscriptionAsync(new SubscriptionDescription(topicName, subscriptionName), cancellationToken);
        }

        // TODO: Expose CreateSubscriptionWithRule()
        public async Task<SubscriptionDescription> CreateSubscriptionAsync(SubscriptionDescription subscriptionDescription, CancellationToken cancellationToken = default)
        {
            subscriptionDescription.NormalizeDescription(this.csBuilder.Endpoint);
            var atomRequest = subscriptionDescription.Serialize().ToString();
            var content = await PutEntity(
                EntityNameHelper.FormatSubscriptionPath(subscriptionDescription.TopicPath, subscriptionDescription.SubscriptionName),
                atomRequest,
                false,
                subscriptionDescription.ForwardTo,
                subscriptionDescription.ForwardDeadLetteredMessagesTo,
                cancellationToken).ConfigureAwait(false);
            return SubscriptionDescription.ParseFromContent(subscriptionDescription.TopicPath, content);
        }

        public async Task<RuleDescription> CreateRuleAsync(string topicName, string subscriptionName, RuleDescription ruleDescription, CancellationToken cancellationToken = default)
        {
            ManagementClient.CheckValidTopicName(topicName);
            ManagementClient.CheckValidSubscriptionName(topicName);
            var atomRequest = ruleDescription.Serialize().ToString();
            var content = await PutEntity(
                EntityNameHelper.FormatRulePath(topicName, subscriptionName, ruleDescription.Name),
                atomRequest,
                false,
                null, 
                null,
                cancellationToken).ConfigureAwait(false);
            return RuleDescription.ParseFromContent(content);
        }

        #endregion CreateEntity

        #region UpdateEntity

        public async Task<QueueDescription> UpdateQueueAsync(QueueDescription queueDescription, CancellationToken cancellationToken = default)
        {
            queueDescription.NormalizeDescription(this.csBuilder.Endpoint);
            var atomRequest = queueDescription.Serialize().ToString();
            var content = await PutEntity(
                queueDescription.Path, 
                atomRequest, 
                true, 
                queueDescription.ForwardTo,
                queueDescription.ForwardDeadLetteredMessagesTo, 
                cancellationToken).ConfigureAwait(false);
            return QueueDescription.ParseFromContent(content);
        }

        public async Task<TopicDescription> UpdateTopicAsync(TopicDescription topicDescription, CancellationToken cancellationToken = default)
        {
            var atomRequest = topicDescription.Serialize().ToString();
            var content = await PutEntity(topicDescription.Path, atomRequest, true, null, null, cancellationToken).ConfigureAwait(false);
            return TopicDescription.ParseFromContent(content);
        }

        public async Task<SubscriptionDescription> UpdateSubscriptionAsync(SubscriptionDescription subscriptionDescription, CancellationToken cancellationToken = default)
        {
            subscriptionDescription.NormalizeDescription(this.csBuilder.Endpoint);
            var atomRequest = subscriptionDescription.Serialize().ToString();
            var content = await PutEntity(
                EntityNameHelper.FormatSubscriptionPath(subscriptionDescription.TopicPath, subscriptionDescription.SubscriptionName),
                atomRequest,
                true,
                subscriptionDescription.ForwardTo,
                subscriptionDescription.ForwardDeadLetteredMessagesTo,
                cancellationToken).ConfigureAwait(false);
            return SubscriptionDescription.ParseFromContent(subscriptionDescription.TopicPath, content);
        }

        public async Task<RuleDescription> UpdateRuleAsync(string topicName, string subscriptionName, RuleDescription ruleDescription, CancellationToken cancellationToken = default)
        {
            ManagementClient.CheckValidRuleName(ruleDescription.Name);
            var atomRequest = ruleDescription.Serialize().ToString();
            var content = await PutEntity(
                EntityNameHelper.FormatRulePath(topicName, subscriptionName, ruleDescription.Name),
                atomRequest,
                true,
                null, null,
                cancellationToken).ConfigureAwait(false);
            return RuleDescription.ParseFromContent(content);
        }

        private async Task<string> PutEntity(string path, string requestBody, bool isUpdate, string forwardTo, string fwdDeadLetterTo, CancellationToken cancellationToken)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
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
                var token = await this.internalClient.GetToken(forwardTo).ConfigureAwait(false);
                request.Headers.Add(ManagementClientConstants.ServiceBusSupplementartyAuthorizationHeaderName, token);
            }

            if (!string.IsNullOrWhiteSpace(fwdDeadLetterTo))
            {
                var token = await this.internalClient.GetToken(fwdDeadLetterTo).ConfigureAwait(false);
                request.Headers.Add(ManagementClientConstants.ServiceBusDlqSupplementaryAuthorizationHeaderName, token);
            }

            HttpResponseMessage response = null;
            await this.internalClient.RetryPolicy.RunOperation(
                async () =>
                {
                    response = await SendHttpRequest(request, cancellationToken).ConfigureAwait(false);
                }, this.internalClient.ServiceBusConnection.OperationTimeout).ConfigureAwait(false);

            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        #endregion

        #region Exists

        public async Task<bool> QueueExistsAsync(string queueName, CancellationToken cancellationToken = default)
        {
            CheckValidQueueName(queueName);
            
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
            CheckValidTopicName(topicName);
            
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
            CheckValidTopicName(topicName);
            CheckValidSubscriptionName(subscriptionName);

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

        public async Task CloseAsync()
        {
            await internalClient.CloseAsync().ConfigureAwait(false);

            if (httpClient != null)
            {
                httpClient.Dispose();
                httpClient = null;
            }
        }

        #endregion

        private async Task<HttpResponseMessage> SendHttpRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = await this.internalClient.GetToken(request.RequestUri).ConfigureAwait(false);
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
            else if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
            {
                throw new MessagingEntityNotFoundException(exceptionMessage);
            }
            else if (response.StatusCode == HttpStatusCode.Conflict)
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
                else if (exceptionMessage.Contains(ManagementClientConstants.ConflictOperationInProgressSubCode))
                {
                    throw new ServiceBusException(true, exceptionMessage);
                }
                else
                {
                    throw new MessagingEntityAlreadyExistsException(exceptionMessage);
                }
            }
            else if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                if (exceptionMessage.Contains(ManagementClientConstants.ForbiddenInvalidOperationSubCode))
                {
                    throw new InvalidOperationException(exceptionMessage);
                }
                
                throw new QuotaExceededException(exceptionMessage);
            }
            else if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new ServiceBusException(false, new ArgumentException(exceptionMessage));
            }
            else if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                throw new ServerBusyException(exceptionMessage);
            }
            else
            {
                throw new ServiceBusException(true, exceptionMessage);
            }
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
                if (detail != null)
                {
                    return detail.Value;
                }

                return content;
            }
            catch (Exception)
            {
                return content;
            }
        }

        internal static void CheckValidQueueName(string queueName, string paramName = "queueName")
        {
            CheckValidEntityName(queueName, ManagementClientConstants.QueueNameMaximumLength, true, paramName);
        }

        internal static void CheckValidTopicName(string topicName, string paramName = "topicName")
        {
            CheckValidEntityName(topicName, ManagementClientConstants.TopicNameMaximumLength, true, paramName);
        }

        internal static void CheckValidSubscriptionName(string subscriptionName, string paramName = "subscriptionName")
        {
            CheckValidEntityName(subscriptionName, ManagementClientConstants.SubscriptionNameMaximumLength, false, paramName);
        }

        internal static void CheckValidRuleName(string ruleName, string paramName = "ruleName")
        {
            CheckValidEntityName(ruleName, ManagementClientConstants.RuleNameMaximumLength, false, paramName);
        }

        private static void CheckValidEntityName(string entityName, int maxEntityNameLength, bool allowSeparator, string paramName)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                throw new ArgumentNullException(paramName);
            }

            // and "\" will be converted to "/" on the REST path anyway. Gateway/REST do not
            // have to worry about the begin/end slash problem, so this is purely a client side check.
            string tmpName = entityName.Replace(@"\", Constants.PathDelimiter);
            if (tmpName.Length > maxEntityNameLength)
            {
                throw new ArgumentOutOfRangeException(paramName, $"Entity path '{entityName}' " +
                    $"exceeds the '{maxEntityNameLength}' character limit.");
            }

            if (tmpName.StartsWith(Constants.PathDelimiter, StringComparison.Ordinal) || 
                tmpName.EndsWith(Constants.PathDelimiter, StringComparison.Ordinal))
            {
                throw new ArgumentException($"The entity name/path cannot contain '/' as " +
                    $"prefix or suffix. The supplied value is '{entityName}'", paramName);
            }

            if (!allowSeparator && tmpName.Contains(Constants.PathDelimiter))
            {
                throw new ArgumentException($"The entity name/path contains an invalid character" +
                    $" '{Constants.PathDelimiter}'", paramName);
            }

            string[] uriSchemeKeys = new string[] { "@", "?", "#" };
            foreach (var uriSchemeKey in uriSchemeKeys)
            {
                if (entityName.Contains(uriSchemeKey))
                {
                    throw new ArgumentException($"'{entityName}' contains character '{uriSchemeKey}' " +
                        $"which is not allowed because it is reserved in the Uri scheme.", paramName);
                }
            }
        }

        class InternalClient : ClientEntity
        {
            private bool ownsConnection;

            public InternalClient(string clientTypeName, string postfix, RetryPolicy retryPolicy, ServiceBusConnectionStringBuilder connectionStringBuilder) : base(clientTypeName, postfix, retryPolicy)
            {
                this.ServiceBusConnection = new ServiceBusConnection(connectionStringBuilder);
                this.ServiceBusConnection.RetryPolicy = this.RetryPolicy;
                this.ownsConnection = true;
            }

            public override ServiceBusConnection ServiceBusConnection { get; }

            public override TimeSpan OperationTimeout
            {
                get => this.ServiceBusConnection.OperationTimeout;
                set => this.ServiceBusConnection.OperationTimeout = value;
            }

            public override string Path => this.ServiceBusConnection.Endpoint.AbsoluteUri;

            public override IList<ServiceBusPlugin> RegisteredPlugins => throw new NotImplementedException($"{nameof(ManagementClient)} doesn't support plugins");

            public override void RegisterPlugin(ServiceBusPlugin serviceBusPlugin) => throw new NotImplementedException($"{nameof(ManagementClient)} doesn't support plugins");
            
            public override void UnregisterPlugin(string serviceBusPluginName) => throw new NotImplementedException($"{nameof(ManagementClient)} doesn't support plugins");

            protected async override Task OnClosingAsync()
            {
                if (this.ownsConnection)
                {
                    await this.ServiceBusConnection.CloseAsync().ConfigureAwait(false);
                }
            }

            // TODO: Operation timeout as token timeout??? :O
            // TODO: token caching?
            public Task<string> GetToken(Uri requestUri)
            {
                return this.GetToken(requestUri.GetLeftPart(UriPartial.Path));
            }

            public async Task<string> GetToken(string requestUri)
            {
                var token = await this.ServiceBusConnection.TokenProvider.GetTokenAsync(requestUri, this.ServiceBusConnection.OperationTimeout).ConfigureAwait(false);
                return token.TokenValue;
            }
        }
    }
}
