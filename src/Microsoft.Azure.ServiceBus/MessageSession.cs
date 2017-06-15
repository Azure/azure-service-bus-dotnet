// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.ServiceBus.Amqp;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus
{
    internal class MessageSession : MessageReceiver, IMessageSession
    {
        public MessageSession(string sessionId, DateTime lockedUntilUtc, MessageReceiver innerMessageReceiver, RetryPolicy retryPolicy)
            : base(innerMessageReceiver.ReceiveMode, innerMessageReceiver.OperationTimeout, retryPolicy)
        {
            SessionId = sessionId;
            LockedUntilUtc = lockedUntilUtc;
            InnerMessageReceiver = innerMessageReceiver ?? throw Fx.Exception.ArgumentNull(nameof(innerMessageReceiver));
            ReceiveMode = innerMessageReceiver.ReceiveMode;
        }

        protected MessageReceiver InnerMessageReceiver { get; set; }

        public override string Path => InnerMessageReceiver.Path;

        public Task<Stream> GetStateAsync()
        {
            return OnGetStateAsync();
        }

        public Task SetStateAsync(Stream sessionState)
        {
            return OnSetStateAsync(sessionState);
        }

        public Task RenewSessionLockAsync()
        {
            return OnRenewLockAsync();
        }

        protected override async Task OnClosingAsync()
        {
            if (InnerMessageReceiver != null)
                await InnerMessageReceiver.CloseAsync().ConfigureAwait(false);
        }

        protected override Task<IList<Message>> OnReceiveAsync(int maxMessageCount, TimeSpan serverWaitTime)
        {
            return InnerMessageReceiver.ReceiveAsync(maxMessageCount, serverWaitTime);
        }

        protected override Task<IList<Message>> OnReceiveBySequenceNumberAsync(IEnumerable<long> sequenceNumbers)
        {
            return InnerMessageReceiver.ReceiveBySequenceNumberAsync(sequenceNumbers);
        }

        protected override Task OnCompleteAsync(IEnumerable<string> lockTokens)
        {
            return InnerMessageReceiver.CompleteAsync(lockTokens);
        }

        protected override Task OnAbandonAsync(string lockToken)
        {
            return InnerMessageReceiver.AbandonAsync(lockToken);
        }

        protected override Task OnDeferAsync(string lockToken)
        {
            return InnerMessageReceiver.DeferAsync(lockToken);
        }

        protected override Task OnDeadLetterAsync(string lockToken)
        {
            return InnerMessageReceiver.DeadLetterAsync(lockToken);
        }

        protected override Task<DateTime> OnRenewLockAsync(string lockToken)
        {
            return InnerMessageReceiver.RenewLockAsync(lockToken);
        }

        protected override Task<IList<Message>> OnPeekAsync(long fromSequenceNumber, int messageCount = 1)
        {
            return InnerMessageReceiver.PeekAsync(messageCount);
        }

        protected async Task<Stream> OnGetStateAsync()
        {
            try
            {
                var amqpRequestMessage = AmqpRequestMessage.CreateRequest(ManagementConstants.Operations.GetSessionStateOperation, OperationTimeout, null);
                amqpRequestMessage.Map[ManagementConstants.Properties.SessionId] = SessionId;

                var amqpResponseMessage = await InnerMessageReceiver.ExecuteRequestResponseAsync(amqpRequestMessage).ConfigureAwait(false);

                Stream sessionState = null;
                if (amqpResponseMessage.StatusCode == AmqpResponseStatusCode.OK)
                {
                    if (amqpResponseMessage.Map[ManagementConstants.Properties.SessionState] != null)
                        sessionState = new BufferListStream(new[] {amqpResponseMessage.GetValue<ArraySegment<byte>>(ManagementConstants.Properties.SessionState)});
                }
                else
                {
                    throw amqpResponseMessage.ToMessagingContractException();
                }

                return sessionState;
            }
            catch (Exception exception)
            {
                throw AmqpExceptionHelper.GetClientException(exception);
            }
        }

        protected async Task OnSetStateAsync(Stream sessionState)
        {
            try
            {
                if (sessionState != null && sessionState.CanSeek && sessionState.Position != 0)
                    throw new InvalidOperationException(Resources.CannotSerializeSessionStateWithPartiallyConsumedStream);

                var amqpRequestMessage = AmqpRequestMessage.CreateRequest(ManagementConstants.Operations.SetSessionStateOperation, OperationTimeout, null);
                amqpRequestMessage.Map[ManagementConstants.Properties.SessionId] = SessionId;

                if (sessionState != null)
                {
                    var buffer = BufferListStream.Create(sessionState, AmqpConstants.SegmentSize);
                    var value = buffer.ReadBytes((int) buffer.Length);
                    amqpRequestMessage.Map[ManagementConstants.Properties.SessionState] = value;
                }
                else
                {
                    amqpRequestMessage.Map[ManagementConstants.Properties.SessionState] = null;
                }

                var amqpResponseMessage = await InnerMessageReceiver.ExecuteRequestResponseAsync(amqpRequestMessage).ConfigureAwait(false);
                if (amqpResponseMessage.StatusCode != AmqpResponseStatusCode.OK)
                    throw amqpResponseMessage.ToMessagingContractException();
            }
            catch (Exception exception)
            {
                throw AmqpExceptionHelper.GetClientException(exception);
            }
        }

        protected async Task OnRenewLockAsync()
        {
            try
            {
                var amqpRequestMessage = AmqpRequestMessage.CreateRequest(ManagementConstants.Operations.RenewSessionLockOperation, OperationTimeout, null);
                amqpRequestMessage.Map[ManagementConstants.Properties.SessionId] = SessionId;

                var amqpResponseMessage = await InnerMessageReceiver.ExecuteRequestResponseAsync(amqpRequestMessage).ConfigureAwait(false);

                if (amqpResponseMessage.StatusCode == AmqpResponseStatusCode.OK)
                    LockedUntilUtc = amqpResponseMessage.GetValue<DateTime>(ManagementConstants.Properties.Expiration);
                else
                    throw amqpResponseMessage.ToMessagingContractException();
            }
            catch (Exception exception)
            {
                throw AmqpExceptionHelper.GetClientException(exception);
            }
        }
    }
}