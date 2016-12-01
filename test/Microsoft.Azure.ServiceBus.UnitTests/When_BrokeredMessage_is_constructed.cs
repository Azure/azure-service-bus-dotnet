namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System.Threading.Tasks;
    using Xunit;

    public class When_BrokeredMessage_is_constructed
    {
        [Fact]
        async Task Message_should_have_MessageId_set()
        {
            var message = new BrokeredMessage("message with id");
            Assert.True(message.MessageId != null);
        }
    }
}