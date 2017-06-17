// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus.Core;

namespace Microsoft.Azure.ServiceBus
{
    internal sealed class SessionPumpHost
    {
        readonly object syncLock;
        CancellationTokenSource sessionPumpCancellationTokenSource;
        SessionReceivePump sessionReceivePump;

        public SessionPumpHost(string clientId, ReceiveMode receiveMode, IMessageSessionEntity sessionClient)
        {
            syncLock = new object();
            ClientId = clientId;
            ReceiveMode = receiveMode;
            SessionClient = sessionClient;
        }

        ReceiveMode ReceiveMode { get; }

        IMessageSessionEntity SessionClient { get; }

        string ClientId { get; }

        public void Close()
        {
            if (sessionReceivePump != null)
            {
                sessionPumpCancellationTokenSource?.Cancel();
                sessionPumpCancellationTokenSource?.Dispose();
                sessionReceivePump = null;
            }
        }

        public async Task OnSessionHandlerAsync(
            Func<IMessageSession, Message, CancellationToken, Task> callback,
            SessionHandlerOptions sessionHandlerOptions)
        {
            MessagingEventSource.Log.RegisterOnSessionHandlerStart(ClientId, sessionHandlerOptions);

            lock (syncLock)
            {
                if (sessionReceivePump != null)
                {
                    throw new InvalidOperationException(Resources.SessionHandlerAlreadyRegistered);
                }

                sessionPumpCancellationTokenSource = new CancellationTokenSource();
                sessionReceivePump = new SessionReceivePump(
                    ClientId,
                    SessionClient,
                    ReceiveMode,
                    sessionHandlerOptions,
                    callback,
                    sessionPumpCancellationTokenSource.Token);
            }

            try
            {
                await sessionReceivePump.StartPumpAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.RegisterOnSessionHandlerException(ClientId, exception);
                if (sessionReceivePump != null)
                {
                    sessionPumpCancellationTokenSource.Cancel();
                    sessionPumpCancellationTokenSource.Dispose();
                    sessionReceivePump = null;
                }

                throw;
            }

            MessagingEventSource.Log.RegisterOnSessionHandlerStop(ClientId);
        }
    }
}