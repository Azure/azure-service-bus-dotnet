// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus.Amqp
{
    internal sealed class AmqpSessionClient : IMessageSessionEntity
    {
        public AmqpSessionClient(
            string clientId,
            string entityPath,
            MessagingEntityType entityType,
            ReceiveMode receiveMode,
            int prefetchCount,
            ServiceBusConnection serviceBusConnection,
            ICbsTokenProvider cbsTokenProvider,
            RetryPolicy retryPolicy)
        {
            ClientId = clientId;
            EntityPath = entityPath;
            EntityType = entityType;
            ReceiveMode = receiveMode;
            PrefetchCount = prefetchCount;
            ServiceBusConnection = serviceBusConnection;
            CbsTokenProvider = cbsTokenProvider;
            RetryPolicy = retryPolicy;
        }

        ReceiveMode ReceiveMode { get; }

        string ClientId { get; }

        string EntityPath { get; }

        MessagingEntityType EntityType { get; }

        int PrefetchCount { get; }

        ServiceBusConnection ServiceBusConnection { get; }

        ICbsTokenProvider CbsTokenProvider { get; }

        RetryPolicy RetryPolicy { get; }

        public Task<IMessageSession> AcceptMessageSessionAsync()
        {
            return AcceptMessageSessionAsync(ServiceBusConnection.OperationTimeout);
        }

        public Task<IMessageSession> AcceptMessageSessionAsync(TimeSpan serverWaitTime)
        {
            return AcceptMessageSessionAsync(null, serverWaitTime);
        }

        public Task<IMessageSession> AcceptMessageSessionAsync(string sessionId)
        {
            return AcceptMessageSessionAsync(sessionId, ServiceBusConnection.OperationTimeout);
        }

        public async Task<IMessageSession> AcceptMessageSessionAsync(string sessionId, TimeSpan serverWaitTime)
        {
            MessagingEventSource.Log.AmqpSessionClientAcceptMessageSessionStart(
                ClientId,
                EntityPath,
                ReceiveMode,
                PrefetchCount,
                sessionId);

            var receiver = new MessageReceiver(
                EntityPath,
                EntityType,
                ReceiveMode,
                ServiceBusConnection,
                CbsTokenProvider,
                RetryPolicy,
                PrefetchCount,
                sessionId,
                true);
            try
            {
                await RetryPolicy.RunOperation(
                        async () => { await receiver.GetSessionReceiverLinkAsync(serverWaitTime).ConfigureAwait(false); }, serverWaitTime)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.AmqpSessionClientAcceptMessageSessionException(
                    ClientId,
                    EntityPath,
                    exception);

                await receiver.CloseAsync().ConfigureAwait(false);
                throw AmqpExceptionHelper.GetClientException(exception);
            }

            var session = new MessageSession(receiver.SessionId, receiver.LockedUntilUtc, receiver, RetryPolicy);

            MessagingEventSource.Log.AmqpSessionClientAcceptMessageSessionStop(
                ClientId,
                EntityPath,
                session.SessionId);

            return session;
        }
    }
}