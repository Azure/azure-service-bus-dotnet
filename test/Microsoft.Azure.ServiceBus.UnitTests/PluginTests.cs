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
            var firstPlugin = new FirstSendPlugin();
            var secondPlugin = new SecondSendPlugin();

            var messageSender = new MessageSender(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName);

            messageSender.UsePlugin(firstPlugin);
            messageSender.UsePlugin(secondPlugin);

            var messageReceiver = new MessageReceiver(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName, ReceiveMode.ReceiveAndDelete);

            var sendMessage = new Message(Encoding.UTF8.GetBytes("Test message"));
            await messageSender.SendAsync(sendMessage);

            var receivedMessage = await messageReceiver.ReceiveAsync(1, TimeSpan.FromMinutes(1));
            var firstSendPluginUserProperty = receivedMessage.First().UserProperties["FirstSendPlugin"];
            var secondSendPluginUserProperty = receivedMessage.First().UserProperties["SecondSendPlugin"];

            Assert.Equal(true, firstSendPluginUserProperty);
            Assert.Equal(true, secondSendPluginUserProperty);
        }

        [Fact]
        async Task SendReceivePlugin()
        {
            var sendReceivePlugin = new SendReceivePlugin();
            var messageSender = new MessageSender(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName);
            messageSender.UsePlugin(sendReceivePlugin);

            var messageReceiver = new MessageReceiver(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName, ReceiveMode.ReceiveAndDelete);

            var sendMessage = new Message(Encoding.UTF8.GetBytes("Test message"))
            {
                MessageId = Guid.NewGuid().ToString()
            };
            await messageSender.SendAsync(sendMessage);

            var receivedMessage = await messageReceiver.ReceiveAsync(1, TimeSpan.FromMinutes(1));

            Assert.Equal(sendMessage.Body, receivedMessage.First().Body);
        }

        [Fact]
        void PluginWithException()
        {
            var plugin = new ExceptionPlugin();
            var messageSender = new MessageSender(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName);
            messageSender.UsePlugin(plugin);

            var sendMessage = new Message(Encoding.UTF8.GetBytes("Test message"));
            Assert.ThrowsAsync<NotImplementedException>(() => messageSender.SendAsync(sendMessage));
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
            Assert.Equal(true, message.UserProperties["FirstSendPlugin"]);
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
}
