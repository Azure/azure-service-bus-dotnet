using System;
using Xunit;

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    public class When_BrokeredMessage_message_is_given_a_null_id_generator
    {
        [Fact]
        public void Should_throw_an_exception()
        {
            Assert.Throws<ArgumentNullException>(() => BrokeredMessage.SetMessageIdGenerator(null));
        }           
    }
}