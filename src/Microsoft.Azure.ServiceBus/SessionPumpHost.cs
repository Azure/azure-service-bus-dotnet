// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus.Core;

    sealed class SessionPumpHost
    {
        SessionReceivePump sessionReceivePump;
        CancellationTokenSource sessionPumpCancellationTokenSource;

        public SessionPumpHost(string clientId, ReceiveMode receiveMode, IMessageSessionEntity sessionClient)
        {
            this.ClientId = clientId;
            this.ReceiveMode = receiveMode;
            this.SessionClient = sessionClient;
        }

        ReceiveMode ReceiveMode { get; }

        IMessageSessionEntity SessionClient { get; }

        string ClientId { get; }

        public void OnClosingAsync()
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
            RegisterSessionHandlerOptions registerSessionHandlerOptions)
        {
            MessagingEventSource.Log.RegisterOnSessionHandlerStart(this.ClientId, registerSessionHandlerOptions);

            this.sessionPumpCancellationTokenSource = new CancellationTokenSource();
            this.sessionReceivePump = new SessionReceivePump(
                this.ClientId,
                this.SessionClient,
                this.ReceiveMode,
                registerSessionHandlerOptions,
                callback,
                this.sessionPumpCancellationTokenSource.Token);

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