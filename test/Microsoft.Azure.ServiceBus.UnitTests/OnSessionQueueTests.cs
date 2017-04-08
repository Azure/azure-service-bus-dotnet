// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Core;
    using Xunit;

    public class OnSessionQueueTests
    {
        public static IEnumerable<object> TestPermutations => new object[]
        {
            new object[] { TestConstants.SessionNonPartitionedQueueName, 1 },
            new object[] { TestConstants.SessionNonPartitionedQueueName, 5 },
            new object[] { TestConstants.SessionPartitionedQueueName, 1 },
            new object[] { TestConstants.SessionPartitionedQueueName, 5 },
        };

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnSessionPeekLockWithAutoCompleteTrue(string queueName, int maxConcurrentCalls)
        {
            await this.OnSessionTestAsync(queueName, maxConcurrentCalls, ReceiveMode.PeekLock, true);
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnSessionPeekLockWithAutoCompleteFalse(string queueName, int maxConcurrentCalls)
        {
            await this.OnSessionTestAsync(queueName, maxConcurrentCalls, ReceiveMode.PeekLock, false);
        }

        [Fact]
        [DisplayTestMethodName]
        void OnSessionHandlerShouldFailOnNonSessionFulQueue()
        {
            var queueClient = new QueueClient(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName);

            Assert.Throws<InvalidOperationException>(
               () => queueClient.RegisterSessionHandler(
               (session, message, token) =>
               {
                   return Task.CompletedTask;
               }));
        }

        async Task OnSessionTestAsync(string queueName, int maxConcurrentCalls, ReceiveMode mode, bool autoComplete)
        {
            const int numberOfSessions = 5;
            const int messagesPerSession = 10;

            var queueClient = new QueueClient(TestUtility.NamespaceConnectionString, queueName, mode);
            try
            {
                int totalMessageCount = 0;
                Dictionary<string, int> sessionMessageMap = new Dictionary<string, int>();

                await TestUtility.SendSessionMessagesAsync(queueClient.InnerSender, numberOfSessions, messagesPerSession);

                RegisterSessionHandlerOptions handlerOptions =
                    new RegisterSessionHandlerOptions()
                    {
                        MaxConcurrentSessions = maxConcurrentCalls,
                        MessageWaitTimeout = TimeSpan.FromSeconds(5),
                        AutoComplete = autoComplete
                    };

                queueClient.RegisterSessionHandler(
                    async (session, message, token) =>
                    {
                        Assert.NotNull(session);
                        Assert.NotNull(message);

                        totalMessageCount++;
                        TestUtility.Log($"Received Session: {session.SessionId} message: SequenceNumber: {message.SystemProperties.SequenceNumber}");

                        if (queueClient.ReceiveMode == ReceiveMode.PeekLock && !autoComplete)
                        {
                            await session.CompleteAsync(message.SystemProperties.LockToken);
                        }

                        if (!sessionMessageMap.ContainsKey(session.SessionId))
                        {
                            sessionMessageMap[session.SessionId] = 1;
                        }
                        else
                        {
                            sessionMessageMap[session.SessionId]++;
                        }
                    },
                    handlerOptions);

                // Wait for the OnMessage Tasks to finish
                Stopwatch stopwatch = Stopwatch.StartNew();
                while (stopwatch.Elapsed.TotalSeconds <= 300)
                {
                    if (totalMessageCount == messagesPerSession * numberOfSessions)
                    {
                        TestUtility.Log($"All '{totalMessageCount}' messages Received.");
                        break;
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                }

                foreach (KeyValuePair<string, int> keyValuePair in sessionMessageMap)
                {
                    TestUtility.Log($"Session: {keyValuePair.Key}, Messages Received in this Session: {keyValuePair.Value}");
                }

                Assert.True(sessionMessageMap.Keys.Count == numberOfSessions);
                Assert.True(totalMessageCount == messagesPerSession * numberOfSessions);
            }
            finally
            {
                await queueClient.CloseAsync();
            }
        }
    }
}