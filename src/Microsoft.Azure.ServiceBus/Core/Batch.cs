using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Azure.ServiceBus.Amqp;

namespace Microsoft.Azure.ServiceBus.Core
{
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    public class Batch : IDisposable
    {
        private readonly long maximumBatchSize;
        private AmqpMessage firstMessage;
        private readonly List<Data> datas;
        private AmqpMessage result;
        private (string messageId, string sessionId, string partitionKey, string viaPartitionKey) originalMessageData;

        /// <summary>
        /// Construct a new batch with a maximum batch size.
        /// </summary>
        /// <param name="maximumBatchSize">Maximum batch size allowed for batch.</param>
        public Batch(long maximumBatchSize)
        {
            this.maximumBatchSize = maximumBatchSize;
            this.datas = new List<Data>();
            this.result = AmqpMessage.Create(datas);
        }

        /// <summary>
        /// Add <see cref="Message"/> to the batch if the overall size of the batch with the added message is not exceeding the batch maximum.
        /// </summary>
        /// <param name="message"><see cref="Message"/> to add to the batch.</param>
        /// <returns></returns>
        public bool TryAdd(Message message)
        {
            ThrowIfDisposed();

            var amqpMessage = AmqpMessageConverter.SBMessageToAmqpMessage(message);

            if (firstMessage == null)
            {
                originalMessageData = (message.MessageId, message.SessionId, message.PartitionKey, message.ViaPartitionKey);
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

        private long Size => result.SerializedMessageSize;

        /// <summary>
        /// Convert batch to AMQP message
        /// </summary>
        /// <returns></returns>
        public AmqpMessage ToAmqpMessage()
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
                throw new Exception("Batch is has been disposed and cannot be re-used.");
            }
        }

        private string DebuggerDisplay => $"Batch: size={Size} message count={datas.Count}";
    }
}