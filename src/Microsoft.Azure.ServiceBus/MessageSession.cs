﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public abstract class MessageSession : MessageReceiver, IMessageSession
    {
        /// <summary>Represents a message session that allows grouping of related messages for processing in a single transaction.</summary>
        protected MessageSession(ReceiveMode receiveMode, string sessionId, DateTime lockedUntilUtc, MessageReceiver innerReceiver)
            : base(receiveMode, innerReceiver.OperationTimeout)
        {
            if (innerReceiver == null)
            {
                throw Fx.Exception.ArgumentNull("innerReceiver");
            }

            this.SessionId = sessionId;
            this.LockedUntilUtc = lockedUntilUtc;
            this.InnerMessageReceiver = innerReceiver;
        }

        public string SessionId { get; protected set; }

        public DateTime LockedUntilUtc { get; protected set;  }

        public override string Path
        {
            get { return this.InnerMessageReceiver.Path;  }
        }

        protected MessageReceiver InnerMessageReceiver { get; set; }

        public override async Task CloseAsync()
        {
            if (this.InnerMessageReceiver != null)
            {
                await this.InnerMessageReceiver.CloseAsync().ConfigureAwait(false);
            }
        }

        public Task<Stream> GetStateAsync()
        {
            return this.OnGetStateAsync();
        }

        public Task SetStateAsync(Stream sessionState)
        {
            return this.OnSetStateAsync(sessionState);
        }

        public Task RenewLockAsync()
        {
            return this.OnRenewLockAsync();
        }

        protected abstract Task<Stream> OnGetStateAsync();

        protected abstract Task OnSetStateAsync(Stream sessionState);

        protected abstract Task OnRenewLockAsync();

        protected override Task<IList<BrokeredMessage>> OnReceiveAsync(int maxMessageCount, TimeSpan serverWaitTime)
        {
            return this.InnerMessageReceiver.ReceiveAsync(maxMessageCount, serverWaitTime);
        }

        protected override Task<IList<BrokeredMessage>> OnReceiveBySequenceNumberAsync(IEnumerable<long> sequenceNumbers)
        {
            return this.InnerMessageReceiver.ReceiveBySequenceNumberAsync(sequenceNumbers);
        }

        protected override Task OnCompleteAsync(IEnumerable<Guid> lockTokens)
        {
            return this.InnerMessageReceiver.CompleteAsync(lockTokens);
        }

        protected override Task OnAbandonAsync(Guid lockToken)
        {
            return this.InnerMessageReceiver.AbandonAsync(lockToken);
        }

        protected override Task OnDeferAsync(Guid lockToken)
        {
            return this.InnerMessageReceiver.DeferAsync(lockToken);
        }

        protected override Task OnDeadLetterAsync(Guid lockToken)
        {
            return this.InnerMessageReceiver.DeadLetterAsync(lockToken);
        }

        protected override Task<DateTime> OnRenewLockAsync(Guid lockToken)
        {
            return this.InnerMessageReceiver.RenewLockAsync(lockToken);
        }

        protected override Task<IList<BrokeredMessage>> OnPeekAsync(long fromSequenceNumber, int messageCount = 1)
        {
            return this.InnerMessageReceiver.PeekAsync(messageCount);
        }
    }
}