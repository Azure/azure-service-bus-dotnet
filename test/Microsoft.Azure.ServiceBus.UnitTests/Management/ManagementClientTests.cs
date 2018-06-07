using Xunit;
using Microsoft.Azure.ServiceBus.Management;
using System;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus.Core;

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
        [DisplayTestMethodName]
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
        [DisplayTestMethodName]
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
        [DisplayTestMethodName]
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
        [DisplayTestMethodName]
        public async void GetQueueRuntimeInfoTest()
        {
            var queueName = Guid.NewGuid().ToString("D").Substring(0, 8);

            // Fixing Created Time
            var qd = await client.CreateQueueAsync(queueName);

            // Changing Last Updated Time
            qd.AutoDeleteOnIdle = TimeSpan.FromMinutes(100);
            var updatedQ = await client.UpdateQueueAsync(qd);

            // Populating 1 active message, 1 dead letter message and 1 scheduled message
            // Changing Last Accessed Time
            var qClient = new QueueClient(this.ConnectionString, queueName);
            await qClient.SendAsync(new Message() { MessageId = "1" });
            await qClient.SendAsync(new Message() { MessageId = "2" });
            await qClient.SendAsync(new Message() { MessageId = "3", ScheduledEnqueueTimeUtc = DateTime.UtcNow.AddDays(1) });
            var msg = await qClient.InnerReceiver.ReceiveAsync();
            await qClient.DeadLetterAsync(msg.SystemProperties.LockToken);

            var runtimeInfo = await client.GetQueueRuntimeInfoAsync(queueName);

            Assert.Equal(queueName, runtimeInfo.Path);
            Assert.True(runtimeInfo.CreatedAt < runtimeInfo.UpdatedAt);
            Assert.True(runtimeInfo.UpdatedAt < runtimeInfo.AccessedAt);
            Assert.Equal(1, runtimeInfo.MessageCountDetails.ActiveMessageCount);
            Assert.Equal(1, runtimeInfo.MessageCountDetails.DeadLetterMessageCount);
            Assert.Equal(1, runtimeInfo.MessageCountDetails.ScheduledMessageCount);
            Assert.Equal(3, runtimeInfo.MessageCount);
            Assert.True(runtimeInfo.SizeInBytes > 0);

            await client.DeleteQueueAsync(queueName);
            await qClient.CloseAsync();
        }

        [Fact]
        [DisplayTestMethodName]
        public async void GetTopicAndSubscriptionRuntimeInfoTest()
        {
            var topicName = Guid.NewGuid().ToString("D").Substring(0, 8);
            var td = await client.CreateTopicAsync(topicName);

            // Changing Last Updated Time
            td.AutoDeleteOnIdle = TimeSpan.FromMinutes(100);
            var updatedT = await client.UpdateTopicAsync(td);

            var subscriptionName = Guid.NewGuid().ToString("D").Substring(0, 8);
            var sd = await client.CreateSubscriptionAsync(topicName, subscriptionName);

            // Changing Last Updated Time for subscription
            sd.AutoDeleteOnIdle = TimeSpan.FromMinutes(100);
            var updatedS = await client.UpdateSubscriptionAsync(sd);

            // Populating 1 active message, 1 dead letter message and 1 scheduled message
            // Changing Last Accessed Time
            var sender = new MessageSender(this.ConnectionString, topicName);
            var receiver = new MessageReceiver(this.ConnectionString, EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName));
            await sender.SendAsync(new Message() { MessageId = "1" });
            await sender.SendAsync(new Message() { MessageId = "2" });
            await sender.SendAsync(new Message() { MessageId = "3", ScheduledEnqueueTimeUtc = DateTime.UtcNow.AddDays(1) });
            var msg = await receiver.ReceiveAsync();
            await receiver.DeadLetterAsync(msg.SystemProperties.LockToken);

            var topicRI = await client.GetTopicRuntimeInfoAsync(topicName);
            var subscriptionRI = await client.GetSubscriptionRuntimeInfoAsync(topicName, subscriptionName);

            Assert.Equal(topicName, topicRI.Path);
            Assert.Equal(topicName, subscriptionRI.TopicPath);
            Assert.Equal(subscriptionName, subscriptionRI.SubscriptionName);

            Assert.True(topicRI.CreatedAt < topicRI.UpdatedAt);
            Assert.True(topicRI.UpdatedAt < topicRI.AccessedAt);
            Assert.True(subscriptionRI.CreatedAt < subscriptionRI.UpdatedAt);
            Assert.True(subscriptionRI.UpdatedAt < subscriptionRI.AccessedAt);
            Assert.True(topicRI.UpdatedAt < subscriptionRI.UpdatedAt);

            Assert.Equal(0, topicRI.MessageCountDetails.ActiveMessageCount);
            Assert.Equal(0, topicRI.MessageCountDetails.DeadLetterMessageCount);
            Assert.Equal(1, topicRI.MessageCountDetails.ScheduledMessageCount);
            Assert.Equal(1, subscriptionRI.MessageCountDetails.ActiveMessageCount);
            Assert.Equal(1, subscriptionRI.MessageCountDetails.DeadLetterMessageCount);
            Assert.Equal(0, subscriptionRI.MessageCountDetails.ScheduledMessageCount);
            Assert.Equal(2, subscriptionRI.MessageCount);
            Assert.Equal(1, topicRI.SubscriptionCount);
            Assert.True(topicRI.SizeInBytes > 0);

            await client.DeleteSubscriptionAsync(topicName, subscriptionName);
            await client.DeleteTopicAsync(topicName);
            await sender.CloseAsync();
            await receiver.CloseAsync();
        }

        // TODO: Asserts
        [Fact]
        public async void GetQueues()
        {
            var queues = await client.GetQueuesAsync();
        }

        [Fact]
        public async void GetTopics()
        {
            var topics = await client.GetTopicsAsync();
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
