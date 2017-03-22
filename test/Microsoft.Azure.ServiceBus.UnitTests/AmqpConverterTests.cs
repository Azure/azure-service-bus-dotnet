// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using System.Text;
    using Microsoft.Azure.ServiceBus.Amqp;
    using Xunit;
    using BrokeredMessage = Microsoft.Azure.ServiceBus.Message;

    public class AmqpConverterTests
    {
        public class WhenConvertingBrokeredMessagesToAmqpMessages
        {
            [Fact]
            [DisplayTestMethodName]
            void EnsureMessageProeprtiesAreConverted()
            {
                var messageBody = Encoding.UTF8.GetBytes("hello");
                var messageId = Guid.NewGuid().ToString();
                var partitionKey = Guid.NewGuid().ToString();
                var sessionId = Guid.NewGuid().ToString();
                var correlationId = Guid.NewGuid().ToString();
                var label = Guid.NewGuid().ToString();
                var to = Guid.NewGuid().ToString();
                var contentType = Guid.NewGuid().ToString();
                var replyTo = Guid.NewGuid().ToString();
                var replyToSessionId = Guid.NewGuid().ToString();
                var publisher = Guid.NewGuid().ToString();
                var deadLetterSource = Guid.NewGuid().ToString();

                var brokeredMessage = new BrokeredMessage(messageBody)
                {
                    MessageId = messageId,
                    PartitionKey = partitionKey,
                    SessionId = sessionId,
                    CorrelationId = correlationId,
                    Label = label,
                    To = to,
                    ContentType = contentType,
                    ReplyTo = replyTo,
                    ReplyToSessionId = replyToSessionId,
                    Publisher = publisher,
                    DeadLetterSource = deadLetterSource,
                };
                brokeredMessage.UserProperties.Add("UserProperty", "SomeUserProperty");

                var amqpMessage = AmqpMessageConverter.BrokeredMessageToAmqpMessage(brokeredMessage);
                var convertedBrokeredMessage = AmqpMessageConverter.AmqpMessageToBrokeredMessage(amqpMessage);

                Assert.Equal("SomeUserProperty", convertedBrokeredMessage.UserProperties["UserProperty"]);
                Assert.Equal(messageBody, convertedBrokeredMessage.Body.Array);
                Assert.Equal(messageId, convertedBrokeredMessage.MessageId);
                Assert.Equal(partitionKey, convertedBrokeredMessage.PartitionKey);
                Assert.Equal(sessionId, convertedBrokeredMessage.SessionId);
                Assert.Equal(correlationId, convertedBrokeredMessage.CorrelationId);
                Assert.Equal(label, convertedBrokeredMessage.Label);
                Assert.Equal(to, convertedBrokeredMessage.To);
                Assert.Equal(contentType, convertedBrokeredMessage.ContentType);
                Assert.Equal(replyTo, convertedBrokeredMessage.ReplyTo);
                Assert.Equal(replyToSessionId, convertedBrokeredMessage.ReplyToSessionId);
                Assert.Equal(publisher, convertedBrokeredMessage.Publisher);
                Assert.Equal(deadLetterSource, convertedBrokeredMessage.DeadLetterSource);
            }
        }
    }
}