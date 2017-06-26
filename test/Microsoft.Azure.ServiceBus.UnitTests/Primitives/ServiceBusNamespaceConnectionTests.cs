using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus.UnitTests.Primitives
{
    using Xunit;
    public class ServiceBusNamespaceConnectionTests
    {
        private const string EndpointUri = "sb://land-of-oz.servicebus.windows.net/";
        private const string SasKeyName = "RootManageSharedAccessKey";
        private const string SasKey = "7ry17m@yb31tw1llw0rk=";
        private static readonly string NamespaceConnectionString = $"Endpoint={EndpointUri};SharedAccessKeyName={SasKeyName};SharedAccessKey={SasKey}";

        [Fact]
        public void Returns_endpoint_with_proper_uri_scheme()
        {
            var namespaceConnection = new ServiceBusNamespaceConnection(NamespaceConnectionString);
            Assert.Equal("amqps://land-of-oz.servicebus.windows.net/", namespaceConnection.Endpoint.ToString());
        }

        [Fact]
        public void Returns_shared_access_key_name()
        {
            var namespaceConnection = new ServiceBusNamespaceConnection(NamespaceConnectionString);
            Assert.Equal(SasKeyName, namespaceConnection.SasKeyName);
        }

        [Fact]
        public void Returns_shared_access_key()
        {
            var namespaceConnection = new ServiceBusNamespaceConnection(NamespaceConnectionString);
            Assert.Equal(SasKey, namespaceConnection.SasKey);
        }
    }
}