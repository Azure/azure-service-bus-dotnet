// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using System.Threading.Tasks;
    using System.Transactions;
    using Microsoft.Azure.ServiceBus.Core;
    using Xunit;

    public class TransactionTests
    {
        const string QueueName = TestConstants.NonPartitionedQueueName;
        static readonly string ConnectionString = TestUtility.NamespaceConnectionString;
        static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(5);

        [Fact]
        [DisplayTestMethodName]
        public async Task TransactionalSendCommitTest()
        {
            var sender = new MessageSender(ConnectionString, QueueName);
            var receiver = new MessageReceiver(ConnectionString, QueueName);
            
            try
            {
                string body = Guid.NewGuid().ToString("N");
                var message = new Message(body.GetBytes());
                using (var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    await sender.SendAsync(message).ConfigureAwait(false);
                    ts.Complete();
                }

                var receivedMessage = await receiver.ReceiveAsync(ReceiveTimeout);

                Assert.NotNull(receivedMessage);
                Assert.Equal(body, receivedMessage.Body.GetString());
                await receiver.CompleteAsync(receivedMessage.SystemProperties.LockToken);
            }
            finally
            {
                await sender.CloseAsync();
                await receiver.CloseAsync();
            }
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task TransactionalSendRollbackTest()
        {
            var sender = new MessageSender(ConnectionString, QueueName);
            var receiver = new MessageReceiver(ConnectionString, QueueName);

            try
            {
                string body = Guid.NewGuid().ToString("N");
                var message = new Message(body.GetBytes());
                using (var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    await sender.SendAsync(message).ConfigureAwait(false);
                    ts.Dispose();
                }

                var receivedMessage = await receiver.ReceiveAsync(ReceiveTimeout);
                Assert.Null(receivedMessage);
            }
            finally
            {
                await sender.CloseAsync();
                await receiver.CloseAsync();
            }
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task TransactionalCompleteCommitTest()
        {
            var sender = new MessageSender(ConnectionString, QueueName);
            var receiver = new MessageReceiver(ConnectionString, QueueName);

            try
            {
                string body = Guid.NewGuid().ToString("N");
                var message = new Message(body.GetBytes());
                await sender.SendAsync(message).ConfigureAwait(false);

                var receivedMessage = await receiver.ReceiveAsync(ReceiveTimeout);
                Assert.NotNull(receivedMessage);
                Assert.Equal(body, receivedMessage.Body.GetString());

                using (var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    await receiver.CompleteAsync(receivedMessage.SystemProperties.LockToken);
                    ts.Complete();
                }

                await Assert.ThrowsAsync<MessageLockLostException>(async () => await receiver.CompleteAsync(receivedMessage.SystemProperties.LockToken));
            }
            finally
            {
                await sender.CloseAsync();
                await receiver.CloseAsync();
            }
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task TransactionalCompleteRollbackTest()
        {
            var sender = new MessageSender(ConnectionString, QueueName);
            var receiver = new MessageReceiver(ConnectionString, QueueName);

            try
            {
                string body = Guid.NewGuid().ToString("N");
                var message = new Message(body.GetBytes());
                await sender.SendAsync(message).ConfigureAwait(false);

                var receivedMessage = await receiver.ReceiveAsync(ReceiveTimeout);
                Assert.NotNull(receivedMessage);
                Assert.Equal(body, receivedMessage.Body.GetString());

                using (var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    await receiver.CompleteAsync(receivedMessage.SystemProperties.LockToken);
                    ts.Dispose();
                }

                await receiver.CompleteAsync(receivedMessage.SystemProperties.LockToken);
            }
            finally
            {
                await sender.CloseAsync();
                await receiver.CloseAsync();
            }
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task TransactionalSessionDispositionTest()
        {
            var testQueueName = TestConstants.SessionNonPartitionedQueueName;
            var sender = new MessageSender(ConnectionString, testQueueName);
            var sessionClient = new SessionClient(ConnectionString, testQueueName);
            IMessageSession receiver = null;

            try
            {
                string body = Guid.NewGuid().ToString("N");
                var message = new Message(body.GetBytes())
                {
                    SessionId = body
                };
                await sender.SendAsync(message).ConfigureAwait(false);

                receiver = await sessionClient.AcceptMessageSessionAsync(body);

                var receivedMessage = await receiver.ReceiveAsync(ReceiveTimeout);
                Assert.NotNull(receivedMessage);
                Assert.Equal(body, receivedMessage.Body.GetString());

                using (var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    await receiver.CompleteAsync(receivedMessage.SystemProperties.LockToken);
                    ts.Dispose();
                }

                using (var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    await receiver.CompleteAsync(receivedMessage.SystemProperties.LockToken);
                    ts.Complete();
                }

                await Assert.ThrowsAsync<SessionLockLostException>(async () => await receiver.CompleteAsync(receivedMessage.SystemProperties.LockToken));
            }
            finally
            {
                await sender.CloseAsync();
                await sessionClient.CloseAsync();
                if (receiver != null)
                {
                    await receiver.CloseAsync();
                }
            }
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task TransactionalRequestResponseDispositionTest()
        {
            var sender = new MessageSender(ConnectionString, QueueName);
            var receiver = new MessageReceiver(ConnectionString, QueueName);

            try
            {
                string body = Guid.NewGuid().ToString("N");
                var message = new Message(body.GetBytes());
                await sender.SendAsync(message).ConfigureAwait(false);

                var receivedMessage = await receiver.ReceiveAsync(ReceiveTimeout);
                Assert.NotNull(receivedMessage);
                Assert.Equal(body, receivedMessage.Body.GetString());
                var sequenceNumber = receivedMessage.SystemProperties.SequenceNumber;
                await receiver.DeferAsync(receivedMessage.SystemProperties.LockToken);

                var deferredMessage = await receiver.ReceiveDeferredMessageAsync(sequenceNumber);

                using (var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    await receiver.CompleteAsync(deferredMessage.SystemProperties.LockToken);
                    ts.Dispose();
                }

                using (var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    await receiver.CompleteAsync(deferredMessage.SystemProperties.LockToken);
                    ts.Complete();
                }

                await Assert.ThrowsAsync<MessageLockLostException>(async () => await receiver.CompleteAsync(deferredMessage.SystemProperties.LockToken));
            }
            finally
            {
                await sender.CloseAsync();
                await receiver.CloseAsync();
            }
        }

        [Fact]
        [DisplayTestMethodName]
        public async Task TransactionThrowsWhenOperationsOfDifferentPartitionsAreInSameTransaction()
        {
            var queueName = TestConstants.PartitionedQueueName;
            var sender = new MessageSender(ConnectionString, queueName);
            var receiver = new MessageReceiver(ConnectionString, queueName);

            try
            {
                string body = Guid.NewGuid().ToString("N");
                var message1 = new Message((body + "1").GetBytes())
                {
                    PartitionKey = "1"
                };
                var message2 = new Message((body + "2").GetBytes())
                {
                    PartitionKey = "2"
                };

                // Two send operations to different partitions.
                var transaction = new CommittableTransaction();
                using (TransactionScope ts = new TransactionScope(transaction, TransactionScopeAsyncFlowOption.Enabled))
                {
                    await sender.SendAsync(message1);

                    await Assert.ThrowsAsync<InvalidOperationException>(
                        async () => await sender.SendAsync(message2));
                    ts.Complete();
                }
                transaction.Rollback();

                // Two complete operations to different partitions.
                await sender.SendAsync(message1);
                await sender.SendAsync(message2);

                var receivedMessage1 = await receiver.ReceiveAsync(ReceiveTimeout);
                Assert.NotNull(receivedMessage1);
                var receivedMessage2 = await receiver.ReceiveAsync(ReceiveTimeout);
                Assert.NotNull(receivedMessage2);

                transaction = new CommittableTransaction();
                using (TransactionScope ts = new TransactionScope(transaction, TransactionScopeAsyncFlowOption.Enabled))
                {
                    await receiver.CompleteAsync(receivedMessage1.SystemProperties.LockToken);

                    await Assert.ThrowsAsync<InvalidOperationException>(
                        async () => await receiver.CompleteAsync(receivedMessage2.SystemProperties.LockToken));
                    ts.Complete();
                }

                transaction.Rollback();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                await sender.CloseAsync();
                await receiver.CloseAsync();
            }
        }
    }
}
