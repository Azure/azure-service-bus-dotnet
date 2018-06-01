using Xunit;
using Microsoft.Azure.ServiceBus.Management;
using System;

namespace Microsoft.Azure.ServiceBus.UnitTests.Management
{
    // TODO: Asserts
    // Tests with XML encoded letters in queue name etc.
    // Negative scenarios
    public class SerializationTests : IDisposable
    {
        internal string ConnectionString = TestUtility.NamespaceConnectionString;
        //internal string ConnectionString = "Endpoint=sb://contoso.servicebus.onebox.windows-int.net/;SharedAccessKeyName=DefaultNamespaceSasAllKeyName;SharedAccessKey=8864/auVd3qDC75iTjBL1GJ4D2oXC6bIttRd0jzDZ+g=";
        ManagementClient client;

        public SerializationTests()
        {
            client = new ManagementClient(new ServiceBusConnectionStringBuilder(ConnectionString));
        }

        [Fact]
        public async void GetQueue()
        {
            var qd = await client.GetQueueAsync("queue22");
        }

        [Fact]
        public async void GetQueueRuntimeInfo()
        {
            var qd = await client.GetQueueRuntimeInfoAsync(TestConstants.NonPartitionedQueueName);
        }

        [Fact]
        public async void GetQueues()
        {
            var queues = await client.GetQueuesAsync();
        }

        [Fact]
        public async void CreateQueue()
        {
            var qd = new QueueDescription("queue2");
            var queue = await client.CreateQueueAsync(qd.Path);
        }

        [Fact]
        public async void GetTopic()
        {
            var td = await client.GetTopicAsync(TestConstants.NonPartitionedTopicName);
        }

        [Fact]
        public async void GetTopics()
        {
            var topics = await client.GetTopicsAsync();
        }

        [Fact]
        public async void GetSubscription()
        {
            var sd = await client.GetSubscriptionAsync("mytopic", "sub1");
        }

        [Fact]
        public async void GetSubscriptions()
        {
            var subscriptions = await client.GetSubscriptionsAsync(TestConstants.NonPartitionedTopicName);// "mytopic");
        }

        [Fact]
        public async void GetRules()
        {
            var rules = await client.GetRulesAsync("mytopic", "sub1");
        }

        [Fact]
        public async void DeleteQueue()
        {
            await client.DeleteQueueAsync("queue2");
        }

        public void Dispose()
        {
            client.CloseAsync().Wait();
        }
    }
}
