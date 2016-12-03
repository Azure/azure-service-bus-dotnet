using Xunit;

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    public class When_BrokeredMessage_message_id_generator_is_not_specified
    {
        [Fact]
        public void Message_should_have_MessageId_set()
        {
            var message = new BrokeredMessage();

            Assert.Null(message.MessageId);
        }           
    }
}