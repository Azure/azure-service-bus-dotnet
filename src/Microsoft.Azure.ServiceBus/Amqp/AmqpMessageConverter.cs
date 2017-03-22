// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Amqp
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Encoding;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Messaging.Amqp;
    using Microsoft.Azure.ServiceBus.Filters;
    using BrokeredMessage = Microsoft.Azure.ServiceBus.Message;

    static class AmqpMessageConverter
    {
        const string EnqueuedTimeUtcName = "x-opt-enqueued-time";
        const string ScheduledEnqueueTimeUtcName = "x-opt-scheduled-enqueue-time";
        const string SequenceNumberName = "x-opt-sequence-number";
        const string OffsetName = "x-opt-offset";
        const string LockedUntilName = "x-opt-locked-until";
        const string PublisherName = "x-opt-publisher";
        const string PartitionKeyName = "x-opt-partition-key";
        const string PartitionIdName = "x-opt-partition-id";
        const string DeadLetterSourceName = "x-opt-deadletter-source";
        const string TimeSpanName = AmqpConstants.Vendor + ":timespan";
        const string UriName = AmqpConstants.Vendor + ":uri";
        const string DateTimeOffsetName = AmqpConstants.Vendor + ":datetime-offset";
        const int GuidSize = 16;

        public static AmqpMessage BatchBrokeredMessagesAsAmqpMessage(IEnumerable<BrokeredMessage> brokeredMessages, bool batchable)
        {
            if (brokeredMessages == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(brokeredMessages));
            }

            AmqpMessage amqpMessage;
            AmqpMessage firstAmqpMessage = null;
            BrokeredMessage firstMessage = null;
            List<Data> dataList = null;
            int messageCount = 0;
            foreach (var brokeredMessage in brokeredMessages)
            {
                messageCount++;

                amqpMessage = AmqpMessageConverter.BrokeredMessageToAmqpMessage(brokeredMessage);
                if (firstAmqpMessage == null)
                {
                    firstAmqpMessage = amqpMessage;
                    firstMessage = brokeredMessage;
                    continue;
                }

                if (dataList == null)
                {
                    dataList = new List<Data>() { ToData(firstAmqpMessage) };
                }

                dataList.Add(ToData(amqpMessage));
            }

            if (messageCount == 1 && firstAmqpMessage != null)
            {
                firstAmqpMessage.Batchable = batchable;
                return firstAmqpMessage;
            }

            amqpMessage = AmqpMessage.Create(dataList);
            amqpMessage.MessageFormat = AmqpConstants.AmqpBatchedMessageFormat;

            if (firstMessage.MessageId != null)
            {
                amqpMessage.Properties.MessageId = firstMessage.MessageId;
            }
            if (firstMessage.SessionId != null)
            {
                amqpMessage.Properties.GroupId = firstMessage.SessionId;
            }

            if (firstMessage.PartitionKey != null)
            {
                amqpMessage.MessageAnnotations.Map[AmqpMessageConverter.PartitionKeyName] =
                    firstMessage.PartitionKey;
            }

            amqpMessage.Batchable = batchable;
            return amqpMessage;
        }

        public static AmqpMessage BrokeredMessageToAmqpMessage(BrokeredMessage brokeredMessage)
        {
            var amqpMessage = AmqpMessage.Create(new AmqpValue() { Value = brokeredMessage.Body });

            amqpMessage.Properties.MessageId = brokeredMessage.MessageId;
            amqpMessage.Properties.CorrelationId = brokeredMessage.CorrelationId;
            amqpMessage.Properties.ContentType = brokeredMessage.ContentType;
            amqpMessage.Properties.Subject = brokeredMessage.Label;
            amqpMessage.Properties.To = brokeredMessage.To;
            amqpMessage.Properties.ReplyTo = brokeredMessage.ReplyTo;
            amqpMessage.Properties.GroupId = brokeredMessage.SessionId;
            amqpMessage.Properties.ReplyToGroupId = brokeredMessage.ReplyToSessionId;

            if (brokeredMessage.TimeToLive != null)
            {
                amqpMessage.Header.Ttl = (uint)brokeredMessage.TimeToLive.TotalMilliseconds;
                amqpMessage.Properties.CreationTime = DateTime.UtcNow;

                if (AmqpConstants.MaxAbsoluteExpiryTime - amqpMessage.Properties.CreationTime.Value > brokeredMessage.TimeToLive)
                {
                    amqpMessage.Properties.AbsoluteExpiryTime = amqpMessage.Properties.CreationTime.Value + brokeredMessage.TimeToLive;
                }
                else
                {
                    amqpMessage.Properties.AbsoluteExpiryTime = AmqpConstants.MaxAbsoluteExpiryTime;
                }
            }

            if ((brokeredMessage.ScheduledEnqueueTimeUtc != null) && brokeredMessage.ScheduledEnqueueTimeUtc > DateTime.MinValue)
            {
                amqpMessage.MessageAnnotations.Map.Add(ScheduledEnqueueTimeUtcName, brokeredMessage.ScheduledEnqueueTimeUtc);
            }

            if (brokeredMessage.Publisher != null)
            {
                amqpMessage.MessageAnnotations.Map.Add(PublisherName, brokeredMessage.Publisher);
            }

            if (brokeredMessage.DeadLetterSource != null)
            {
                amqpMessage.MessageAnnotations.Map.Add(DeadLetterSourceName, brokeredMessage.DeadLetterSource);
            }

            if (brokeredMessage.PartitionKey != null)
            {
                amqpMessage.MessageAnnotations.Map.Add(PartitionKeyName, brokeredMessage.PartitionKey);
            }

            if (brokeredMessage.UserProperties != null && brokeredMessage.UserProperties.Count > 0)
            {
                if (amqpMessage.ApplicationProperties == null)
                {
                    amqpMessage.ApplicationProperties = new ApplicationProperties();
                }

                foreach (var pair in brokeredMessage.UserProperties)
                {
                    object amqpObject;
                    if (TryGetAmqpObjectFromNetObject(pair.Value, MappingType.ApplicationProperty, out amqpObject))
                    {
                        amqpMessage.ApplicationProperties.Map.Add(pair.Key, amqpObject);
                    }
                }
            }

            return amqpMessage;
        }

        public static BrokeredMessage AmqpMessageToBrokeredMessage(AmqpMessage amqpMessage)
        {
            if (amqpMessage == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(amqpMessage));
            }

            BrokeredMessage brokeredMessage;

            // ToDo: Add additional tests for AMQP interoperability. This may pose some issues,
            // and we should be able to parse the message body for different types.
            if (amqpMessage.BodyType == SectionFlag.AmqpValue && amqpMessage.ValueBody.Value != null)
            {
                var arraySegment = (ArraySegment<byte>)amqpMessage.ValueBody.Value;
                brokeredMessage = new BrokeredMessage(arraySegment);
            }
            else
            {
                brokeredMessage = new BrokeredMessage();
            }

            SectionFlag sections = amqpMessage.Sections;
            if ((sections & SectionFlag.Header) != 0)
            {
                if (amqpMessage.Header.Ttl != null)
                {
                    brokeredMessage.TimeToLive = TimeSpan.FromMilliseconds(amqpMessage.Header.Ttl.Value);
                }

                if (amqpMessage.Header.DeliveryCount != null)
                {
                    brokeredMessage.SystemProperties.DeliveryCount = (int)(amqpMessage.Header.DeliveryCount.Value + 1);
                }
            }

            if ((sections & SectionFlag.Properties) != 0)
            {
                if (amqpMessage.Properties.MessageId != null)
                {
                    brokeredMessage.MessageId = amqpMessage.Properties.MessageId.ToString();
                }

                if (amqpMessage.Properties.CorrelationId != null)
                {
                    brokeredMessage.CorrelationId = amqpMessage.Properties.CorrelationId.ToString();
                }

                if (amqpMessage.Properties.ContentType.Value != null)
                {
                    brokeredMessage.ContentType = amqpMessage.Properties.ContentType.Value;
                }

                if (amqpMessage.Properties.Subject != null)
                {
                    brokeredMessage.Label = amqpMessage.Properties.Subject;
                }

                if (amqpMessage.Properties.To != null)
                {
                    brokeredMessage.To = amqpMessage.Properties.To.ToString();
                }

                if (amqpMessage.Properties.ReplyTo != null)
                {
                    brokeredMessage.ReplyTo = amqpMessage.Properties.ReplyTo.ToString();
                }

                if (amqpMessage.Properties.GroupId != null)
                {
                    brokeredMessage.SessionId = amqpMessage.Properties.GroupId;
                }

                if (amqpMessage.Properties.ReplyToGroupId != null)
                {
                    brokeredMessage.ReplyToSessionId = amqpMessage.Properties.ReplyToGroupId;
                }
            }

            // Do applicaiton properties before message annotations, because the application properties
            // can be updated by entries from message annotation.
            if ((sections & SectionFlag.ApplicationProperties) != 0)
            {
                foreach (var pair in amqpMessage.ApplicationProperties.Map)
                {
                    object netObject;
                    if (TryGetNetObjectFromAmqpObject(pair.Value, MappingType.ApplicationProperty, out netObject))
                    {
                        brokeredMessage.UserProperties[pair.Key.ToString()] = netObject;
                    }
                }
            }

            if ((sections & SectionFlag.MessageAnnotations) != 0)
            {
                foreach (var pair in amqpMessage.MessageAnnotations.Map)
                {
                    string key = pair.Key.ToString();
                    switch (key)
                    {
                        case EnqueuedTimeUtcName:
                            brokeredMessage.SystemProperties.EnqueuedTimeUtc = (DateTime)pair.Value;
                            break;
                        case ScheduledEnqueueTimeUtcName:
                            brokeredMessage.ScheduledEnqueueTimeUtc = (DateTime)pair.Value;
                            break;
                        case SequenceNumberName:
                            brokeredMessage.SystemProperties.SequenceNumber = (long)pair.Value;
                            break;
                        case OffsetName:
                            brokeredMessage.SystemProperties.EnqueuedSequenceNumber = long.Parse((string)pair.Value);
                            break;
                        case LockedUntilName:
                            brokeredMessage.SystemProperties.LockedUntilUtc = (DateTime)pair.Value;
                            break;
                        case PublisherName:
                            brokeredMessage.Publisher = (string)pair.Value;
                            break;
                        case PartitionKeyName:
                            brokeredMessage.PartitionKey = (string)pair.Value;
                            break;
                        case PartitionIdName:
                            brokeredMessage.SystemProperties.PartitionId = (short)pair.Value;
                            break;
                        case DeadLetterSourceName:
                            brokeredMessage.DeadLetterSource = (string)pair.Value;
                            break;
                        default:
                            object netObject;
                            if (TryGetNetObjectFromAmqpObject(pair.Value, MappingType.ApplicationProperty, out netObject))
                            {
                                brokeredMessage.UserProperties[key] = netObject;
                            }
                            break;
                    }
                }
            }

            if (amqpMessage.DeliveryTag.Count == GuidSize)
            {
                byte[] guidBuffer = new byte[GuidSize];
                Buffer.BlockCopy(amqpMessage.DeliveryTag.Array, amqpMessage.DeliveryTag.Offset, guidBuffer, 0, GuidSize);
                brokeredMessage.SystemProperties.LockTokenGuid = new Guid(guidBuffer);
            }

            amqpMessage.Dispose();

            return brokeredMessage;
        }

        public static AmqpMap GetRuleDescriptionMap(RuleDescription description)
        {
            AmqpMap ruleDescriptionMap = new AmqpMap();
            if (description.Filter is SqlFilter)
            {
                AmqpMap filterMap = GetSqlFilterMap(description.Filter as SqlFilter);
                ruleDescriptionMap[ManagementConstants.Properties.SqlFilter] = filterMap;
            }
            else if (description.Filter is CorrelationFilter)
            {
                AmqpMap correlationFilterMap = GetCorrelationFilterMap(description.Filter as CorrelationFilter);
                ruleDescriptionMap[ManagementConstants.Properties.CorrelationFilter] = correlationFilterMap;
            }
            else
            {
                throw new NotSupportedException(
                    Resources.RuleFilterNotSupported.FormatForUser(
                        description.Filter.GetType(),
                        nameof(SqlFilter),
                        nameof(CorrelationFilter)));
            }

            AmqpMap amqpAction = GetRuleActionMap(description.Action as SqlRuleAction);
            ruleDescriptionMap[ManagementConstants.Properties.SqlRuleAction] = amqpAction;

            return ruleDescriptionMap;
        }

        static bool TryGetAmqpObjectFromNetObject(object netObject, MappingType mappingType, out object amqpObject)
        {
            amqpObject = null;
            if (netObject == null)
            {
                return true;
            }

            switch (SerializationUtilities.GetTypeId(netObject))
            {
                case PropertyValueType.Byte:
                case PropertyValueType.SByte:
                case PropertyValueType.Int16:
                case PropertyValueType.Int32:
                case PropertyValueType.Int64:
                case PropertyValueType.UInt16:
                case PropertyValueType.UInt32:
                case PropertyValueType.UInt64:
                case PropertyValueType.Single:
                case PropertyValueType.Double:
                case PropertyValueType.Boolean:
                case PropertyValueType.Decimal:
                case PropertyValueType.Char:
                case PropertyValueType.Guid:
                case PropertyValueType.DateTime:
                case PropertyValueType.String:
                    amqpObject = netObject;
                    break;
                case PropertyValueType.Stream:
                    if (mappingType == MappingType.ApplicationProperty)
                    {
                        amqpObject = StreamToBytes((Stream)netObject);
                    }
                    break;
                case PropertyValueType.Uri:
                    amqpObject = new DescribedType((AmqpSymbol)UriName, ((Uri)netObject).AbsoluteUri);
                    break;
                case PropertyValueType.DateTimeOffset:
                    amqpObject = new DescribedType((AmqpSymbol)DateTimeOffsetName, ((DateTimeOffset)netObject).UtcTicks);
                    break;
                case PropertyValueType.TimeSpan:
                    amqpObject = new DescribedType((AmqpSymbol)TimeSpanName, ((TimeSpan)netObject).Ticks);
                    break;
                case PropertyValueType.Unknown:
                    if (netObject is Stream)
                    {
                        if (mappingType == MappingType.ApplicationProperty)
                        {
                            amqpObject = StreamToBytes((Stream)netObject);
                        }
                    }
                    else if (mappingType == MappingType.ApplicationProperty)
                    {
                        throw Fx.Exception.AsError(new SerializationException(Resources.FailedToSerializeUnsupportedType.FormatForUser(netObject.GetType().FullName)));
                    }
                    else if (netObject is byte[])
                    {
                        amqpObject = new ArraySegment<byte>((byte[])netObject);
                    }
                    else if (netObject is IList)
                    {
                        // Array is also an IList
                        amqpObject = netObject;
                    }
                    else if (netObject is IDictionary)
                    {
                        amqpObject = new AmqpMap((IDictionary)netObject);
                    }
                    break;
            }

            return amqpObject != null;
        }

        static bool TryGetNetObjectFromAmqpObject(object amqpObject, MappingType mappingType, out object netObject)
        {
            netObject = null;
            if (amqpObject == null)
            {
                return true;
            }

            switch (SerializationUtilities.GetTypeId(amqpObject))
            {
                case PropertyValueType.Byte:
                case PropertyValueType.SByte:
                case PropertyValueType.Int16:
                case PropertyValueType.Int32:
                case PropertyValueType.Int64:
                case PropertyValueType.UInt16:
                case PropertyValueType.UInt32:
                case PropertyValueType.UInt64:
                case PropertyValueType.Single:
                case PropertyValueType.Double:
                case PropertyValueType.Boolean:
                case PropertyValueType.Decimal:
                case PropertyValueType.Char:
                case PropertyValueType.Guid:
                case PropertyValueType.DateTime:
                case PropertyValueType.String:
                    netObject = amqpObject;
                    break;
                case PropertyValueType.Unknown:
                    if (amqpObject is AmqpSymbol)
                    {
                        netObject = ((AmqpSymbol)amqpObject).Value;
                    }
                    else if (amqpObject is ArraySegment<byte>)
                    {
                        ArraySegment<byte> binValue = (ArraySegment<byte>)amqpObject;
                        if (binValue.Count == binValue.Array.Length)
                        {
                            netObject = binValue.Array;
                        }
                        else
                        {
                            byte[] buffer = new byte[binValue.Count];
                            Buffer.BlockCopy(binValue.Array, binValue.Offset, buffer, 0, binValue.Count);
                            netObject = buffer;
                        }
                    }
                    else if (amqpObject is DescribedType)
                    {
                        DescribedType describedType = (DescribedType)amqpObject;
                        if (describedType.Descriptor is AmqpSymbol)
                        {
                            AmqpSymbol symbol = (AmqpSymbol)describedType.Descriptor;
                            if (symbol.Equals((AmqpSymbol)UriName))
                            {
                                netObject = new Uri((string)describedType.Value);
                            }
                            else if (symbol.Equals((AmqpSymbol)TimeSpanName))
                            {
                                netObject = new TimeSpan((long)describedType.Value);
                            }
                            else if (symbol.Equals((AmqpSymbol)DateTimeOffsetName))
                            {
                                netObject = new DateTimeOffset(new DateTime((long)describedType.Value, DateTimeKind.Utc));
                            }
                        }
                    }
                    else if (mappingType == MappingType.ApplicationProperty)
                    {
                        throw Fx.Exception.AsError(new SerializationException(Resources.FailedToSerializeUnsupportedType.FormatForUser(amqpObject.GetType().FullName)));
                    }
                    else if (amqpObject is AmqpMap)
                    {
                        AmqpMap map = (AmqpMap)amqpObject;
                        Dictionary<string, object> dictionary = new Dictionary<string, object>();
                        foreach (var pair in map)
                        {
                            dictionary.Add(pair.Key.ToString(), pair.Value);
                        }

                        netObject = dictionary;
                    }
                    else
                    {
                        netObject = amqpObject;
                    }
                    break;
            }

            return netObject != null;
        }

        static ArraySegment<byte> StreamToBytes(Stream stream)
        {
            ArraySegment<byte> buffer;
            if (stream == null || stream.Length < 1)
            {
                buffer = default(ArraySegment<byte>);
            }
            else
            {
                using (var memoryStream = new MemoryStream(512))
                {
                    stream.CopyTo(memoryStream, 512);
                    buffer = new ArraySegment<byte>(memoryStream.ToArray());
                }
            }

            return buffer;
        }

        private static Data ToData(AmqpMessage message)
        {
            ArraySegment<byte>[] payload = message.GetPayload();
            BufferListStream buffer = new BufferListStream(payload);
            ArraySegment<byte> value = buffer.ReadBytes((int)buffer.Length);
            return new Data { Value = value };
        }

        static AmqpMap GetSqlFilterMap(SqlFilter sqlFilter)
        {
            AmqpMap amqpFilterMap = new AmqpMap
            {
                [ManagementConstants.Properties.Expression] = sqlFilter.SqlExpression
            };
            return amqpFilterMap;
        }

        static AmqpMap GetCorrelationFilterMap(CorrelationFilter correlationFilter)
        {
            AmqpMap correlationFilterMap = new AmqpMap
            {
                [ManagementConstants.Properties.CorrelationId] = correlationFilter.CorrelationId,
                [ManagementConstants.Properties.MessageId] = correlationFilter.MessageId,
                [ManagementConstants.Properties.To] = correlationFilter.To,
                [ManagementConstants.Properties.ReplyTo] = correlationFilter.ReplyTo,
                [ManagementConstants.Properties.Label] = correlationFilter.Label,
                [ManagementConstants.Properties.SessionId] = correlationFilter.SessionId,
                [ManagementConstants.Properties.ReplyToSessionId] = correlationFilter.ReplyToSessionId,
                [ManagementConstants.Properties.ContentType] = correlationFilter.ContentType
            };

            var propertiesMap = new AmqpMap();
            foreach (var property in correlationFilter.Properties)
            {
                propertiesMap[new MapKey(property.Key)] = property.Value;
            }

            correlationFilterMap[ManagementConstants.Properties.CorrelationFilterProperties] = propertiesMap;

            return correlationFilterMap;
        }

        static AmqpMap GetRuleActionMap(SqlRuleAction sqlRuleAction)
        {
            AmqpMap ruleActionMap = null;
            if (sqlRuleAction != null)
            {
                ruleActionMap = new AmqpMap { [ManagementConstants.Properties.Expression] = sqlRuleAction.SqlExpression };
            }

            return ruleActionMap;
        }
    }
}