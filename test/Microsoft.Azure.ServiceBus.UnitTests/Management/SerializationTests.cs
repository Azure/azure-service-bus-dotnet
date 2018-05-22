using Xunit;
using Microsoft.Azure.ServiceBus.Management;
using System;

namespace Microsoft.Azure.ServiceBus.UnitTests.Management
{
    // TODO: Asserts
    // Tests with XML encoded letters in queue name etc.
    public class SerializationTests
    {
        [Fact]
        public async void GetQueue()
        {
            var client = new ManagementClient(new ServiceBusConnectionStringBuilder(TestUtility.NamespaceConnectionString));
            var qd = await client.GetQueueAsync(TestConstants.NonPartitionedQueueName);
        }

        [Fact]
        public async void GetQueues()
        {
            var client = new ManagementClient(new ServiceBusConnectionStringBuilder(TestUtility.NamespaceConnectionString));
            var queues = await client.GetQueuesAsync();
        }

        [Fact]
        public void CreateQueue()
        {
            var qd = new QueueDescription(Guid.NewGuid().ToString("N"));
            qd.EnableBatchedOperations = true;

            var ser = qd.Serialize().ToString();

        }
    }
}
