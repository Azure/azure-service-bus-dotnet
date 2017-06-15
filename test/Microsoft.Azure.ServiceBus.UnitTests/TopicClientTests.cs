// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus.Primitives;
using Xunit;

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    public sealed class TopicClientTests : SenderReceiverClientTestBase
    {
        public static IEnumerable<object> TestPermutations => new object[]
        {
            new object[] {TestConstants.NonPartitionedTopicName},
            new object[] {TestConstants.PartitionedTopicName}
        };

        string SubscriptionName => TestConstants.SubscriptionName;

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task PeekLockTest(string topicName, int messageCount = 10)
        {
            var topicClient = new TopicClient(TestUtility.NamespaceConnectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                TestUtility.NamespaceConnectionString,
                topicName,
                SubscriptionName);

            try
            {
                await PeekLockTestCase(
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
        async Task TopicClientReceiveDeleteTestCase(string topicName, int messageCount = 10)
        {
            var topicClient = new TopicClient(TestUtility.NamespaceConnectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                TestUtility.NamespaceConnectionString,
                topicName,
                SubscriptionName,
                ReceiveMode.ReceiveAndDelete);
            try
            {
                await
                    ReceiveDeleteTestCase(
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
        async Task TopicClientPeekLockWithAbandonTestCase(string topicName, int messageCount = 10)
        {
            var topicClient = new TopicClient(TestUtility.NamespaceConnectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                TestUtility.NamespaceConnectionString,
                topicName,
                SubscriptionName);
            try
            {
                await
                    PeekLockWithAbandonTestCase(
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
        async Task TopicClientPeekLockWithDeadLetterTestCase(string topicName, int messageCount = 10)
        {
            var topicClient = new TopicClient(TestUtility.NamespaceConnectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                TestUtility.NamespaceConnectionString,
                topicName,
                SubscriptionName);

            // Create DLQ Client To Receive DeadLetteredMessages
            var subscriptionDeadletterPath = EntityNameHelper.FormatDeadLetterPath(SubscriptionName);
            var deadLetterSubscriptionClient = new SubscriptionClient(
                TestUtility.NamespaceConnectionString,
                topicName,
                subscriptionDeadletterPath);

            try
            {
                await
                    PeekLockWithDeadLetterTestCase(
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
        async Task TopicClientRenewLockTestCase(string topicName, int messageCount = 10)
        {
            var topicClient = new TopicClient(TestUtility.NamespaceConnectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                TestUtility.NamespaceConnectionString,
                topicName,
                SubscriptionName);
            try
            {
                await RenewLockTestCase(
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
        async Task ScheduleMessagesAppearAfterScheduledTimeAsyncTest(string topicName, int messageCount = 1)
        {
            var topicClient = new TopicClient(TestUtility.NamespaceConnectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                TestUtility.NamespaceConnectionString,
                topicName,
                SubscriptionName,
                ReceiveMode.ReceiveAndDelete);
            try
            {
                await
                    ScheduleMessagesAppearAfterScheduledTimeAsyncTestCase(
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
        async Task CancelScheduledMessagesAsyncTest(string topicName, int messageCount = 1)
        {
            var topicClient = new TopicClient(TestUtility.NamespaceConnectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                TestUtility.NamespaceConnectionString,
                topicName,
                SubscriptionName,
                ReceiveMode.ReceiveAndDelete);
            try
            {
                await
                    CancelScheduledMessagesAsyncTestCase(
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