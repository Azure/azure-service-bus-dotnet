// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Xunit;

    public class OnSessionTopicSubscriptionTests
    {
        public static IEnumerable<object> TestPermutations => new object[]
        {
            new object[] { TestConstants.NonPartitionedSessionTopicName, 1 },
            new object[] { TestConstants.NonPartitionedSessionTopicName, 5 },
            new object[] { TestConstants.PartitionedSessionTopicName, 1 },
            new object[] { TestConstants.PartitionedSessionTopicName, 5 },
        };

        string SubscriptionName => TestConstants.SessionSubscriptionName;

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnSessionPeekLockWithAutoCompleteTrue(string topicName, int maxConcurrentCalls)
        {
            await this.OnSessionTestAsync(topicName, maxConcurrentCalls, ReceiveMode.PeekLock, true);
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnSessionPeekLockWithAutoCompleteFalse(string topicName, int maxConcurrentCalls)
        {
            await this.OnSessionTestAsync(topicName, maxConcurrentCalls, ReceiveMode.PeekLock, false);
        }

        [Fact]
        [DisplayTestMethodName]
        void OnSessionHandlerShouldFailOnNonSessionFulQueue()
        {
            var topicClient = new TopicClient(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedTopicName);
            var subscriptionClient = new SubscriptionClient(
                TestUtility.NamespaceConnectionString,
                topicClient.TopicName,
                TestConstants.SubscriptionName,
                ReceiveMode.PeekLock);

            Assert.Throws<InvalidOperationException>(
               () => subscriptionClient.RegisterSessionHandler(
               (session, message, token) =>
               {
                   return Task.CompletedTask;
               }));
        }

        async Task OnSessionTestAsync(string topicName, int maxConcurrentCalls, ReceiveMode mode, bool autoComplete)
        {
            const int numberOfSessions = 5;
            const int messagesPerSession = 10;

            var topicClient = new TopicClient(TestUtility.NamespaceConnectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                TestUtility.NamespaceConnectionString,
                topicClient.TopicName,
                this.SubscriptionName,
                ReceiveMode.PeekLock);

            try
            {
                int totalMessageCount = 0;
                Dictionary<string, int> sessionMessageMap = new Dictionary<string, int>();

                await TestUtility.SendSessionMessagesAsync(topicClient.InnerSender, numberOfSessions, messagesPerSession);
                RegisterSessionHandlerOptions handlerOptions =
                    new RegisterSessionHandlerOptions()
                    {
                        MaxConcurrentSessions = maxConcurrentCalls,
                        MessageWaitTimeout = TimeSpan.FromSeconds(5),
                        AutoComplete = autoComplete
                    };

                subscriptionClient.RegisterSessionHandler(
                    async (session, message, token) =>
                    {
                        Assert.NotNull(session);
                        Assert.NotNull(message);

                        totalMessageCount++;
                        TestUtility.Log($"Received Session: {session.SessionId} message: SequenceNumber: {message.SystemProperties.SequenceNumber}");

                        if (subscriptionClient.ReceiveMode == ReceiveMode.PeekLock && !autoComplete)
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
                await subscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
            }
        }
    }
}