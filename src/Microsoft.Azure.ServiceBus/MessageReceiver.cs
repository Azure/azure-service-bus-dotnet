// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Linq;
    using Azure.Amqp;
    using Messaging.Amqp;

    public abstract class MessageReceiver : ClientEntity
    {
        readonly TimeSpan operationTimeout;

        protected MessageReceiver(ReceiveMode receiveMode, TimeSpan operationTimeout)
            : base(nameof(MessageReceiver) + StringUtility.GetRandomString())
        {
            this.ReceiveMode = receiveMode;
            this.operationTimeout = operationTimeout;
        }

        public abstract string Path { get; }

        public ReceiveMode ReceiveMode { get; protected set; }

        internal TimeSpan OperationTimeout
        {
            get { return this.operationTimeout; }
        }

        public async Task<BrokeredMessage> ReceiveAsync()
        {
            IList<BrokeredMessage> messages = await this.ReceiveAsync(1).ConfigureAwait(false);
            if (messages != null && messages.Count > 0)
            {
                return messages[0];
            }

            return null;
        }

        public Task<IList<BrokeredMessage>> ReceiveAsync(int maxMessageCount)
        {
            return this.OnReceiveAsync(maxMessageCount);
        }

        public Task<IList<BrokeredMessage>> ReceiveBySequenceNumberAsync(IEnumerable<long> sequenceNumbers)
        {
            return this.OnReceiveBySequenceNumberAsync(sequenceNumbers);
        }

        public Task CompleteAsync(IEnumerable<Guid> lockTokens)
        {
            this.ThrowIfNotPeekLockMode();
            MessageReceiver.ValidateLockTokens(lockTokens);

            return this.OnCompleteAsync(lockTokens);
        }

        public Task AbandonAsync(IEnumerable<Guid> lockTokens)
        {
            this.ThrowIfNotPeekLockMode();
            MessageReceiver.ValidateLockTokens(lockTokens);

            return this.OnAbandonAsync(lockTokens);
        }

        public Task DeferAsync(IEnumerable<Guid> lockTokens)
        {
            this.ThrowIfNotPeekLockMode();
            MessageReceiver.ValidateLockTokens(lockTokens);

            return this.OnDeferAsync(lockTokens);
        }

        public Task DeadLetterAsync(IEnumerable<Guid> lockTokens)
        {
            this.ThrowIfNotPeekLockMode();
            MessageReceiver.ValidateLockTokens(lockTokens);

            return this.OnDeadLetterAsync(lockTokens);
        }

        public Task<DateTime> RenewLockAsync(Guid lockToken)
        {
            this.ThrowIfNotPeekLockMode();
            MessageReceiver.ValidateLockTokens(new Guid[] {lockToken});

            return this.OnRenewLockAsync(lockToken);
        }

        protected abstract Task<IList<BrokeredMessage>> OnReceiveAsync(int maxMessageCount);

        protected abstract Task<IList<BrokeredMessage>> OnReceiveBySequenceNumberAsync(IEnumerable<long> sequenceNumbers);

        protected abstract Task OnCompleteAsync(IEnumerable<Guid> lockTokens);

        protected abstract Task OnAbandonAsync(IEnumerable<Guid> lockTokens);

        protected abstract Task OnDeferAsync(IEnumerable<Guid> lockTokens);

        protected abstract Task OnDeadLetterAsync(IEnumerable<Guid> lockTokens);

        protected abstract Task<DateTime> OnRenewLockAsync(Guid lockToken);

        internal abstract Task<AmqpResponseMessage> OnExecuteRequestResponseAsync(AmqpRequestMessage requestAmqpMessage);

        internal Task<AmqpResponseMessage> ExecuteRequestResponseAsync(AmqpRequestMessage amqpRequestMessage)
        {
            return this.OnExecuteRequestResponseAsync(amqpRequestMessage);
        }

        void ThrowIfNotPeekLockMode()
        {
            if (this.ReceiveMode != ReceiveMode.PeekLock)
            {
                throw Fx.Exception.AsError(new InvalidOperationException("The operation is only supported in 'PeekLock' receive mode."));
            }
        }

        static void ValidateLockTokens(IEnumerable<Guid> lockTokens)
        {
            if (lockTokens == null || !lockTokens.Any())
            {
                throw Fx.Exception.ArgumentNull("lockTokens");
            }
        }
    }
}
