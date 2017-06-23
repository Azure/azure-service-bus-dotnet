using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus.UnitTests.Primitives
{
    using Xunit;
    public class ServiceBusNamespaceConnectionTests
    {
        [Fact]
        public void Can_return_namespace_name_for_connection_string()
        {
            var namespaceConnection = new ServiceBusNamespaceConnection("Endpoint=sb://land-of-oz.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=7ry17m@yb31tw1llw0rk=");
            Assert.Equal("land-of-oz", namespaceConnection.NamespaceName);
        }
    }
}