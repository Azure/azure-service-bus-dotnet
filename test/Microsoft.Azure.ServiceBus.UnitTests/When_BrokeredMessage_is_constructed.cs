namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using Xunit;

    public class When_BrokeredMessage_is_constructed
    {
        [Fact]
        void Message_should_have_MessageId_set()
        {
            var message = new BrokeredMessage("message with id");
            Assert.True(message.MessageId != null);
        }
    }
}