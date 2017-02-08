// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp.Framing;
    using Xunit;

    public class OnMessageTests
    {
        public static IEnumerable<object> TestPermutations => new object[]
        {
            new object[] { Constants.NonPartitionedQueueName, 1 },
            new object[] { Constants.NonPartitionedQueueName, 10 },
            new object[] { Constants.PartitionedQueueName, 1 },
            new object[] { Constants.PartitionedQueueName, 10 },
        };

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnMessagePeekLockWithAutoCompleteTrue(string queueName, int maxConcurrentCalls)
        {
            await this.OnMessageTestAsync(queueName, maxConcurrentCalls, ReceiveMode.PeekLock, true);
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnMessagePeekLockWithAutoCompleteFalse(string queueName, int maxConcurrentCalls)
        {
            await this.OnMessageTestAsync(queueName, maxConcurrentCalls, ReceiveMode.PeekLock, false);
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnMessageReceiveDelete(string queueName, int maxConcurrentCalls)
        {
            await this.OnMessageTestAsync(queueName, maxConcurrentCalls, ReceiveMode.ReceiveAndDelete, false);
        }

        async Task OnMessageTestAsync(string queueName, int maxConcurrentCalls, ReceiveMode mode, bool autoComplete)
        {
            const int messageCount = 10;

            var queueClient = QueueClient.CreateFromConnectionString(
                TestUtility.GetEntityConnectionString(queueName),
                mode);
            try
            {
                int count = 0;
                await TestUtility.SendMessagesAsync(queueClient.InnerSender, messageCount);
                queueClient.OnMessageAsync(
                async (message, token) =>
                {
                    Console.WriteLine($"Received message: SequenceNumber: {message.SequenceNumber}");
                    count++;
                    if (mode == ReceiveMode.PeekLock && !autoComplete)
                    {
                        await message.CompleteAsync();
                    }
                    await Task.FromResult(0);
                },
                new OnMessageOptions() { MaxConcurrentCalls = maxConcurrentCalls });

                // Wait for the OnMessage Tasks to finish
                Stopwatch stopwatch = Stopwatch.StartNew();
                while (stopwatch.Elapsed.TotalSeconds <= 60)
                {
                    if (count == messageCount)
                    {
                        Console.WriteLine($"All '{messageCount}' messages Received.");
                        break;
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                }
            }
            finally
            {
                await queueClient.CloseAsync();
            }
        }
    }
}