﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    public class Message
    {
        private string messageId;
        private string sessionId;
        private string replyToSessionId;
        private string partitionKey;
        private string viaPartitionKey;
        private TimeSpan timeToLive;

        public Message()
            : this(default(byte[]))
        {
        }

        /// <summary>
        /// Creates a new Message using a string that is UTF8 encoded.
        /// </summary>
        /// <param name="body">The body of the message.</param>
        public Message(string body)
            : this(Encoding.UTF8.GetBytes(body))
        {
        }

        public Message(byte[] array)
        {
            this.Body = array;
            this.SystemProperties = new SystemPropertiesCollection();
            this.UserProperties = new Dictionary<string, object>();
        }

        public byte[] Body { get; set; }

        public string MessageId
        {
            get
            {
                return this.messageId;
            }

            set
            {
                Message.ValidateMessageId(value);
                this.messageId = value;
            }
        }

        public string PartitionKey
        {
            get
            {
                return this.partitionKey;
            }

            set
            {
                Message.ValidatePartitionKey(nameof(this.PartitionKey), value);
                this.partitionKey = value;
            }
        }

        public string ViaPartitionKey
        {
            get
            {
                return this.viaPartitionKey;
            }

            set
            {
                Message.ValidatePartitionKey(nameof(this.ViaPartitionKey), value);
                this.viaPartitionKey = value;
            }
        }

        public string SessionId
        {
            get
            {
                return this.sessionId;
            }

            set
            {
                Message.ValidateSessionId(nameof(this.SessionId), value);
                this.sessionId = value;
            }
        }

        public string ReplyToSessionId
        {
            get
            {
                return this.replyToSessionId;
            }

            set
            {
                Message.ValidateSessionId(nameof(this.ReplyToSessionId), value);
                this.replyToSessionId = value;
            }
        }

        /// <summary>Gets the date and time in UTC at which the message is set to expire.</summary>
        /// <value>The message expiration time in UTC.</value>
        /// <exception cref="System.InvalidOperationException">If the message has not been received. For example if a new message was created but not yet sent and received.</exception>
        public DateTime ExpiresAtUtc
        {
            get
            {
                if (this.TimeToLive >= DateTime.MaxValue.Subtract(this.SystemProperties.EnqueuedTimeUtc))
                {
                    return DateTime.MaxValue;
                }

                return this.SystemProperties.EnqueuedTimeUtc.Add(this.TimeToLive);
            }
        }

        public TimeSpan TimeToLive
        {
            get
            {
                if (this.timeToLive == TimeSpan.Zero)
                {
                    return TimeSpan.MaxValue;
                }

                return this.timeToLive;
            }

            set
            {
                TimeoutHelper.ThrowIfNonPositiveArgument(value);
                this.timeToLive = value;
            }
        }

        public string CorrelationId { get; set; }

        public string Label { get; set; }

        public string To { get; set; }

        public string ContentType { get; set; }

        public string ReplyTo { get; set; }

        public string Publisher { get; set; }

        public DateTime ScheduledEnqueueTimeUtc { get; set; }

        public string DeadLetterSource { get; set; }

        // TODO: Calculate the size of the properties and body
        public long Size { get; set; }

        /// <summary>
        /// Returns the current body as a UTF8 encoded string.
        /// </summary>
        public string BodyAsString => Encoding.UTF8.GetString(this.Body);

        public IDictionary<string, object> UserProperties { get; internal set; }

        public SystemPropertiesCollection SystemProperties { get; internal set; }

        /// <summary>Returns a string that represents the current message.</summary>
        /// <returns>The string representation of the current message.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentCulture, $"{{MessageId:{this.MessageId}}}");
        }

        /// <summary>Clones a message, so that it is possible to send a clone of a message as a new message.</summary>
        /// <returns>The <see cref="Message" /> that contains the cloned message.</returns>
        public Message Clone()
        {
            var clone = (Message)this.MemberwiseClone();
            clone.SystemProperties = new SystemPropertiesCollection();

            if (this.Body != null)
            {
                var clonedBody = new byte[this.Body.Length];
                Array.Copy(this.Body, clonedBody, this.Body.Length);
                clone.Body = clonedBody;
            }
            return clone;
        }

        /// <summary> Validate message identifier. </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when messageId is null, or empty or greater than the maximum message length.
        /// </exception>
        /// <param name="messageId"> Identifier for the message. </param>
        private static void ValidateMessageId(string messageId)
        {
            if (string.IsNullOrEmpty(messageId) ||
                messageId.Length > Constants.MaxMessageIdLength)
            {
                // TODO: throw FxTrace.Exception.Argument("messageId", SRClient.MessageIdIsNullOrEmptyOrOverMaxValue(Constants.MaxMessageIdLength));
                throw new ArgumentException("MessageIdIsNullOrEmptyOrOverMaxValue");
            }
        }

        /// <summary> Validate session identifier. </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when sessionId is greater than the maximum session ID length.
        /// </exception>
        /// <param name="sessionId"> Identifier for the session. </param>
        private static void ValidateSessionId(string sessionIdPropertyName, string sessionId)
        {
            if (sessionId != null && sessionId.Length > Constants.MaxSessionIdLength)
            {
                // TODO: throw FxTrace.Exception.Argument("sessionId", SRClient.SessionIdIsOverMaxValue(Constants.MaxSessionIdLength));
                throw new ArgumentException("SessionIdIsOverMaxValue");
            }
        }

        private static void ValidatePartitionKey(string partitionKeyPropertyName, string partitionKey)
        {
            if (partitionKey != null && partitionKey.Length > Constants.MaxPartitionKeyLength)
            {
                // TODO: throw FxTrace.Exception.Argument(partitionKeyPropertyName, SRClient.PropertyOverMaxValue(partitionKeyPropertyName, Constants.MaxPartitionKeyLength));
                throw new ArgumentException("PropertyValueOverMaxValue");
            }
        }

        public sealed class SystemPropertiesCollection
        {
            public int DeliveryCount { get; internal set; }

            public DateTime LockedUntilUtc { get; internal set; }

            public long SequenceNumber { get; internal set; } = -1;

            public short PartitionId { get; internal set; }

            public long EnqueuedSequenceNumber { get; internal set; }

            public DateTime EnqueuedTimeUtc { get; internal set; }

            public bool IsLockTokenSet => this.LockTokenGuid != default(Guid);

            public string LockToken => this.LockTokenGuid.ToString();

            /// <summary>Specifies if message is a received message or not.</summary>
            public bool IsReceived => this.SequenceNumber > -1;

            internal Guid LockTokenGuid { get; set; }
        }
    }
}