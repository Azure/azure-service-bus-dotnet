// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Amqp
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Amqp;
    using Core;
    using Primitives;

    class AmqpClient : IInnerSenderReceiver
    {
        readonly object syncLock;
        MessageSender innerSender;
        MessageReceiver innerReceiver;
        SessionReceivePump sessionReceivePump = null;
        CancellationTokenSource sessionPumpCancellationTokenSource;

        internal AmqpClient(string clientId, ServiceBusConnection servicebusConnection, string entityPath, MessagingEntityType entityType, ReceiveMode mode = ReceiveMode.ReceiveAndDelete)
        {
            this.ClientId = clientId;
            this.ServiceBusConnection = servicebusConnection;
            this.EntityPath = entityPath;
            this.MessagingEntityType = entityType;
            this.ReceiveMode = mode;
            this.syncLock = new object();
            this.TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(servicebusConnection.SasKeyName, servicebusConnection.SasKey);
            this.CbsTokenProvider = new TokenProviderAdapter(this.TokenProvider, servicebusConnection.OperationTimeout);
        }

        public string ClientId { get; }

        public ReceiveMode ReceiveMode { get; }

        public MessageSender InnerSender
        {
            get
            {
                if (this.innerSender == null)
                {
                    lock (this.ThisLock)
                    {
                        if (this.innerSender == null)
                        {
                            this.innerSender = this.CreateMessageSender();
                        }
                    }
                }

                return this.innerSender;
            }
        }

        public MessageReceiver InnerReceiver
        {
            get
            {
                if (this.innerReceiver == null)
                {
                    lock (this.ThisLock)
                    {
                        if (this.innerReceiver == null)
                        {
                            this.innerReceiver = this.CreateMessageReceiver();
                        }
                    }
                }

                return this.innerReceiver;
            }
        }

        internal ServiceBusConnection ServiceBusConnection { get; set; }

        internal string EntityPath { get; set; }

        internal MessagingEntityType MessagingEntityType { get; set; }

        internal ICbsTokenProvider CbsTokenProvider { get; }

        protected object ThisLock { get; } = new object();

        TokenProvider TokenProvider { get; }

        public async Task CloseSenderAsync()
        {
            await this.InnerSender.OnClosingAsync().ConfigureAwait(false);
        }

        public async Task CloseReceiverAsync()
        {
            await this.InnerReceiver.OnClosingAsync().ConfigureAwait(false);
        }

        public async Task OnClosingAsync()
        {
            if (this.innerSender != null)
            {
                await this.CloseSenderAsync().ConfigureAwait(false);
            }

            if (this.innerReceiver != null)
            {
                await this.CloseReceiverAsync().ConfigureAwait(false);
            }

            if (this.sessionReceivePump != null)
            {
                this.sessionPumpCancellationTokenSource?.Cancel();
                this.sessionPumpCancellationTokenSource?.Dispose();
                this.sessionReceivePump = null;
            }

            await this.ServiceBusConnection.CloseAsync().ConfigureAwait(false);
        }

        public async Task<IMessageSession> AcceptMessageSessionAsync()
        {
            string emptySessionId = string.Empty;

            // No point fetching sessions without messages for Session pump handler
            int prefetchCount = this.ServiceBusConnection.PrefetchCount != 0 ? this.ServiceBusConnection.PrefetchCount : Constants.DefaultClientPumpPrefetchCount;

            AmqpMessageReceiver receiver = new AmqpMessageReceiver(
                this.EntityPath,
                this.MessagingEntityType,
                this.ReceiveMode,
                prefetchCount,
                this.ServiceBusConnection,
                this.CbsTokenProvider,
                emptySessionId,
                true);
            try
            {
                await receiver.GetSessionReceiverLinkAsync(this.ServiceBusConnection.OperationTimeout).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await receiver.CloseAsync().ConfigureAwait(false);
                throw AmqpExceptionHelper.GetClientException(exception);
            }
            MessageSession session = new AmqpMessageSession(receiver.SessionId, receiver.LockedUntilUtc, receiver);
            return session;
        }

        public void RegisterSessionHandler(
            Func<IMessageSession, Message, CancellationToken, Task> callback,
            RegisterSessionHandlerOptions registerSessionHandlerOptions)
        {
            this.OnSessionHandlerAsync(callback, registerSessionHandlerOptions).GetAwaiter().GetResult();
        }

        async Task OnSessionHandlerAsync(
            Func<IMessageSession, Message, CancellationToken, Task> callback,
            RegisterSessionHandlerOptions registerSessionHandlerOptions)
        {
            MessagingEventSource.Log.RegisterOnSessionHandlerStart(this.ClientId, registerSessionHandlerOptions);

            lock (this.syncLock)
            {
                if (this.sessionReceivePump != null)
                {
                    throw new InvalidOperationException(Resources.SessionHandlerAlreadyRegistered);
                }

                this.sessionPumpCancellationTokenSource = new CancellationTokenSource();
                this.sessionReceivePump = new SessionReceivePump(this, registerSessionHandlerOptions, callback, this.sessionPumpCancellationTokenSource.Token);
            }

            try
            {
                await this.sessionReceivePump.StartPumpAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.RegisterOnSessionHandlerException(this.ClientId, exception);
                lock (this.syncLock)
                {
                    if (this.sessionReceivePump != null)
                    {
                        this.sessionPumpCancellationTokenSource.Cancel();
                        this.sessionPumpCancellationTokenSource.Dispose();
                        this.sessionReceivePump = null;
                    }
                }

                throw;
            }

            MessagingEventSource.Log.RegisterOnSessionHandlerStop(this.ClientId);
        }

        MessageSender CreateMessageSender()
        {
            return new AmqpMessageSender(this.EntityPath, this.MessagingEntityType, this.ServiceBusConnection, this.CbsTokenProvider);
        }

        MessageReceiver CreateMessageReceiver()
        {
            return new AmqpMessageReceiver(this.EntityPath, this.MessagingEntityType, this.ReceiveMode, this.ServiceBusConnection.PrefetchCount, this.ServiceBusConnection, this.CbsTokenProvider);
        }
    }
}