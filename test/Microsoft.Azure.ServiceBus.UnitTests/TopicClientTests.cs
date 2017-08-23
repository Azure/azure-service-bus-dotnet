// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Xunit;

    public sealed class TopicClientTests : SenderReceiverClientTestBase
    {
        public static IEnumerable<object> TestPermutations => new object[]
        {
            new object[] { TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedTopicName },
            new object[] { TestUtility.NamespaceConnectionString, TestConstants.PartitionedTopicName },
            new object[] { TestUtility.WebSocketsNamespaceConnectionString, TestConstants.NonPartitionedTopicName },
            new object[] { TestUtility.WebSocketsNamespaceConnectionString, TestConstants.PartitionedTopicName }
        };

        string SubscriptionName => TestConstants.SubscriptionName;

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task PeekLockTest(string connectionString, string topicName, int messageCount = 10)
        {
            var topicClient = new TopicClient(connectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                connectionString,
                topicName,
                this.SubscriptionName);

            try
            {
                await this.PeekLockTestCase(
                    topicClient.InnerSender,
                    subscriptionClient.InnerSubscriptionClient.InnerReceiver,
                    messageCount);
            }
            finally
            {
                await subscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task TopicClientReceiveDeleteTestCase(string connectionString, string topicName, int messageCount = 10)
        {
            var topicClient = new TopicClient(connectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                connectionString,
                topicName,
                this.SubscriptionName,
                ReceiveMode.ReceiveAndDelete);
            try
            {
                await
                    this.ReceiveDeleteTestCase(
                        topicClient.InnerSender,
                        subscriptionClient.InnerSubscriptionClient.InnerReceiver,
                        messageCount);
            }
            finally
            {
                await subscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task TopicClientPeekLockWithAbandonTestCase(string connectionString, string topicName, int messageCount = 10)
        {
            var topicClient = new TopicClient(connectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                connectionString,
                topicName,
                this.SubscriptionName);
            try
            {
                await
                    this.PeekLockWithAbandonTestCase(
                        topicClient.InnerSender,
                        subscriptionClient.InnerSubscriptionClient.InnerReceiver,
                        messageCount);
            }
            finally
            {
                await subscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task TopicClientPeekLockWithDeadLetterTestCase(string connectionString, string topicName, int messageCount = 10)
        {
            var topicClient = new TopicClient(connectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                connectionString,
                topicName,
                this.SubscriptionName);

            // Create DLQ Client To Receive DeadLetteredMessages
            var subscriptionDeadletterPath = EntityNameHelper.FormatDeadLetterPath(this.SubscriptionName);
            var deadLetterSubscriptionClient = new SubscriptionClient(
                connectionString,
                topicName,
                subscriptionDeadletterPath);

            try
            {
                await
                    this.PeekLockWithDeadLetterTestCase(
                        topicClient.InnerSender,
                        subscriptionClient.InnerSubscriptionClient.InnerReceiver,
                        deadLetterSubscriptionClient.InnerSubscriptionClient.InnerReceiver,
                        messageCount);
            }
            finally
            {
                await deadLetterSubscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
                await subscriptionClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task TopicClientRenewLockTestCase(string connectionString, string topicName, int messageCount = 10)
        {
            var topicClient = new TopicClient(connectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                connectionString,
                topicName,
                this.SubscriptionName);
            try
            {
                await this.RenewLockTestCase(
                    topicClient.InnerSender,
                    subscriptionClient.InnerSubscriptionClient.InnerReceiver,
                    messageCount);
            }
            finally
            {
                await subscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task ScheduleMessagesAppearAfterScheduledTimeAsyncTest(string connectionString, string topicName, int messageCount = 1)
        {
            var topicClient = new TopicClient(connectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                connectionString,
                topicName,
                this.SubscriptionName,
                ReceiveMode.ReceiveAndDelete);
            try
            {
                await
                    this.ScheduleMessagesAppearAfterScheduledTimeAsyncTestCase(
                        topicClient.InnerSender,
                        subscriptionClient.InnerSubscriptionClient.InnerReceiver,
                        messageCount);
            }
            finally
            {
                await subscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task CancelScheduledMessagesAsyncTest(string connectionString, string topicName, int messageCount = 1)
        {
            var topicClient = new TopicClient(connectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                connectionString,
                topicName,
                this.SubscriptionName,
                ReceiveMode.ReceiveAndDelete);
            try
            {
                await
                    this.CancelScheduledMessagesAsyncTestCase(
                        topicClient.InnerSender,
                        subscriptionClient.InnerSubscriptionClient.InnerReceiver,
                        messageCount);
            }
            finally
            {
                await subscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
            }
        }
    }
}