﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    public class OnSessionTopicSubscriptionTests
    {
        public static IEnumerable<object> TestPermutations => new object[]
        {
            new object[] {TestConstants.NonPartitionedSessionTopicName, 1},
            new object[] {TestConstants.NonPartitionedSessionTopicName, 5},
            new object[] {TestConstants.PartitionedSessionTopicName, 1},
            new object[] {TestConstants.PartitionedSessionTopicName, 5}
        };

        string SubscriptionName => TestConstants.SessionSubscriptionName;

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnSessionPeekLockWithAutoCompleteTrue(string topicName, int maxConcurrentCalls)
        {
            await OnSessionTestAsync(topicName, maxConcurrentCalls, ReceiveMode.PeekLock, true);
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task OnSessionPeekLockWithAutoCompleteFalse(string topicName, int maxConcurrentCalls)
        {
            await OnSessionTestAsync(topicName, maxConcurrentCalls, ReceiveMode.PeekLock, false);
        }

        async Task OnSessionTestAsync(string topicName, int maxConcurrentCalls, ReceiveMode mode, bool autoComplete)
        {
            TestUtility.Log($"Topic: {topicName}, MaxConcurrentCalls: {maxConcurrentCalls}, Receive Mode: {mode}, AutoComplete: {autoComplete}");
            var topicClient = new TopicClient(TestUtility.NamespaceConnectionString, topicName);
            var subscriptionClient = new SubscriptionClient(
                TestUtility.NamespaceConnectionString,
                topicClient.TopicName,
                SubscriptionName,
                ReceiveMode.PeekLock);

            try
            {
                var handlerOptions =
                    new SessionHandlerOptions
                    {
                        MaxConcurrentSessions = 5,
                        MessageWaitTimeout = TimeSpan.FromSeconds(5),
                        AutoComplete = true
                    };

                var testSessionHandler = new TestSessionHandler(
                    subscriptionClient.ReceiveMode,
                    handlerOptions,
                    topicClient.InnerSender,
                    subscriptionClient.SessionPumpHost);

                // Send messages to Session
                await testSessionHandler.SendSessionMessages();

                // Register handler
                testSessionHandler.RegisterSessionHandler(handlerOptions);

                // Verify messages were received.
                await testSessionHandler.VerifyRun();
            }
            finally
            {
                await subscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
            }
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
                    (session, message, token) => Task.CompletedTask));
        }
    }
}