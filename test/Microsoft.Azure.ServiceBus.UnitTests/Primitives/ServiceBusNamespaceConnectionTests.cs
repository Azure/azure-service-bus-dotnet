using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus.UnitTests.Primitives
{
    using Xunit;
    public class ServiceBusNamespaceConnectionTests
    {
        [Fact]
        public void Can_return_namespace_name_for_connection_string()
        {
            var namespaceConnection = new ServiceBusNamespaceConnection("Endpoint=sb://seanfeldman-test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=7ry17m@yb31tw1llw0rk=");
            Assert.Equal("seanfeldman-test", namespaceConnection.NamespaceName);
        }
    }
}