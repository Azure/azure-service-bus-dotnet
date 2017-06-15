﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    public class OnMessageQueueTests : SenderReceiverClientTestBase
    {
        public static IEnumerable<object> TestPermutations => new object[]
        {
            new object[] {TestConstants.NonPartitionedQueueName, 1},
            new object[] {TestConstants.NonPartitionedQueueName, 10},
            new object[] {TestConstants.PartitionedQueueName, 1},
            new object[] {TestConstants.PartitionedQueueName, 10}
        };

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnMessagePeekLockWithAutoCompleteTrue(string queueName, int maxConcurrentCalls)
        {
            await OnMessageTestAsync(queueName, maxConcurrentCalls, ReceiveMode.PeekLock, true);
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnMessagePeekLockWithAutoCompleteFalse(string queueName, int maxConcurrentCalls)
        {
            await OnMessageTestAsync(queueName, maxConcurrentCalls, ReceiveMode.PeekLock, false);
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnMessageReceiveDelete(string queueName, int maxConcurrentCalls)
        {
            await OnMessageTestAsync(queueName, maxConcurrentCalls, ReceiveMode.ReceiveAndDelete, false);
        }

        async Task OnMessageTestAsync(string queueName, int maxConcurrentCalls, ReceiveMode mode, bool autoComplete)
        {
            const int messageCount = 10;

            var queueClient = new QueueClient(TestUtility.NamespaceConnectionString, queueName, mode);
            try
            {
                await OnMessageAsyncTestCase(
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