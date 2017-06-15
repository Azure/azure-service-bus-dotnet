// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus
{
    /// <summary>
    ///     The object used to communicate and transfer data with Service Bus.
    /// </summary>
    public class Message
    {
        string messageId;
        string partitionKey;
        string replyToSessionId;
        string sessionId;
        TimeSpan timeToLive;
        string viaPartitionKey;

        /// <summary>
        ///     Creates a new Message
        /// </summary>
        public Message()
            : this(null)
        {
        }

        /// <summary>
        ///     Creates a new message from the specified payload.
        /// </summary>
        /// <param name="body"></param>
        public Message(byte[] body)
        {
            Body = body;
            SystemProperties = new SystemPropertiesCollection();
            UserProperties = new Dictionary<string, object>();
        }

        /// <summary>
        ///     Gets or sets the body of the message.
        /// </summary>
        /// <remarks>
        ///     The easiest way to create a new message from a string is the following:
        ///     <code>
        /// message.Body = System.Text.Encoding.UTF8.GetBytes("Message1");
        /// </code>
        /// </remarks>
        public byte[] Body { get; set; }

        /// <summary>
        ///     Gets or sets the MessageId.
        /// </summary>
        /// <remarks>A value set by the user to uniquely identify the message. This value will be used for message deduplication.</remarks>
        public string MessageId
        {
            get => messageId;

            set
            {
                ValidateMessageId(value);
                messageId = value;
            }
        }

        /// <summary>
        ///     Gets or sets a partition key for sending a transactional message to a queue or topic that is not
        ///     session-aware.
        /// </summary>
        /// <value>The partition key for sending a transactional message.</value>
        /// <remarks>Transactions are not currently supported with this library.</remarks>
        public string PartitionKey
        {
            get => partitionKey;

            set
            {
                ValidatePartitionKey(nameof(PartitionKey), value);
                partitionKey = value;
            }
        }

        /// <summary>Gets or sets a partition key value when a transaction is to be used to send messages via a transfer queue.</summary>
        /// <value>The partition key value when a transaction is to be used to send messages via a transfer queue.</value>
        public string ViaPartitionKey
        {
            get => viaPartitionKey;

            set
            {
                ValidatePartitionKey(nameof(ViaPartitionKey), value);
                viaPartitionKey = value;
            }
        }

        /// <summary>Gets or sets the identifier of the session.</summary>
        /// <value>The identifier of the session.</value>
        public string SessionId
        {
            get => sessionId;

            set
            {
                ValidateSessionId(nameof(SessionId), value);
                sessionId = value;
            }
        }

        /// <summary>Gets or sets the session identifier to reply to.</summary>
        /// <value>The session identifier to reply to.</value>
        public string ReplyToSessionId
        {
            get => replyToSessionId;

            set
            {
                ValidateSessionId(nameof(ReplyToSessionId), value);
                replyToSessionId = value;
            }
        }

        /// <summary>Gets the date and time in UTC at which the message is set to expire.</summary>
        /// <value>The message expiration time in UTC.</value>
        /// <exception cref="System.InvalidOperationException">
        ///     If the message has not been received. For example if a new message
        ///     was created but not yet sent and received.
        /// </exception>
        public DateTime ExpiresAtUtc
        {
            get
            {
                if (TimeToLive >= DateTime.MaxValue.Subtract(SystemProperties.EnqueuedTimeUtc))
                    return DateTime.MaxValue;

                return SystemProperties.EnqueuedTimeUtc.Add(TimeToLive);
            }
        }

        /// <summary>
        ///     Gets or sets the message’s time to live value. This is the duration after which the message expires, starting from
        ///     when the message is sent to the Service Bus. Messages older than their TimeToLive value will expire and no longer
        ///     be retained in the message store. Subscribers will be unable to receive expired messages. TimeToLive is the maximum
        ///     lifetime that a message can be received, but its value cannot exceed the entity specified
        ///     value on the destination queue or subscription. If a lower TimeToLive value is specified, it will be applied to the
        ///     individual message. However, a larger value specified on the message will be overridden by the entity’s
        ///     DefaultMessageTimeToLive value.
        /// </summary>
        /// <value>The message’s time to live value.</value>
        /// <remarks>
        ///     If the TTL set on a message by the sender exceeds the destination's TTL, then the message's TTL will be
        ///     overwritten by the later one.
        /// </remarks>
        public TimeSpan TimeToLive
        {
            get
            {
                if (timeToLive == TimeSpan.Zero)
                    return TimeSpan.MaxValue;

                return timeToLive;
            }

            set
            {
                TimeoutHelper.ThrowIfNonPositiveArgument(value);
                timeToLive = value;
            }
        }

        /// <summary>Gets or sets the identifier of the correlation.</summary>
        /// <value>The identifier of the correlation.</value>
        public string CorrelationId { get; set; }

        /// <summary>Gets or sets the application specific label.</summary>
        /// <value>The application specific label.</value>
        public string Label { get; set; }

        /// <summary>Gets or sets the send to address.</summary>
        /// <value>The send to address.</value>
        public string To { get; set; }

        /// <summary>Gets or sets the type of the content.</summary>
        /// <value>
        ///     The type of the content of the message body. This is a
        ///     content type identifier utilized by the sender and receiver for application specific logic.
        /// </value>
        public string ContentType { get; set; }

        /// <summary>Gets or sets the address of the queue to reply to.</summary>
        /// <value>The reply to queue address.</value>
        public string ReplyTo { get; set; }

        /// <summary> Gets or sets the the Publisher. </summary>
        /// <value> Identifies the Publisher Sending the Message. </value>
        public string Publisher { get; set; }

        /// <summary>
        ///     Gets the name of the queue or subscription that this message was enqueued on, before it was deadlettered.
        /// </summary>
        public string DeadLetterSource { get; set; }

        /// <summary>
        ///     Gets or sets the date and time in UTC at which the message will be enqueued. This
        ///     property returns the time in UTC; when setting the property, the supplied DateTime value must also be in UTC.
        /// </summary>
        /// <value>
        ///     The scheduled enqueue time in UTC. This value is for delayed message sending.
        ///     It is utilized to delay messages sending to a specific time in the future.
        /// </value>
        /// <remarks>
        ///     Message enquing time does not mean that the message will be sent at the same time. It will get enqueued, but the
        ///     actual sending time
        ///     depends on the queue's workload and its state.
        /// </remarks>
        public DateTime ScheduledEnqueueTimeUtc { get; set; }

        // TODO: Calculate the size of the properties and body
        /// <summary>
        ///     Gets the total size of the message in bytes.
        /// </summary>
        public long Size { get; internal set; }

        /// <summary>
        ///     Gets the user property bag, which can be used for custom message properties.
        /// </summary>
        public IDictionary<string, object> UserProperties { get; internal set; }

        /// <summary>
        ///     Gets the <see cref="SystemPropertiesCollection" />, which is used to store properties that are set by the system.
        /// </summary>
        public SystemPropertiesCollection SystemProperties { get; internal set; }

        /// <summary>Returns a string that represents the current message.</summary>
        /// <returns>The string representation of the current message.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentCulture, $"{{MessageId:{MessageId}}}");
        }

        /// <summary>Clones a message, so that it is possible to send a clone of a message as a new message.</summary>
        /// <returns>The <see cref="Message" /> that contains the cloned message.</returns>
        public Message Clone()
        {
            var clone = (Message) MemberwiseClone();
            clone.SystemProperties = new SystemPropertiesCollection();

            if (Body != null)
            {
                var clonedBody = new byte[Body.Length];
                Array.Copy(Body, clonedBody, Body.Length);
                clone.Body = clonedBody;
            }
            return clone;
        }

        static void ValidateMessageId(string messageId)
        {
            if (string.IsNullOrEmpty(messageId) ||
                messageId.Length > Constants.MaxMessageIdLength)
                throw new ArgumentException("MessageIdIsNullOrEmptyOrOverMaxValue");
        }

        static void ValidateSessionId(string sessionIdPropertyName, string sessionId)
        {
            if (sessionId != null && sessionId.Length > Constants.MaxSessionIdLength)
                throw new ArgumentException("SessionIdIsOverMaxValue");
        }

        static void ValidatePartitionKey(string partitionKeyPropertyName, string partitionKey)
        {
            if (partitionKey != null && partitionKey.Length > Constants.MaxPartitionKeyLength)
                throw new ArgumentException("PropertyValueOverMaxValue");
        }

        /// <summary>
        ///     A collection used to store properties which are set by the Service Bus service.
        /// </summary>
        public sealed class SystemPropertiesCollection
        {
            int deliveryCount;

            long enqueuedSequenceNumber;

            DateTime enqueuedTimeUtc;

            DateTime lockedUntilUtc;

            Guid lockTokenGuid;

            short partitionId;

            long sequenceNumber = -1;

            /// <summary>
            ///     Specifies whether or not there is a lock token set on the current message.
            /// </summary>
            /// <remarks>
            ///     A lock token will only be specified if the message was received using <see cref="ReceiveMode.PeekLock" />
            /// </remarks>
            public bool IsLockTokenSet => lockTokenGuid != default(Guid);

            /// <summary>
            ///     Gets the lock token for the current message.
            /// </summary>
            /// <remarks>
            ///     A lock token will only be specified if the message was received using <see cref="ReceiveMode.PeekLock" />
            /// </remarks>
            public string LockToken => LockTokenGuid.ToString();

            /// <summary>Specifies if message is a received message or not.</summary>
            public bool IsReceived => sequenceNumber > -1;

            /// <summary>
            ///     Get the current delivery count.
            /// </summary>
            /// <value>This value starts at 1.</value>
            public int DeliveryCount
            {
                get
                {
                    ThrowIfNotReceived();
                    return deliveryCount;
                }

                internal set => deliveryCount = value;
            }

            /// <summary>Gets the date and time in UTC until which the message will be locked in the queue/subscription.</summary>
            /// <value>The date and time until which the message will be locked in the queue/subscription.</value>
            public DateTime LockedUntilUtc
            {
                get
                {
                    ThrowIfNotReceived();
                    return lockedUntilUtc;
                }

                internal set => lockedUntilUtc = value;
            }

            /// <summary>Gets the unique number assigned to a message by Service Bus, for this entity.</summary>
            public long SequenceNumber
            {
                get
                {
                    ThrowIfNotReceived();
                    return sequenceNumber;
                }

                internal set => sequenceNumber = value;
            }

            internal short PartitionId
            {
                get
                {
                    ThrowIfNotReceived();
                    return partitionId;
                }

                set => partitionId = value;
            }

            /// <summary>Gets or sets the enqueued sequence number of the message.</summary>
            /// <value>The enqueued sequence number of the message.</value>
            public long EnqueuedSequenceNumber
            {
                get
                {
                    ThrowIfNotReceived();
                    return enqueuedSequenceNumber;
                }

                internal set => enqueuedSequenceNumber = value;
            }

            /// <summary>Gets or sets the date and time of the sent time in UTC.</summary>
            /// <value>The enqueue time in UTC. This value represents the actual time of enqueuing the message.</value>
            public DateTime EnqueuedTimeUtc
            {
                get
                {
                    ThrowIfNotReceived();
                    return enqueuedTimeUtc;
                }

                internal set => enqueuedTimeUtc = value;
            }

            internal Guid LockTokenGuid
            {
                get
                {
                    ThrowIfNotReceived();
                    return lockTokenGuid;
                }

                set => lockTokenGuid = value;
            }

            void ThrowIfNotReceived()
            {
                if (!IsReceived)
                    throw Fx.Exception.AsError(new InvalidOperationException());
            }
        }
    }
}