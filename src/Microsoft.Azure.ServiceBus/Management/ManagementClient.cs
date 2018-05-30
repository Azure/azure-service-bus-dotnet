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
    public class ManagementClient : ClientEntity
    {
        // TODO: Maybe move to ManagementConstants.cs
        internal const string AtomNs = "http://www.w3.org/2005/Atom";
        internal const string SbNs = "http://schemas.microsoft.com/netservices/2010/10/servicebus/connect";

        private bool ownsConnection;
        private HttpClient httpClient;
        private ServiceBusConnectionStringBuilder csBuilder;
        private string namespaceConnectionString;
        private const string apiVersionQuery = "api-version=2017-04";
        
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

        public override string Path => null;

        public override IList<ServiceBusPlugin> RegisteredPlugins => null;

        public override void RegisterPlugin(ServiceBusPlugin serviceBusPlugin)
        {
            throw new NotImplementedException($"{nameof(ManagementClient)} doesn't support plugins");
        }

        public override void UnregisterPlugin(string serviceBusPluginName)
        {
            throw new NotImplementedException($"{nameof(ManagementClient)} doesn't support plugins");
        }

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
                "application/atom+xml"
            );
            
            var response = await PutHttpRequest(request, uri, cancellationToken);
            return QueueDescription.ParseFromContent(response);
        }

        public async Task<QueueDescription> GetQueueAsync(string queueName, bool includeRuntimeInfo = false, CancellationToken cancellationToken = default)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = queueName,
                Scheme = Uri.UriSchemeHttps,
                Port = GetPort(this.csBuilder.Endpoint),
                Query = $"{apiVersionQuery}&enrich={includeRuntimeInfo}"
            }.Uri;

            var content = await GetHttpContent(uri, cancellationToken);            
            return QueueDescription.ParseFromContent(content);
        }

        public async Task<IList<string>> GetQueuesAsync(int count = 100, CancellationToken cancellationToken = default)
        {
            var uri = new UriBuilder(this.csBuilder.Endpoint)
            {
                Path = "$Resources/queues",
                Scheme = Uri.UriSchemeHttps,
                Port = GetPort(this.csBuilder.Endpoint),
                Query = $"{apiVersionQuery}&enrich=false"
            }.Uri;

            var content = await GetHttpContent(uri, cancellationToken);
            return QueueDescription.ParseCollectionFromContent(content).Select(qd => qd.Path).ToList();
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
        private async Task<string> GetHttpContent(Uri uri, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var token = await GetToken(request.RequestUri);
            request.Headers.Add("Authorization", token);
            HttpResponseMessage response;
            response = await this.httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }

        // TODO: Exception handling
        private async Task<string> PutHttpRequest(HttpRequestMessage message, Uri uri, CancellationToken cancellationToken)
        {
            var token = await GetToken(message.RequestUri);
            message.Headers.Add("Authorization", token);
            var response = await this.httpClient.SendAsync(message, cancellationToken);
            var content = await response.Content.ReadAsStringAsync();
            return content;
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
