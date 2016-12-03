using System;
using Xunit;

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    public class When_BrokeredMessage_message_id_generator_is_specified
    {
        [Fact]
        public void Message_should_have_MessageId_set()
        {
            var seed = 1;
            BrokeredMessage.SetMessageIdGenerator(() => $"id{seed++}");

            var message1 = new BrokeredMessage();
            var message2 = new BrokeredMessage();

            Assert.Equal("id1", message1.MessageId);
            Assert.Equal("id2", message2.MessageId);
        }           
    }
}