// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Xunit;

    public sealed class QueueClientTests : SenderReceiverClientTestBase
    {
        public static IEnumerable<object> TestPermutations => new object[]
        {
            new object[] { TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName },
            new object[] { TestUtility.NamespaceConnectionString, TestConstants.PartitionedQueueName },
            new object[] { TestUtility.WebSocketsNamespaceConnectionString, TestConstants.NonPartitionedQueueName },
            new object[] { TestUtility.WebSocketsNamespaceConnectionString, TestConstants.PartitionedQueueName }
        };

        public static IEnumerable<object> TestConnectionStrings => new object[]
        {
            new object[] { TestUtility.NamespaceConnectionString },
            new object[] { TestUtility.WebSocketsNamespaceConnectionString },
        };

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task PeekLockTest(string connectionString, string queueName, int messageCount = 10)
        {
            var queueClient = new QueueClient(connectionString, queueName);
            try
            {
                await this.PeekLockTestCase(queueClient.InnerSender, queueClient.InnerReceiver, messageCount);
            }
            finally
            {
                await queueClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task ReceiveDeleteTest(string connectionString, string queueName, int messageCount = 10)
        {
            var queueClient = new QueueClient(connectionString, queueName, ReceiveMode.ReceiveAndDelete);
            try
            {
                await this.ReceiveDeleteTestCase(queueClient.InnerSender, queueClient.InnerReceiver, messageCount);
            }
            finally
            {
                await queueClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task PeekLockWithAbandonTest(string connectionString, string queueName, int messageCount = 10)
        {
            var queueClient = new QueueClient(connectionString, queueName);
            try
            {
                await this.PeekLockWithAbandonTestCase(queueClient.InnerSender, queueClient.InnerReceiver, messageCount);
            }
            finally
            {
                await queueClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task PeekLockWithDeadLetterTest(string connectionString, string queueName, int messageCount = 10)
        {
            var queueClient = new QueueClient(connectionString, queueName);

            // Create DLQ Client To Receive DeadLetteredMessages
            var deadLetterQueueClient = new QueueClient(connectionString, EntityNameHelper.FormatDeadLetterPath(queueClient.QueueName));

            try
            {
                await
                    this.PeekLockWithDeadLetterTestCase(
                        queueClient.InnerSender,
                        queueClient.InnerReceiver,
                        deadLetterQueueClient.InnerReceiver,
                        messageCount);
            }
            finally
            {
                await deadLetterQueueClient.CloseAsync();
                await queueClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task BasicRenewLockTest(string connectionString, string queueName, int messageCount = 10)
        {
            var queueClient = new QueueClient(connectionString, queueName);
            try
            {
                await this.RenewLockTestCase(queueClient.InnerSender, queueClient.InnerReceiver, messageCount);
            }
            finally
            {
                await queueClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task ScheduleMessagesAppearAfterScheduledTimeAsyncTest(string connectionString, string queueName, int messageCount = 1)
        {
            var queueClient = new QueueClient(connectionString, queueName, ReceiveMode.ReceiveAndDelete);
            try
            {
                await this.ScheduleMessagesAppearAfterScheduledTimeAsyncTestCase(queueClient.InnerSender, queueClient.InnerReceiver, messageCount);
            }
            finally
            {
                await queueClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task CancelScheduledMessagesAsyncTest(string connectionString, string queueName, int messageCount = 1)
        {
            var queueClient = new QueueClient(connectionString, queueName, ReceiveMode.ReceiveAndDelete);
            try
            {
                await this.CancelScheduledMessagesAsyncTestCase(queueClient.InnerSender, queueClient.InnerReceiver, messageCount);
            }
            finally
            {
                await queueClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData(nameof(TestConnectionStrings))]
        [DisplayTestMethodName]
        async Task UpdatingPrefetchCountOnQueueClientUpdatesTheReceiverPrefetchCount(string connectionString)
        {
            string queueName = TestConstants.NonPartitionedQueueName;
            var queueClient = new QueueClient(connectionString, queueName, ReceiveMode.ReceiveAndDelete);

            try
            {
                Assert.Equal(0, queueClient.PrefetchCount);

                queueClient.PrefetchCount = 2;
                Assert.Equal(2, queueClient.PrefetchCount);

                // Message receiver should be created with latest prefetch count (lazy load).
                Assert.Equal(2, queueClient.InnerReceiver.PrefetchCount);

                queueClient.PrefetchCount = 3;
                Assert.Equal(3, queueClient.PrefetchCount);

                // Already created message receiver should have its prefetch value updated.
                Assert.Equal(3, queueClient.InnerReceiver.PrefetchCount);
            }
            finally
            {
                await queueClient.CloseAsync();
            }
        }
    }
}