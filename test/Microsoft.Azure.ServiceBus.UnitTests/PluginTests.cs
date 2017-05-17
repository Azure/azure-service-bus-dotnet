// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus.Core;
    using Xunit;

    public class PluginTests
    {
        [Fact]
        async Task SendPluginTest()
        {
            var messageSender = new MessageSender(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName);
            var messageReceiver = new MessageReceiver(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName, ReceiveMode.ReceiveAndDelete);

            try
            {
                var firstPlugin = new FirstSendPlugin();
                var secondPlugin = new SecondSendPlugin();

                messageSender.UsePlugin(firstPlugin);
                messageSender.UsePlugin(secondPlugin);

                var sendMessage = new Message(Encoding.UTF8.GetBytes("Test message"));
                await messageSender.SendAsync(sendMessage);

                var receivedMessage = await messageReceiver.ReceiveAsync(1, TimeSpan.FromMinutes(1));
                var firstSendPluginUserProperty = receivedMessage.First().UserProperties["FirstSendPlugin"];
                var secondSendPluginUserProperty = receivedMessage.First().UserProperties["SecondSendPlugin"];

                Assert.True((bool)firstSendPluginUserProperty);
                Assert.True((bool)secondSendPluginUserProperty);
            }
            finally
            {
                await messageSender.CloseAsync();
                await messageReceiver.CloseAsync();
            }
        }

        [Fact]
        async Task SendReceivePlugin()
        {
            var messageSender = new MessageSender(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName);
            var messageReceiver = new MessageReceiver(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName, ReceiveMode.ReceiveAndDelete);

            try
            {
                var sendReceivePlugin = new SendReceivePlugin();
                messageSender.UsePlugin(sendReceivePlugin);

                var sendMessage = new Message(Encoding.UTF8.GetBytes("Test message"))
                {
                    MessageId = Guid.NewGuid().ToString()
                };
                await messageSender.SendAsync(sendMessage);

                var receivedMessage = await messageReceiver.ReceiveAsync(1, TimeSpan.FromMinutes(1));

                Assert.Equal(sendMessage.Body, receivedMessage.First().Body);
            }

            finally
            {
                await messageSender.CloseAsync();
                await messageReceiver.CloseAsync();
            }
        }

        [Fact]
        async Task PluginWithException_ShouldHaltOperation()
        {
            var messageSender = new MessageSender(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName);
            try
            {
                var plugin = new ExceptionPlugin();
                
                messageSender.UsePlugin(plugin);

                var sendMessage = new Message(Encoding.UTF8.GetBytes("Test message"));
                await Assert.ThrowsAsync<NotImplementedException>(() => messageSender.SendAsync(sendMessage));
            }
            finally
            {
                await messageSender.CloseAsync();
            }
        }

        [Fact]
        async Task PluginWithException_ShouldCompleteAnyway()
        {
            var messageSender = new MessageSender(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName);
            try
            {
                var plugin = new ShouldCompleteAnywayExceptionPlugin();

                messageSender.UsePlugin(plugin);

                var sendMessage = new Message(Encoding.UTF8.GetBytes("Test message"));
                await messageSender.SendAsync(sendMessage);
            }
            finally
            {
                await messageSender.CloseAsync();
                var messageReceiver = new MessageReceiver(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName, ReceiveMode.ReceiveAndDelete);
                await messageReceiver.ReceiveAsync();
                await messageReceiver.CloseAsync();
            }
        }
    }

    internal class FirstSendPlugin : ServiceBusPlugin
    {
        public override Task<Message> BeforeMessageSend(Message message)
        {
            message.UserProperties.Add("FirstSendPlugin", true);
            return Task.FromResult(message);
        }
    }

    internal class SecondSendPlugin : ServiceBusPlugin
    {
        public override Task<Message> BeforeMessageSend(Message message)
        {
            // Ensure that the first plugin actually ran first
            Assert.True((bool)message.UserProperties["FirstSendPlugin"]);
            message.UserProperties.Add("SecondSendPlugin", true);
            return Task.FromResult(message);
        }
    }

    internal class SendReceivePlugin : ServiceBusPlugin
    {
        // Null the body on send, and replace it when received.
        Dictionary<string, byte[]> MessageBodies = new Dictionary<string, byte[]>();

        public override Task<Message> BeforeMessageSend(Message message)
        {
            MessageBodies.Add(message.MessageId, message.Body);
            message.Body = null;
            return Task.FromResult(message);
        }

        public override Task<Message> AfterMessageReceive(Message message)
        {
            Assert.Null(message.Body);
            message.Body = MessageBodies[message.MessageId];
            return Task.FromResult(message);
        }
    }

    internal class ExceptionPlugin : ServiceBusPlugin
    {
        public override Task<Message> BeforeMessageSend(Message message)
        {
            throw new NotImplementedException();
        }
    }

    internal class ShouldCompleteAnywayExceptionPlugin : ServiceBusPlugin
    {
        public override Task<Message> BeforeMessageSend(Message message)
        {
            throw new ServiceBusPluginException(true, new NotImplementedException());
        }
    }
}
