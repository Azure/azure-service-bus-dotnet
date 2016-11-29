// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using Xunit;
    using Xunit.Abstractions;

    public class QueueSessionTests
    {
        const int MaxAttemptsCount = 5;
        ITestOutputHelper output;

        public QueueSessionTests(ITestOutputHelper output)
        {
            this.output = output;
            ConnectionString = Environment.GetEnvironmentVariable("SESSIONQUEUECLIENTCONNECTIONSTRING");
            
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                throw new InvalidOperationException("SESSIONQUEUECLIENTCONNECTIONSTRING environment variable was not found!");
            }
        }

        string ConnectionString { get; }

        [Fact]
        async Task BasicSessionTest()
        {
            QueueClient queueClient = QueueClient.Create(this.ConnectionString);

            string messageId1 = "test-message1";
            string sessionId1 = "sessionId1";
            await queueClient.SendAsync(new BrokeredMessage() { MessageId = messageId1, SessionId = sessionId1 });
            Log($"Sent Message: {messageId1} to Session: {sessionId1}");

            string messageId2 = "test-message2";
            string sessionId2 = "sessionId2";
            await queueClient.SendAsync(new BrokeredMessage() { MessageId = messageId2, SessionId = sessionId2 });
            Log($"Sent Message: {messageId2} to Session: {sessionId2}");

            //Receive Message, Complete and Close with SessionId - sessionId 1
            await this.AcceptAndCompleteSessionsAsync(queueClient, sessionId1, messageId1);

            //Receive Message, Complete and Close with SessionId - sessionId 2
            await this.AcceptAndCompleteSessionsAsync(queueClient, sessionId2, messageId2);

            //Receive Message, Complete and Close - With Null SessionId specified
            string messageId3 = "test-message3";
            string sessionId3 = "sessionId3";
            await queueClient.SendAsync(new BrokeredMessage() { MessageId = messageId3, SessionId = sessionId3 });

            await this.AcceptAndCompleteSessionsAsync(queueClient, null, messageId3);

            //await this.CleanupSessionsAsync(queueClient);
        }

        [Fact]
        async Task GetAndSetStateTest()
        {
            Log($"ConnectionString: {this.ConnectionString}");
            QueueClient queueClient = QueueClient.Create(this.ConnectionString);

            string messageId = "test-message1";
            string sessionId = Guid.NewGuid().ToString();
            await queueClient.SendAsync(new BrokeredMessage() { MessageId = messageId, SessionId = sessionId });
            Log($"Sent Message: {messageId} to Session: {sessionId}");

            MessageSession sessionReceiver = await queueClient.AcceptMessageSessionAsync(sessionId);
            Assert.NotNull((object) sessionReceiver);
            BrokeredMessage message = await sessionReceiver.ReceiveAsync();
            Log($"Received Message: {message.MessageId} from Session: {sessionReceiver.SessionId}");
            Assert.True(message.MessageId == messageId);

            string sessionStateString = "Received Message From Session!";
            Stream sessionState = new MemoryStream(Encoding.UTF8.GetBytes(sessionStateString));
            await sessionReceiver.SetStateAsync(sessionState);
            Log($"Set Session State: {sessionStateString} for Session: {sessionReceiver.SessionId}");

            Stream returnedSessionState = await sessionReceiver.GetStateAsync();
            using (StreamReader reader = new StreamReader(returnedSessionState, Encoding.UTF8))
            {
                string returnedSessionStateString = reader.ReadToEnd();
                Log($"Get Session State Returned: {returnedSessionStateString} for Session: {sessionReceiver.SessionId}");
                Assert.Equal(sessionStateString, returnedSessionStateString);
            }

            //Complete message using Session Receiver 
            await sessionReceiver.CompleteAsync(new Guid[] {message.LockToken});
            Log($"Completed Message: {message.MessageId} for Session: {sessionReceiver.SessionId}");

            sessionStateString = "Completed Message On Session!";
            sessionState = new MemoryStream(Encoding.UTF8.GetBytes(sessionStateString));
            await sessionReceiver.SetStateAsync(sessionState);
            Log($"Set Session State: {sessionStateString} for Session: {sessionReceiver.SessionId}");

            returnedSessionState = await sessionReceiver.GetStateAsync();
            using (StreamReader reader = new StreamReader(returnedSessionState, Encoding.UTF8))
            {
                string returnedSessionStateString = reader.ReadToEnd();
                Log($"Get Session State Returned: {returnedSessionStateString} for Session: {sessionReceiver.SessionId}");
                Assert.Equal(sessionStateString, returnedSessionStateString);
            }

            await sessionReceiver.CloseAsync();
        }

        [Fact]
        async Task SessionRenewLockTest() 
        {
            QueueClient queueClient = QueueClient.Create(this.ConnectionString);

            string messageId = "test-message1";
            string sessionId = Guid.NewGuid().ToString();
            await queueClient.SendAsync(new BrokeredMessage() { MessageId = messageId, SessionId = sessionId });
            Log($"Sent Message: {messageId} to Session: {sessionId}");

            MessageSession sessionReceiver = await queueClient.AcceptMessageSessionAsync(sessionId);
            Assert.NotNull((object)sessionReceiver);
            DateTime initialSessionLockedUntilTime = sessionReceiver.LockedUntilUtc;
            Log($"Session LockedUntilUTC: {initialSessionLockedUntilTime} for Session: {sessionReceiver.SessionId}");
            BrokeredMessage message = await sessionReceiver.ReceiveAsync();
            Log($"Received Message: {message.MessageId} from Session: {sessionReceiver.SessionId}");
            Assert.True(message.MessageId == messageId);

            Log("Sleeping 10 seconds...");
            Thread.Sleep(TimeSpan.FromSeconds(10));

            await sessionReceiver.RenewLockAsync();
            DateTime firstLockedUntilUtcTime = sessionReceiver.LockedUntilUtc;
            Log($"After Renew Session LockedUntilUTC: {firstLockedUntilUtcTime} for Session: {sessionReceiver.SessionId}");
            Assert.True(firstLockedUntilUtcTime >= initialSessionLockedUntilTime + TimeSpan.FromSeconds(10));

            Log("Sleeping 5 seconds...");
            Thread.Sleep(TimeSpan.FromSeconds(5));

            await sessionReceiver.RenewLockAsync();
            Log($"After Second Renew Session LockedUntilUTC: {sessionReceiver.LockedUntilUtc} for Session: {sessionReceiver.SessionId}");
            Assert.True(sessionReceiver.LockedUntilUtc >= firstLockedUntilUtcTime + TimeSpan.FromSeconds(5));
            await message.CompleteAsync();
            Log($"Completed Message: {message.MessageId} for Session: {sessionReceiver.SessionId}");
            await sessionReceiver.CloseAsync();
        }

        async Task AcceptAndCompleteSessionsAsync(QueueClient queueClient, string sessionId, string messageId)
        {
            MessageSession sessionReceiver = await queueClient.AcceptMessageSessionAsync(sessionId);
            {
                if (sessionId != null)
                {
                    Assert.True(sessionReceiver.SessionId == sessionId);
                }
                BrokeredMessage message = await sessionReceiver.ReceiveAsync();
                Assert.True(message.MessageId == messageId);
                Log($"Received Message: {message.MessageId} from Session: {sessionReceiver.SessionId}");
                await message.CompleteAsync();
                Log($"Completed Message: {message.MessageId} for Session: {sessionReceiver.SessionId}");
                await sessionReceiver.CloseAsync();
            }
        }

        async Task CleanupSessionsAsync(QueueClient queueClient)
        {
            MessageSession sessionReceiver = null;
            do
            {
                sessionReceiver = await queueClient.AcceptMessageSessionAsync();
                if (sessionReceiver != null)
                {
                    BrokeredMessage message = null;
                    do
                    {
                        message = await sessionReceiver.ReceiveAsync();
                        if (message != null)
                        {
                            Log($"Received Message: {message.MessageId} from Session: {sessionReceiver.SessionId}");
                            await message.CompleteAsync();
                        }
                    } while (message != null);

                    await sessionReceiver.CloseAsync();
                }
            } while (sessionReceiver != null);
        }

        void Log(string message)
        {
            var formattedMessage = string.Format("{0} {1}", DateTime.Now.TimeOfDay, message);
            output.WriteLine(formattedMessage);
            Debug.WriteLine(formattedMessage);
            Console.WriteLine(formattedMessage);
        }
    }
}
