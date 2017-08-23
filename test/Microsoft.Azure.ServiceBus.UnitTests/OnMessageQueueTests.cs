// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Xunit;

    public class OnMessageQueueTests : SenderReceiverClientTestBase
    {
        public static IEnumerable<object> TestPermutations => new object[]
        {
            new object[] { TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName, 1 },
            new object[] { TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName, 10 },
            new object[] { TestUtility.NamespaceConnectionString, TestConstants.PartitionedQueueName, 1 },
            new object[] { TestUtility.NamespaceConnectionString, TestConstants.PartitionedQueueName, 10 },
            new object[] { TestUtility.WebSocketsNamespaceConnectionString, TestConstants.NonPartitionedQueueName, 1 },
            new object[] { TestUtility.WebSocketsNamespaceConnectionString, TestConstants.NonPartitionedQueueName, 10 },
            new object[] { TestUtility.WebSocketsNamespaceConnectionString, TestConstants.PartitionedQueueName, 1 },
            new object[] { TestUtility.WebSocketsNamespaceConnectionString, TestConstants.PartitionedQueueName, 10 },
        };

        public static IEnumerable<object> TestConnectionStrings => new object[]
        {
            new object[] { TestUtility.NamespaceConnectionString },
            new object[] { TestUtility.WebSocketsNamespaceConnectionString },
        };

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnMessagePeekLockWithAutoCompleteTrue(string connectionString, string queueName, int maxConcurrentCalls)
        {
            await this.OnMessageTestAsync(connectionString, queueName, maxConcurrentCalls, ReceiveMode.PeekLock, true);
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnMessagePeekLockWithAutoCompleteFalse(string connectionString, string queueName, int maxConcurrentCalls)
        {
            await this.OnMessageTestAsync(connectionString, queueName, maxConcurrentCalls, ReceiveMode.PeekLock, false);
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnMessageReceiveDelete(string connectionString, string queueName, int maxConcurrentCalls)
        {
            await this.OnMessageTestAsync(connectionString, queueName, maxConcurrentCalls, ReceiveMode.ReceiveAndDelete, false);
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnMessageRegistrationWithoutPendingMessagesReceiveAndDelete(string connectionString, string queueName, int maxConcurrentCalls)
        {
            var queueClient = new QueueClient(connectionString, queueName, ReceiveMode.ReceiveAndDelete);
            try
            {
                await this.OnMessageRegistrationWithoutPendingMessagesTestCase(queueClient.InnerSender, queueClient.InnerReceiver, maxConcurrentCalls, true);
            }
            finally
            {
                await queueClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData(nameof(TestConnectionStrings))]
        [DisplayTestMethodName]
        async Task OnMessageExceptionHandlerCalledTest(string connectionString)
        {
            string queueName = "nonexistentqueuename";
            bool exceptionReceivedHandlerCalled = false;

            var queueClient = new QueueClient(connectionString, queueName, ReceiveMode.ReceiveAndDelete);
            queueClient.RegisterMessageHandler(
                (message, token) => throw new Exception("Unexpected exception: Did not expect messages here"),
                (eventArgs) =>
                {
                    Assert.NotNull(eventArgs);
                    Assert.NotNull(eventArgs.Exception);
                    if (eventArgs.Exception is MessagingEntityNotFoundException)
                    {
                        exceptionReceivedHandlerCalled = true;
                    }
                    return Task.CompletedTask;
                });

            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                while (stopwatch.Elapsed.TotalSeconds <= 10)
                {
                    if (exceptionReceivedHandlerCalled)
                    {
                        break;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                TestUtility.Log($"{DateTime.Now}: ExceptionReceivedHandlerCalled: {exceptionReceivedHandlerCalled}");
                Assert.True(exceptionReceivedHandlerCalled);
            }
            finally
            {
                await queueClient.CloseAsync();
            }            
        }

        async Task OnMessageTestAsync(string connectionString, string queueName, int maxConcurrentCalls, ReceiveMode mode, bool autoComplete)
        {
            const int messageCount = 10;

            var queueClient = new QueueClient(connectionString, queueName, mode);
            try
            {
                await this.OnMessageAsyncTestCase(
                    queueClient.InnerSender,
                    queueClient.InnerReceiver,
                    maxConcurrentCalls,
                    autoComplete,
                    messageCount);
            }
            finally
            {
                await queueClient.CloseAsync();
            }
        }
    }
}