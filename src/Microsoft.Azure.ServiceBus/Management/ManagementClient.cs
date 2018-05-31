using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Azure.ServiceBus.Core;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class ManagementClient : ClientEntity, IManagementClient
    {
        // TODO: Maybe move to ManagementConstants.cs
        internal const string AtomNs = "http://www.w3.org/2005/Atom";
        internal const string SbNs = "http://schemas.microsoft.com/netservices/2010/10/servicebus/connect";
        internal const string AtomContentType = "application/atom+xml";
        internal const string apiVersionQuery = "api-version=2017-04";

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

        public async Task<QueueDescription> GetQueueAsync(string queueName, CancellationToken cancellationToken = default)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = queueName,
                Scheme = Uri.UriSchemeHttps,
                Port = GetPort(this.csBuilder.Endpoint),
                Query = $"{apiVersionQuery}&enrich=false"
            }.Uri;

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await SendHttpRequest(request, cancellationToken);

            // TODO: what about non success status code?
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                return QueueDescription.ParseFromContent(content);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public async Task<QueueRuntimeInfo> GetQueueRuntimeInfoAsync(string queueName, CancellationToken cancellationToken = default)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = queueName,
                Scheme = Uri.UriSchemeHttps,
                Port = GetPort(this.csBuilder.Endpoint),
                Query = $"{apiVersionQuery}&enrich=true"
            }.Uri;

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await SendHttpRequest(request, cancellationToken);

            // TODO: what about non success status code?
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                return QueueRuntimeInfo.ParseFromContent(content);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public async Task<IList<QueueDescription>> GetQueuesAsync(int count = 100, CancellationToken cancellationToken = default)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = "$Resources/queues",
                Scheme = Uri.UriSchemeHttps,
                Port = GetPort(this.csBuilder.Endpoint),
                Query = $"{apiVersionQuery}&enrich=false"
            }.Uri;

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await SendHttpRequest(request, cancellationToken);

            // TODO: what about non success status code?
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                return QueueDescription.ParseCollectionFromContent(content);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public async Task<TopicDescription> GetTopicAsync(string topicName, CancellationToken cancellationToken = default)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = topicName,
                Scheme = Uri.UriSchemeHttps,
                Port = GetPort(this.csBuilder.Endpoint),
                Query = $"{apiVersionQuery}&enrich=false"
            }.Uri;

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await SendHttpRequest(request, cancellationToken);

            // TODO: what about non success status code?
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                return TopicDescription.ParseFromContent(content);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public async Task<IList<TopicDescription>> GetTopicsAsync(int count = 100, CancellationToken cancellationToken = default)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = "$Resources/topics",
                Scheme = Uri.UriSchemeHttps,
                Port = GetPort(this.csBuilder.Endpoint),
                Query = $"{apiVersionQuery}&enrich=false"
            }.Uri;

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await SendHttpRequest(request, cancellationToken);

            // TODO: what about non success status code?
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                return TopicDescription.ParseCollectionFromContent(content);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public async Task<SubscriptionDescription> GetSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = $"{topicName}/Subscriptions/{subscriptionName}",
                Scheme = Uri.UriSchemeHttps,
                Port = GetPort(this.csBuilder.Endpoint),
                Query = $"{apiVersionQuery}&enrich=false"
            }.Uri;

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await SendHttpRequest(request, cancellationToken);

            // TODO: what about non success status code?
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                return SubscriptionDescription.ParseFromContent(topicName, content);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public async Task<IList<SubscriptionDescription>> GetSubscriptionsAsync(string topicName, int count = 100, CancellationToken cancellationToken = default)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = $"{topicName}/Subscriptions",
                Scheme = Uri.UriSchemeHttps,
                Port = GetPort(this.csBuilder.Endpoint),
                Query = $"{apiVersionQuery}&enrich=false"
            }.Uri;

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await SendHttpRequest(request, cancellationToken);

            // TODO: what about non success status code?
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                return SubscriptionDescription.ParseCollectionFromContent(topicName, content);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public Task<SubscriptionDescription> GetSubscriptionAsync(string formattedSubscriptionPath, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<RuleDescription> GetRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<TopicRuntimeInfo> GetTopicRuntimeInfoAsync(string topicName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<SubscriptionRuntimeInfo> GetSubscriptionRuntimeInfoAsync(string formattedSubscriptionPath, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<SubscriptionRuntimeInfo> GetSubscriptionRuntimeInfoAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        Task<ICollection<string>> IManagementClient.GetQueuesAsync(int count, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<string>> GetQueuesAsync(int skip, int count, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        Task<ICollection<string>> IManagementClient.GetTopicsAsync(int count, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<string>> GetTopicsAsync(int skip, int count, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        Task<ICollection<string>> IManagementClient.GetSubscriptionsAsync(string topicName, int count, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<string>> GetSubscriptionsAsync(string topicName, int skip, int count, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<RuleDescription>> GetRulesAsync(string topicName, string subscriptionName, int count = 100, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<RuleDescription>> GetRulesAsync(string topicName, string subscriptionName, int skip, int count, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

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
            var response = await this.httpClient.SendAsync(request, cancellationToken);
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
            if (endpoint.EndsWith("onebox.windows-int.net"))
            {
                return 4446;
            }

            return -1;
        }

    }
}
