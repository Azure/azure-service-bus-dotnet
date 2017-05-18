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
        [DisplayTestMethodName]
        async Task Registering_plugin_multiple_times_should_throw()
        {
            var messageReceiver = new MessageReceiver(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName, ReceiveMode.ReceiveAndDelete);
            var firstPlugin = new FirstSendPlugin();
            var secondPlugin = new FirstSendPlugin();

            messageReceiver.RegisterPlugin(firstPlugin);
            Assert.Throws<ArgumentException>(() => messageReceiver.RegisterPlugin(secondPlugin));
            await messageReceiver.CloseAsync();
        }

        [Fact]
        [DisplayTestMethodName]
        async Task Unregistering_plugin_should_complete_with_plugin_set()
        {
            var messageReceiver = new MessageReceiver(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName, ReceiveMode.ReceiveAndDelete);
            var firstPlugin = new FirstSendPlugin();

            messageReceiver.RegisterPlugin(firstPlugin);
            messageReceiver.UnregisterPlugin(firstPlugin);
            await messageReceiver.CloseAsync();
        }

        [Fact]
        [DisplayTestMethodName]
        async Task Unregistering_plugin_should_complete_without_plugin_set()
        {
            var messageReceiver = new MessageReceiver(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName, ReceiveMode.ReceiveAndDelete);
            messageReceiver.UnregisterPlugin(typeof(ServiceBusPlugin));
            await messageReceiver.CloseAsync();
        }

        [Fact]
        [DisplayTestMethodName]
        async Task Multiple_plugins_should_run_in_order()
        {
            var messageSender = new MessageSender(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName);
            var messageReceiver = new MessageReceiver(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName, ReceiveMode.ReceiveAndDelete);

            try
            {
                var firstPlugin = new FirstSendPlugin();
                var secondPlugin = new SecondSendPlugin();

                messageSender.RegisterPlugin(firstPlugin);
                messageSender.RegisterPlugin(secondPlugin);

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
        [DisplayTestMethodName]
        async Task Multiple_plugins_should_be_able_to_manipulate_message()
        {
            var messageSender = new MessageSender(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName);
            var messageReceiver = new MessageReceiver(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName, ReceiveMode.ReceiveAndDelete);

            try
            {
                var sendReceivePlugin = new SendReceivePlugin();
                messageSender.RegisterPlugin(sendReceivePlugin);

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
        [DisplayTestMethodName]
        async Task Plugin_that_throws_generic_exception_should_throw()
        {
            var messageSender = new MessageSender(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName);
            try
            {
                var plugin = new ExceptionPlugin();
                
                messageSender.RegisterPlugin(plugin);

                var sendMessage = new Message(Encoding.UTF8.GetBytes("Test message"));
                await Assert.ThrowsAsync<NotImplementedException>(() => messageSender.SendAsync(sendMessage));
            }
            finally
            {
                await messageSender.CloseAsync();
            }
        }

        [Fact]
        [DisplayTestMethodName]
        async Task Plugin_that_throws_ServiceBusPluginException_with_ShouldContinue_should_continue()
        {
            var messageSender = new MessageSender(TestUtility.NamespaceConnectionString, TestConstants.NonPartitionedQueueName);
            try
            {
                var plugin = new ShouldCompleteAnywayExceptionPlugin();

                messageSender.RegisterPlugin(plugin);

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
