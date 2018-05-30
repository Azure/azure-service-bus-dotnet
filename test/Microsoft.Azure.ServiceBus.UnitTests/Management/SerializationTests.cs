using Xunit;
using Microsoft.Azure.ServiceBus.Management;
using System;

namespace Microsoft.Azure.ServiceBus.UnitTests.Management
{
    // TODO: Asserts
    // Tests with XML encoded letters in queue name etc.
    public class SerializationTests
    {
        //internal string ConnectionString = TestUtility.NamespaceConnectionString;
        internal string ConnectionString = "Endpoint=sb://contoso.servicebus.onebox.windows-int.net/;SharedAccessKeyName=DefaultNamespaceSasAllKeyName;SharedAccessKey=8864/auVd3qDC75iTjBL1GJ4D2oXC6bIttRd0jzDZ+g=";

        [Fact]
        public async void GetQueue()
        {
            var client = new ManagementClient(new ServiceBusConnectionStringBuilder(ConnectionString));
            var qd = await client.GetQueueAsync(TestConstants.NonPartitionedQueueName);
        }

        [Fact]
        public async void GetQueues()
        {
            var client = new ManagementClient(new ServiceBusConnectionStringBuilder(ConnectionString));
            var queues = await client.GetQueuesAsync();
        }

        [Fact]
        public async void CreateQueue()
        {
            var qd = new QueueDescription("queue2");
            qd.EnableBatchedOperations = true;

            var client = new ManagementClient(new ServiceBusConnectionStringBuilder(ConnectionString));
            var queue = await client.CreateQueueAsync(qd.Path);
        }
    }
}
