﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus.Core;

    internal sealed class SessionPumpHost
    {
        readonly object syncLock;
        SessionReceivePump sessionReceivePump;
        CancellationTokenSource sessionPumpCancellationTokenSource;
        readonly string namespaceName;

        public SessionPumpHost(string clientId, ReceiveMode receiveMode, IMessageSessionEntity sessionClient, string namespaceName)
        {
            this.syncLock = new object();
            this.ClientId = clientId;
            this.ReceiveMode = receiveMode;
            this.SessionClient = sessionClient;
            this.namespaceName = namespaceName;
        }

        ReceiveMode ReceiveMode { get; }

        IMessageSessionEntity SessionClient { get; }

        string ClientId { get; }

        public void Close()
        {
            if (this.sessionReceivePump != null)
            {
                this.sessionPumpCancellationTokenSource?.Cancel();
                this.sessionPumpCancellationTokenSource?.Dispose();
                this.sessionReceivePump = null;
            }
        }

        public async Task OnSessionHandlerAsync(
            Func<IMessageSession, Message, CancellationToken, Task> callback,
            SessionHandlerOptions sessionHandlerOptions)
        {
            MessagingEventSource.Log.RegisterOnSessionHandlerStart(this.ClientId, sessionHandlerOptions);

            lock (this.syncLock)
            {
                if (this.sessionReceivePump != null)
                {
                    throw new InvalidOperationException(Resources.SessionHandlerAlreadyRegistered);
                }

                this.sessionPumpCancellationTokenSource = new CancellationTokenSource();
                this.sessionReceivePump = new SessionReceivePump(
                    this.ClientId,
                    this.SessionClient,
                    this.ReceiveMode,
                    sessionHandlerOptions,
                    callback,
                    namespaceName,
                    this.sessionPumpCancellationTokenSource.Token);
            }

            try
            {
                await this.sessionReceivePump.StartPumpAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.RegisterOnSessionHandlerException(this.ClientId, exception);
                if (this.sessionReceivePump != null)
                {
                    this.sessionPumpCancellationTokenSource.Cancel();
                    this.sessionPumpCancellationTokenSource.Dispose();
                    this.sessionReceivePump = null;
                }

                throw;
            }

            MessagingEventSource.Log.RegisterOnSessionHandlerStop(this.ClientId);
        }
    }
}