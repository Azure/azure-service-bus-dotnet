﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public sealed class TopicSessionTests
    {
        public static IEnumerable<object> TestPermutations => new object[]
        {
            new object[] { Constants.NonPartitionedSessionTopicName },
            new object[] { Constants.PartitionedSessionTopicName }
        };

        string SubscriptionName => Constants.SessionSubscriptionName;

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task SessionTest(string topicName)
        {
            var entityConnectionString = TestUtility.GetEntityConnectionString(topicName);
            var topicClient = TopicClient.CreateFromConnectionString(entityConnectionString);
            var subscriptionClient = SubscriptionClient.CreateFromConnectionString(entityConnectionString, this.SubscriptionName);
            try
            {
                var messageId1 = "test-message1";
                var sessionId1 = "sessionId1";
                await topicClient.SendAsync(new BrokeredMessage() { MessageId = messageId1, SessionId = sessionId1 });
                TestUtility.Log($"Sent Message: {messageId1} to Session: {sessionId1}");

                var messageId2 = "test-message2";
                var sessionId2 = "sessionId2";
                await topicClient.SendAsync(new BrokeredMessage() { MessageId = messageId2, SessionId = sessionId2 });
                TestUtility.Log($"Sent Message: {messageId2} to Session: {sessionId2}");

                // Receive Message, Complete and Close with SessionId - sessionId 1
                await this.AcceptAndCompleteSessionsAsync(subscriptionClient, sessionId1, messageId1);

                // Receive Message, Complete and Close with SessionId - sessionId 2
                await this.AcceptAndCompleteSessionsAsync(subscriptionClient, sessionId2, messageId2);

                // Receive Message, Complete and Close - With Null SessionId specified
                var messageId3 = "test-message3";
                var sessionId3 = "sessionId3";
                await topicClient.SendAsync(new BrokeredMessage() { MessageId = messageId3, SessionId = sessionId3 });

                await this.AcceptAndCompleteSessionsAsync(subscriptionClient, null, messageId3);
            }
            finally
            {
                subscriptionClient.Close();
                topicClient.Close();
            }
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task GetAndSetSessionStateTest(string topicName)
        {
            var entityConnectionString = TestUtility.GetEntityConnectionString(topicName);
            var topicClient = TopicClient.CreateFromConnectionString(entityConnectionString);
            var subscriptionClient = SubscriptionClient.CreateFromConnectionString(entityConnectionString, this.SubscriptionName);
            try
            {
                var messageId = "test-message1";
                var sessionId = Guid.NewGuid().ToString();
                await topicClient.SendAsync(new BrokeredMessage() { MessageId = messageId, SessionId = sessionId });
                TestUtility.Log($"Sent Message: {messageId} to Session: {sessionId}");

                var sessionReceiver = await subscriptionClient.AcceptMessageSessionAsync(sessionId);
                Assert.NotNull((object)sessionReceiver);
                var message = await sessionReceiver.ReceiveAsync();
                TestUtility.Log($"Received Message: {message.MessageId} from Session: {sessionReceiver.SessionId}");
                Assert.True(message.MessageId == messageId);

                var sessionStateString = "Received Message From Session!";
                var sessionState = new MemoryStream(Encoding.UTF8.GetBytes(sessionStateString));
                await sessionReceiver.SetStateAsync(sessionState);
                TestUtility.Log($"Set Session State: {sessionStateString} for Session: {sessionReceiver.SessionId}");

                var returnedSessionState = await sessionReceiver.GetStateAsync();
                using (var reader = new StreamReader(returnedSessionState, Encoding.UTF8))
                {
                    var returnedSessionStateString = await reader.ReadToEndAsync();
                    TestUtility.Log($"Get Session State Returned: {returnedSessionStateString} for Session: {sessionReceiver.SessionId}");
                    Assert.Equal(sessionStateString, returnedSessionStateString);
                }

                // Complete message using Session Receiver
                await sessionReceiver.CompleteAsync(new Guid[] { message.LockToken });
                TestUtility.Log($"Completed Message: {message.MessageId} for Session: {sessionReceiver.SessionId}");

                sessionStateString = "Completed Message On Session!";
                sessionState = new MemoryStream(Encoding.UTF8.GetBytes(sessionStateString));
                await sessionReceiver.SetStateAsync(sessionState);
                TestUtility.Log($"Set Session State: {sessionStateString} for Session: {sessionReceiver.SessionId}");

                returnedSessionState = await sessionReceiver.GetStateAsync();
                using (var reader = new StreamReader(returnedSessionState, Encoding.UTF8))
                {
                    var returnedSessionStateString = await reader.ReadToEndAsync();
                    TestUtility.Log($"Get Session State Returned: {returnedSessionStateString} for Session: {sessionReceiver.SessionId}");
                    Assert.Equal(sessionStateString, returnedSessionStateString);
                }

                await sessionReceiver.CloseAsync();
            }
            finally
            {
                subscriptionClient.Close();
                topicClient.Close();
            }
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task SessionRenewLockTestCase(string topicName)
        {
            var entityConnectionString = TestUtility.GetEntityConnectionString(topicName);
            var topicClient = TopicClient.CreateFromConnectionString(entityConnectionString);
            var subscriptionClient = SubscriptionClient.CreateFromConnectionString(entityConnectionString, this.SubscriptionName);
            try
            {
                var messageId = "test-message1";
                var sessionId = Guid.NewGuid().ToString();
                await topicClient.SendAsync(new BrokeredMessage() { MessageId = messageId, SessionId = sessionId });
                TestUtility.Log($"Sent Message: {messageId} to Session: {sessionId}");

                var sessionReceiver = await subscriptionClient.AcceptMessageSessionAsync(sessionId);
                Assert.NotNull((object)sessionReceiver);
                var initialSessionLockedUntilTime = sessionReceiver.LockedUntilUtc;
                TestUtility.Log($"Session LockedUntilUTC: {initialSessionLockedUntilTime} for Session: {sessionReceiver.SessionId}");
                var message = await sessionReceiver.ReceiveAsync();
                TestUtility.Log($"Received Message: {message.MessageId} from Session: {sessionReceiver.SessionId}");
                Assert.True(message.MessageId == messageId);

                TestUtility.Log("Sleeping 10 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(10));

                await sessionReceiver.RenewLockAsync();
                var firstLockedUntilUtcTime = sessionReceiver.LockedUntilUtc;
                TestUtility.Log($"After Renew Session LockedUntilUTC: {firstLockedUntilUtcTime} for Session: {sessionReceiver.SessionId}");
                Assert.True(firstLockedUntilUtcTime >= initialSessionLockedUntilTime + TimeSpan.FromSeconds(10));

                TestUtility.Log("Sleeping 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5));

                await sessionReceiver.RenewLockAsync();
                TestUtility.Log($"After Second Renew Session LockedUntilUTC: {sessionReceiver.LockedUntilUtc} for Session: {sessionReceiver.SessionId}");
                Assert.True(sessionReceiver.LockedUntilUtc >= firstLockedUntilUtcTime + TimeSpan.FromSeconds(5));
                await message.CompleteAsync();
                TestUtility.Log($"Completed Message: {message.MessageId} for Session: {sessionReceiver.SessionId}");
                await sessionReceiver.CloseAsync();
            }
            finally
            {
                subscriptionClient.Close();
                topicClient.Close();
            }
        }

        [Theory]
        [MemberData(nameof(TestPermutations))]
        [DisplayTestMethodName]
        async Task PeekSessionAsyncTest(string topicName, int messageCount = 10)
        {
            var entityConnectionString = TestUtility.GetEntityConnectionString(topicName);
            var topicClient = TopicClient.CreateFromConnectionString(entityConnectionString);
            var subscriptionClient = SubscriptionClient.CreateFromConnectionString(entityConnectionString, this.SubscriptionName, ReceiveMode.ReceiveAndDelete);
            try
            {
                var messageId1 = "test-message1";
                var sessionId1 = "sessionId1";
                await topicClient.SendAsync(new BrokeredMessage() { MessageId = messageId1, SessionId = sessionId1 });
                TestUtility.Log($"Sent Message: {messageId1} to Session: {sessionId1}");

                var messageId2 = "test-message2";
                var sessionId2 = "sessionId2";
                await topicClient.SendAsync(new BrokeredMessage() { MessageId = messageId2, SessionId = sessionId2 });
                TestUtility.Log($"Sent Message: {messageId2} to Session: {sessionId2}");

                // Peek Message, Receive and Delete with SessionId - sessionId 1
                await this.PeekAndDeleteMessageAsync(subscriptionClient, sessionId1, messageId1);

                // Peek Message, Receive and Delete with SessionId - sessionId 2
                await this.PeekAndDeleteMessageAsync(subscriptionClient, sessionId2, messageId2);
            }
            finally
            {
                await subscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
            }
        }

        async Task AcceptAndCompleteSessionsAsync(SubscriptionClient subscriptionClient, string sessionId, string messageId)
        {
            var sessionReceiver = await subscriptionClient.AcceptMessageSessionAsync(sessionId);
            if (sessionId != null)
            {
                Assert.True(sessionReceiver.SessionId == sessionId);
            }
            var message = await sessionReceiver.ReceiveAsync();
            Assert.True(message.MessageId == messageId);
            TestUtility.Log($"Received Message: {message.MessageId} from Session: {sessionReceiver.SessionId}");
            await message.CompleteAsync();
            TestUtility.Log($"Completed Message: {message.MessageId} for Session: {sessionReceiver.SessionId}");
        }

        async Task PeekAndDeleteMessageAsync(SubscriptionClient queueClient, string sessionId, string messageId)
        {
            var sessionReceiver = await queueClient.AcceptMessageSessionAsync(sessionId);
            if (sessionId != null)
            {
                Assert.True(sessionReceiver.SessionId == sessionId);
            }

            var message = await sessionReceiver.PeekAsync();
            Assert.True(message.MessageId == messageId);
            TestUtility.Log($"Peeked Message: {message.MessageId} from Session: {sessionReceiver.SessionId}");

            message = await sessionReceiver.ReceiveAsync();
            Assert.True(message.MessageId == messageId);
            TestUtility.Log($"Received Message: {message.MessageId} from Session: {sessionReceiver.SessionId}");

            await sessionReceiver.CloseAsync();
        }
    }
}