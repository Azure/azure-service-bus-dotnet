using Xunit;
using Microsoft.Azure.ServiceBus.Management;
using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.ServiceBus.UnitTests.Management
{
    // TODO
    // Tests with XML encoded letters in queue name etc.
    // Negative scenarios
    // Test with mix and match default values and set values.
    // What if default value in service is different from client.
    // Update non-updatable property
    // QuotaExceededException
    // Verify Runtime count
    // Get more than 100 queues - manual test.
    // entity name validation tests
    public class ManagementClientTests : IDisposable
    {
        internal string ConnectionString = TestUtility.NamespaceConnectionString;
        //internal string ConnectionString = "Endpoint=sb://contoso.servicebus.onebox.windows-int.net/;SharedAccessKeyName=DefaultNamespaceSasAllKeyName;SharedAccessKey=8864/auVd3qDC75iTjBL1GJ4D2oXC6bIttRd0jzDZ+g=";
        ManagementClient client;

        public ManagementClientTests()
        {
            client = new ManagementClient(new ServiceBusConnectionStringBuilder(ConnectionString));
        }

        [Fact]
        public async Task BasicQueueCrudTest()
        {
            var queueName = Guid.NewGuid().ToString("D").Substring(0, 8);

            var qd = new QueueDescription(queueName)
            {
                AutoDeleteOnIdle = TimeSpan.FromHours(1),
                DefaultMessageTimeToLive = TimeSpan.FromDays(2),
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(1),
                EnableBatchedOperations = true,
                EnableDeadLetteringOnMessageExpiration = true,
                EnablePartitioning = false,
                ForwardDeadLetteredMessagesTo = null,
                ForwardTo = null,
                LockDuration = TimeSpan.FromSeconds(45),
                MaxDeliveryCount = 8,
                MaxSizeInMegabytes = 2048,
                RequiresDuplicateDetection = true,
                RequiresSession = true
            };

            var finalQ = await client.CreateQueueAsync(qd);
            Assert.Equal(qd, finalQ);

            var getQ = await client.GetQueueAsync(qd.Path);
            Assert.Equal(qd, getQ);

            getQ.EnableBatchedOperations = false;
            getQ.MaxDeliveryCount = 9;

            var updatedQ = await client.UpdateQueueAsync(getQ);
            Assert.Equal(getQ, updatedQ);

            await client.DeleteQueueAsync(updatedQ.Path);

            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                    async () =>
                    {
                        await client.GetQueueAsync(qd.Path);
                    });
        }

        [Fact]
        public async Task BasicTopicCrudTest()
        {
            var topicName = Guid.NewGuid().ToString("D").Substring(0, 8);

            var td = new TopicDescription(topicName)
            {
                AutoDeleteOnIdle = TimeSpan.FromHours(1),
                DefaultMessageTimeToLive = TimeSpan.FromDays(2),
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(1),
                EnableBatchedOperations = true,
                EnablePartitioning = false,
                MaxSizeInMegabytes = 2048,
                RequiresDuplicateDetection = true
            };

            var createdT = await client.CreateTopicAsync(td);
            Assert.Equal(td, createdT);

            var getT = await client.GetTopicAsync(td.Path);
            Assert.Equal(td, getT);

            getT.EnableBatchedOperations = false;
            getT.DefaultMessageTimeToLive = TimeSpan.FromDays(3);

            var updatedT = await client.UpdateTopicAsync(getT);
            Assert.Equal(getT, updatedT);

            await client.DeleteTopicAsync(updatedT.Path);

            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                    async () =>
                    {
                        await client.GetTopicAsync(td.Path);
                    });
        }

        [Fact]
        public async Task BasicSubscriptionCrudTest()
        {
            var topicName = Guid.NewGuid().ToString("D").Substring(0, 8);
            await client.CreateTopicAsync(topicName);

            var subscriptionName = Guid.NewGuid().ToString("D").Substring(0, 8);
            var sd = new SubscriptionDescription(topicName, subscriptionName)
            {
                AutoDeleteOnIdle = TimeSpan.FromHours(1),
                DefaultMessageTimeToLive = TimeSpan.FromDays(2),
                EnableDeadLetteringOnMessageExpiration = true,
                ForwardDeadLetteredMessagesTo = null,
                ForwardTo = null,
                LockDuration = TimeSpan.FromSeconds(45),
                MaxDeliveryCount = 8,
                RequiresSession = true
            };

            var createdS = await client.CreateSubscriptionAsync(sd);
            //var createdS2 = await client.UpdateSubscriptionAsync(sd);
            Assert.Equal(sd, createdS);

            var getS = await client.GetSubscriptionAsync(sd.TopicPath, sd.SubscriptionName);
            Assert.Equal(sd, getS);

            getS.DefaultMessageTimeToLive = TimeSpan.FromDays(3);
            getS.MaxDeliveryCount = 9;

            var updatedS = await client.UpdateSubscriptionAsync(getS);
            Assert.Equal(getS, updatedS);

            await client.DeleteSubscriptionAsync(sd.TopicPath, sd.SubscriptionName);

            await Assert.ThrowsAsync<MessagingEntityNotFoundException>(
                    async () =>
                    {
                        await client.GetSubscriptionAsync(sd.TopicPath, sd.SubscriptionName);
                    });

            await client.DeleteTopicAsync(sd.TopicPath);
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
            var subscriptions = await client.GetSubscriptionsAsync(TestConstants.NonPartitionedTopicName);
        }

        [Fact]
        public async void GetRule()
        {
            var rule = await client.GetRuleAsync("mytopic", "sub1", "rule1");
        }

        [Fact]
        public async void GetRules()
        {
            var rules = await client.GetRulesAsync("mytopic", "sub1");
        }

        public void Dispose()
        {
            client.CloseAsync().Wait();
        }
    }
}
