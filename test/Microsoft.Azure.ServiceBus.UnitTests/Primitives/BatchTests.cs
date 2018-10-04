// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.ServiceBus.UnitTests.Primitives
{
    using System;
    using System.Text;
    using Microsoft.Azure.ServiceBus.Core;
    using Xunit;

    public class BatchTests
    {
        private Func<Message, Task<Message>> fakePluginsCallback = Task.FromResult;

        [Fact]
        public async Task Should_return_false_when_is_about_to_exceed_max_batch_size()
        {
            using (var batch = new MessageBatch(1, fakePluginsCallback))
            {
                var wasAdded = await batch.TryAdd(new Message(Encoding.UTF8.GetBytes("hello")));
                Assert.False(wasAdded, "Message should not have been added, but it was.");
            }
        }

        [Fact]
        public void Should_throw_if_batch_disposed()
        {
            using (var batch = new MessageBatch(1, fakePluginsCallback))
            {
                batch.Dispose();
                Assert.ThrowsAsync<Exception>(() => batch.TryAdd(new Message()));
            }
        }

        [Fact]
        public void Should_throw_when_trying_to_add_an_already_received_message_to_batch()
        {
            using (var batch = new MessageBatch(100, fakePluginsCallback))
            {
                var message = new Message("test".GetBytes());
                message.SystemProperties.LockTokenGuid = Guid.NewGuid();

                Assert.ThrowsAsync<ArgumentException>(() => batch.TryAdd(message));
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public async Task Should_report_how_many_messages_are_in_batch(int numberOfMessages)
        {
            using (var batch = new MessageBatch(100, fakePluginsCallback))
            {
                for (var i = 0; i < numberOfMessages; i++)
                {
                    await batch.TryAdd(new Message());
                }

                Assert.Equal(numberOfMessages, batch.Length);
            }
        }

        [Fact]
        public async Task Should_reflect_property_in_batch_size()
        {
            using (var batch = new MessageBatch(100, fakePluginsCallback))
            {
                var message = new Message();

                await batch.TryAdd(message);

                Assert.Equal((ulong)24, batch.Size);
            }

            using (var batch = new MessageBatch(100, fakePluginsCallback))
            {
                var message = new Message();
                message.UserProperties["custom"] = "value";

                await batch.TryAdd(message);

                Assert.Equal((ulong)45, batch.Size);
            }
        }
    }
}