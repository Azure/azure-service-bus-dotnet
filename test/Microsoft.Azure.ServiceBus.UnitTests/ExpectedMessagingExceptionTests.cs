// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;

    public class ExpectedMessagingExceptionTests
    {
        [Fact]
        async Task MessageLockLostExceptionTest()
        {
            const int messageCount = 2;
            var messagingFactory = new ServiceBusFactory();
            var queueClient = messagingFactory.CreateQueueClientFromConnectionString(TestUtility.GetEntityConnectionString(Constants.NonPartitionedQueueName));

            try
            {
                await TestUtility.SendMessagesAsync(queueClient, messageCount);
                var receivedMessages = await TestUtility.ReceiveMessagesAsync(queueClient, messageCount);

                Assert.True(receivedMessages.Count() == messageCount);

                // Let the messages expire
                await Task.Delay(TimeSpan.FromMinutes(1));

                // Complete should throw
                await Assert.ThrowsAsync<MessageLockLostException>(async () => await TestUtility.CompleteMessagesAsync(queueClient, receivedMessages));

                receivedMessages = await TestUtility.ReceiveMessagesAsync(queueClient, messageCount);
                Assert.True(receivedMessages.Count() == messageCount);

                await TestUtility.CompleteMessagesAsync(queueClient, receivedMessages);
            }
            finally
            {
                await queueClient.CloseAsync();
            }
        }

        [Fact]
        async Task SessionLockLostExceptionTest()
        {
            var messagingFactory = new ServiceBusFactory();
            var queueClient =
                (QueueClient)messagingFactory.CreateQueueClientFromConnectionString(
                    TestUtility.GetEntityConnectionString(Constants.SessionNonPartitionedQueueName));

            try
            {
                var messageId = "test-message1";
                var sessionId = Guid.NewGuid().ToString();
            await queueClient.SendAsync(new BrokeredMessage
                { MessageId = messageId, SessionId = sessionId });
                TestUtility.Log($"Sent Message: {messageId} to Session: {sessionId}");

                var messageSession = await queueClient.AcceptMessageSessionAsync(sessionId);
            Assert.NotNull(messageSession);

                var message = await messageSession.ReceiveAsync();
                Assert.True(message.MessageId == messageId);
                TestUtility.Log($"Received Message: SessionId: {messageSession.SessionId}");

                // Let the Session expire
                await Task.Delay(TimeSpan.FromMinutes(1));

                // Complete should throw
                await Assert.ThrowsAsync<SessionLockLostException>(async () => await message.CompleteAsync());
                try
                {
                    await messageSession.CloseAsync();
                }
                catch (Exception e)
                {
                    TestUtility.Log($"Got Exception on Session Close(): SessionId: {messageSession.SessionId}, Exception: {e.Message}");
                }

                messageSession = await queueClient.AcceptMessageSessionAsync(sessionId);
            Assert.NotNull(messageSession);

                message = await messageSession.ReceiveAsync();
                TestUtility.Log($"Received Message: SessionId: {messageSession.SessionId}");

                await message.CompleteAsync();
            }
            finally
            {
                await queueClient.CloseAsync();
            }
        }

        [Fact]
        async Task CompleteOnPeekedMessagesShouldThrowTest()
        {
            var messagingFactory = new ServiceBusFactory();
            var queueClient = messagingFactory.CreateQueueClientFromConnectionString(TestUtility.GetEntityConnectionString(Constants.NonPartitionedQueueName), ReceiveMode.ReceiveAndDelete);

            try
            {
                await TestUtility.SendMessagesAsync(queueClient, 1);
                var message = await queueClient.PeekAsync();
                Assert.NotNull(message);
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await message.CompleteAsync());

                message = await queueClient.ReceiveAsync();
                Assert.NotNull(message);
            }
            finally
            {
                await queueClient.CloseAsync();
            }
        }
    }
}