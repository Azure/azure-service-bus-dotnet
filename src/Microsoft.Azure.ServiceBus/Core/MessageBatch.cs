// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.ServiceBus.Amqp;
    using Microsoft.Azure.ServiceBus.Diagnostics;

    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    public class MessageBatch : IDisposable
    {
        internal readonly ulong maximumBatchSize;
        private readonly Func<Message, Task<Message>> pluginsCallback;
        private AmqpMessage firstMessage;
        private readonly List<Data> datas;
        private AmqpMessage result;
        private (string messageId, string sessionId, string partitionKey, string viaPartitionKey) originalMessageData;

        /// <summary>
        /// Construct a new batch with a maximum batch size and outgoing plugins callback.
        /// <remarks>
        /// To construct a batch at run-time, use <see cref="MessageSender"/>, <see cref="QueueClient"/>, or <see cref="TopicClient"/>.
        /// Use this constructor for testing and custom implementations.
        /// </remarks>
        /// </summary>
        /// <param name="maximumBatchSize">Maximum batch size allowed for batch.</param>
        /// <param name="pluginsCallback">Plugins callback to invoke on outgoing messages regisered with batch.</param>
        internal MessageBatch(ulong maximumBatchSize, Func<Message, Task<Message>> pluginsCallback)
        {
            this.maximumBatchSize = maximumBatchSize;
            this.pluginsCallback = pluginsCallback;
            this.datas = new List<Data>();
            this.result = AmqpMessage.Create(datas);
        }

        /// <summary>
        /// Add <see cref="Message"/> to the batch if the overall size of the batch with the added message is not exceeding the batch maximum.
        /// </summary>
        /// <param name="message"><see cref="Message"/> to add to the batch.</param>
        /// <returns></returns>
        public async Task<bool> TryAdd(Message message)
        {
            ThrowIfDisposed();

            message.VerifyMessageIsNotPreviouslyReceived();

            var processedMessage = await pluginsCallback(message);

            var amqpMessage = AmqpMessageConverter.SBMessageToAmqpMessage(processedMessage);

            if (firstMessage == null)
            {
                originalMessageData = (processedMessage.MessageId, processedMessage.SessionId, processedMessage.PartitionKey, processedMessage.ViaPartitionKey);
                firstMessage = amqpMessage;
            }

            var data = AmqpMessageConverter.ToData(amqpMessage);
            datas.Add(data);

            if (Size <= maximumBatchSize)
            {
                return true;
            }

            datas.Remove(data);
            return false;

        }

        /// <summary>
        /// Number of messages in batch.
        /// </summary>
        public int Length => datas.Count;

        internal ulong Size => (ulong) result.SerializedMessageSize;


        /// <summary>
        /// Convert batch to AMQP message.
        /// </summary>
        /// <returns></returns>
        internal AmqpMessage ToAmqpMessage()
        {
            ThrowIfDisposed();

            if (datas.Count == 1)
            {
                firstMessage.Batchable = true;
                return firstMessage;
            }

            if (originalMessageData.messageId != null)
            {
                result.Properties.MessageId = originalMessageData.messageId;
            }

            if (originalMessageData.sessionId != null)
            {
                result.Properties.GroupId = originalMessageData.sessionId;
            }

            if (originalMessageData.partitionKey != null)
            {
                result.MessageAnnotations.Map[AmqpMessageConverter.PartitionKeyName] = originalMessageData.partitionKey;
            }

            if (originalMessageData.viaPartitionKey != null)
            {
                result.MessageAnnotations.Map[AmqpMessageConverter.ViaPartitionKeyName] = originalMessageData.viaPartitionKey;
            }

            result.MessageFormat = AmqpConstants.AmqpBatchedMessageFormat;
            result.Batchable = true;
            return result;
        }

        public void Dispose()
        {
            // TODO: review if there's anything else to do
            firstMessage?.Dispose();
            result?.Dispose();

            firstMessage = null;
            result = null;
        }

        private void ThrowIfDisposed()
        {
            if (result == null)
            {
                throw new ObjectDisposedException("MessageBatch is has been disposed and cannot be re-used.");
            }
        }

        private string DebuggerDisplay => $"MessageBatch: size={Size}, message count={datas.Count}, maximum size={maximumBatchSize}.";
    }
}