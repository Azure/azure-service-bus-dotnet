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
    public class ManagementClient : ClientEntity, IManagementClient
    {
        // TODO: Move to ManagementConstants.cs
        internal const string AtomNs = "http://www.w3.org/2005/Atom";
        internal const string SbNs = "http://schemas.microsoft.com/netservices/2010/10/servicebus/connect";
        internal const string XmlSchemaNs = "http://www.w3.org/2001/XMLSchema-instance";
        internal const string AtomContentType = "application/atom+xml";
        internal const string apiVersionQuery = "api-version=" + ApiVersion;
        internal const string ApiVersion = "2017-04";

        private bool ownsConnection;
        private HttpClient httpClient;
        private ServiceBusConnectionStringBuilder csBuilder;
        private string namespaceConnectionString;
        
        // TODO: Expose other constructor overloads
        public ManagementClient(ServiceBusConnectionStringBuilder connectionStringBuilder)
            : base(nameof(ManagementClient), string.Empty, new NoRetry())
        {
            this.csBuilder = connectionStringBuilder;
            this.namespaceConnectionString = connectionStringBuilder.GetNamespaceConnectionString();
            this.ServiceBusConnection = new ServiceBusConnection(connectionStringBuilder);
            this.ownsConnection = true;
            this.httpClient = new HttpClient();
        }

        public override ServiceBusConnection ServiceBusConnection { get; }

        public override TimeSpan OperationTimeout
        {
            get => this.ServiceBusConnection.OperationTimeout;
            set => this.ServiceBusConnection.OperationTimeout = value;
        }

        public override string Path => this.ServiceBusConnection.Endpoint.AbsoluteUri;

        public override IList<ServiceBusPlugin> RegisteredPlugins => null;

        public override void RegisterPlugin(ServiceBusPlugin serviceBusPlugin)
        {
            throw new NotImplementedException($"{nameof(ManagementClient)} doesn't support plugins");
        }

        public override void UnregisterPlugin(string serviceBusPluginName)
        {
            throw new NotImplementedException($"{nameof(ManagementClient)} doesn't support plugins");
        }

        #region CreateEntity

        public Task<QueueDescription> CreateQueueAsync(string queueName, CancellationToken cancellationToken = default)
        {
            return this.CreateQueueAsync(new QueueDescription(queueName), cancellationToken);
        }

        public async Task<QueueDescription> CreateQueueAsync(QueueDescription queueDescription, CancellationToken cancellationToken = default)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = queueDescription.Path,
                Port = GetPort(this.csBuilder.Endpoint),
                Scheme = Uri.UriSchemeHttps,
                Query = $"{apiVersionQuery}"
            }.Uri;

            var atomRequest = queueDescription.Serialize().ToString();

            var request = new HttpRequestMessage(HttpMethod.Put, uri);
            request.Content = new StringContent(
                atomRequest,
                Encoding.UTF8,
                ManagementClient.AtomContentType
            );

            var response = await SendHttpRequest(request, cancellationToken);
            // TODO: non success status code?
            if (response.StatusCode == System.Net.HttpStatusCode.Created)
            {
                var content = await response.Content.ReadAsStringAsync();
                return QueueDescription.ParseFromContent(content);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public Task<TopicDescription> CreateTopicAsync(string topicName, CancellationToken cancellationToken = default)
        {
            return this.CreateTopicAsync(new TopicDescription(topicName), cancellationToken);
        }

        // TODO: See if can reuse code in CreateQueue
        public async Task<TopicDescription> CreateTopicAsync(TopicDescription topicDescription, CancellationToken cancellationToken = default)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = topicDescription.Path,
                Port = GetPort(this.csBuilder.Endpoint),
                Scheme = Uri.UriSchemeHttps,
                Query = $"{apiVersionQuery}"
            }.Uri;

            var atomRequest = topicDescription.Serialize().ToString();

            var request = new HttpRequestMessage(HttpMethod.Put, uri);
            request.Content = new StringContent(
                atomRequest,
                Encoding.UTF8,
                ManagementClient.AtomContentType
            );

            var response = await SendHttpRequest(request, cancellationToken);
            // TODO: what about non success status code?
            if (response.StatusCode == System.Net.HttpStatusCode.Created)
            {
                var content = await response.Content.ReadAsStringAsync();
                return TopicDescription.ParseFromContent(content);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public Task<SubscriptionDescription> CreateSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            return this.CreateSubscriptionAsync(new SubscriptionDescription(topicName, subscriptionName), cancellationToken);
        }

        public async Task<SubscriptionDescription> CreateSubscriptionAsync(SubscriptionDescription subscriptionDescription, CancellationToken cancellationToken = default)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = EntityNameHelper.FormatSubscriptionPath(subscriptionDescription.TopicPath, subscriptionDescription.SubscriptionName),
                Port = GetPort(this.csBuilder.Endpoint),
                Scheme = Uri.UriSchemeHttps,
                Query = $"{apiVersionQuery}"
            }.Uri;

            var atomRequest = subscriptionDescription.Serialize().ToString();

            var request = new HttpRequestMessage(HttpMethod.Put, uri);
            request.Content = new StringContent(
                atomRequest,
                Encoding.UTF8,
                ManagementClient.AtomContentType
            );

            var response = await SendHttpRequest(request, cancellationToken);
            
            // TODO: what about non success status code?
            if (response.StatusCode == System.Net.HttpStatusCode.Created)
            {
                var content = await response.Content.ReadAsStringAsync();
                return SubscriptionDescription.ParseFromContent(subscriptionDescription.TopicPath, content); 
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        // TODO
        public Task<RuleDescription> CreateRuleAsync(string topicName, string subscriptionName, RuleDescription ruleDescription, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        #endregion CreateEntity

        #region DeleteEntity

        public async Task DeleteQueueAsync(string queueName, CancellationToken cancellationToken = default)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = queueName,
                Scheme = Uri.UriSchemeHttps,
                Port = GetPort(this.csBuilder.Endpoint),
                Query = $"{apiVersionQuery}&enrich=false"
            }.Uri;

            var request = new HttpRequestMessage(HttpMethod.Delete, uri);
            await SendHttpRequest(request, cancellationToken);
        }

        public Task DeleteTopicAsync(string topicName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task DeleteRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region GetEntity

        public async Task<QueueDescription> GetQueueAsync(string queueName, CancellationToken cancellationToken = default)
        {
            CheckValidEntityName(queueName, ManagementConstants.QueueNameMaximumLength, true, nameof(queueName));
            var content = await GetEntity(queueName, false, cancellationToken);
            return QueueDescription.ParseFromContent(content);
        }

        public async Task<TopicDescription> GetTopicAsync(string topicName, CancellationToken cancellationToken = default)
        {
            var content = await GetEntity(topicName, false, cancellationToken);
            return TopicDescription.ParseFromContent(content);
        }

        public async Task<SubscriptionDescription> GetSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            var content = await GetEntity(EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName), false, cancellationToken);
            return SubscriptionDescription.ParseFromContent(topicName, content);
        }

        public Task<RuleDescription> GetRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region GetRuntimeInfo

        public async Task<QueueRuntimeInfo> GetQueueRuntimeInfoAsync(string queueName, CancellationToken cancellationToken = default)
        {
            var content = await GetEntity(queueName, true, cancellationToken);
            return QueueRuntimeInfo.ParseFromContent(content);
        }

        public async Task<TopicRuntimeInfo> GetTopicRuntimeInfoAsync(string topicName, CancellationToken cancellationToken = default)
        {
            var content = await GetEntity(topicName, true, cancellationToken);
            return TopicRuntimeInfo.ParseFromContent(content);
        }

        public async Task<SubscriptionRuntimeInfo> GetSubscriptionRuntimeInfoAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            var content = await GetEntity(EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName), true, cancellationToken);
            return SubscriptionRuntimeInfo.ParseFromContent(topicName, content);
        }

        #endregion

        #region GetEntities

        // TODO: pass skip and top
        public async Task<IList<QueueDescription>> GetQueuesAsync(int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            var content = await GetEntity("$Resources/queues", false, cancellationToken);
            return QueueDescription.ParseCollectionFromContent(content);
        }

        public async Task<IList<TopicDescription>> GetTopicsAsync(int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            var content = await GetEntity("$Resources/topics", false, cancellationToken);
            return TopicDescription.ParseCollectionFromContent(content);
        }

        public async Task<IList<SubscriptionDescription>> GetSubscriptionsAsync(string topicName, int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            var content = await GetEntity($"{topicName}/Subscriptions", false, cancellationToken);
            return SubscriptionDescription.ParseCollectionFromContent(topicName, content);
        }

        public async Task<IList<RuleDescription>> GetRulesAsync(string topicName, string subscriptionName, int count = 100, int skip = 0, CancellationToken cancellationToken = default)
        {
            var content = await GetEntity($"{topicName}/Subscriptions/{subscriptionName}/rules", false, cancellationToken);
            return RuleDescription.ParseCollectionFromContent(content);
        }

        private async Task<string> GetEntity(string path, bool enrich, CancellationToken cancellationToken)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = path,
                Scheme = Uri.UriSchemeHttps,
                Port = GetPort(this.csBuilder.Endpoint),
                Query = $"{apiVersionQuery}&enrich={enrich}"
            }.Uri;

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await SendHttpRequest(request, cancellationToken);

            // TODO: what about non success status code?
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        #endregion

        #region UpdateEntity

        public Task<QueueDescription> UpdateQueueAsync(QueueDescription queueDescription, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<TopicDescription> UpdateTopicAsync(TopicDescription topicDescription, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<SubscriptionDescription> UpdateSubscriptionAsync(SubscriptionDescription subscriptionDescription, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Exists

        public Task<bool> QueueExistsAsync(string queueName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> TopicExistsAsync(string topicName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SubscriptionExistsAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        #endregion

        protected async override Task OnClosingAsync()
        {
            if (this.ownsConnection)
            {
                await this.ServiceBusConnection.CloseAsync().ConfigureAwait(false);
            }

            if (httpClient != null)
            {
                httpClient.Dispose();
                httpClient = null;
            }
        }

        private string EncodeToAtom(XElement objectToEncode)
        {
            XDocument doc = new XDocument(
               new XElement(XName.Get("entry", ManagementClient.AtomNs),
                   new XElement(XName.Get("content", ManagementClient.AtomNs),
                   objectToEncode
                   )));

            return doc.ToString();
        }

        // TODO: Exception handling
        private async Task<HttpResponseMessage> SendHttpRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = await GetToken(request.RequestUri);
            request.Headers.Add("Authorization", token);
            request.Headers.Add("UserAgent", $"SERVICEBUS/{ManagementClient.ApiVersion}(api-origin={ClientInfo.Framework};os={ClientInfo.Platform};version={ClientInfo.Version};product={ClientInfo.Product})");
            // Tracking ID + CorrelationID
            HttpResponseMessage response;
            try
            {
                response = await this.httpClient.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                throw new ServiceBusException(true, exception);
            }

            ValidateHttpResponse(response);
            return response;
        }

        // TODO: Operation timeout as token timeout??? :O
        // TODO: token caching?
        private async Task<string> GetToken(Uri requestUri)
        {
            var token = await this.ServiceBusConnection.TokenProvider.GetTokenAsync(requestUri.GetLeftPart(UriPartial.Path), this.ServiceBusConnection.OperationTimeout);
            return token.TokenValue;
        }

        private int GetPort(string endpoint)
        {
            // used for manual testing
            if (endpoint.EndsWith("onebox.windows-int.net"))
            {
                return 4446;
            }

            return -1;
        }

        private async void ValidateHttpResponse(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var content = await response.Content.ReadAsStringAsync() ?? string.Empty;
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new UnauthorizedException(content);
            }
            else if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
            {
                throw new MessagingEntityNotFoundException(content);
            }
            else if (response.StatusCode == HttpStatusCode.Conflict && response.RequestMessage.Method == HttpMethod.Put)
            {
                // TODO: What if Update() is in conflict
                throw new MessagingEntityAlreadyExistsException(content);
            }
            else if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException(content);
                // TODO: Isolate cases when it needs to be QUotaExceededException
            }
            else if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new ArgumentException(content);
            }
            else if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                throw new ServerBusyException(content);
            }
            else
            {
                throw new ServiceBusException(true, content);
            }
        }

        static void CheckValidEntityName(string entityName, int maxEntityNameLength, bool allowSeparator, string paramName)
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
    }
}
