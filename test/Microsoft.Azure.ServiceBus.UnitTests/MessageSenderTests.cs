namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using Core;
    using Xunit;

    public class MessageSenderTests
    {
        private MessageSender messageSender;

        public MessageSenderTests()
        {
            var builder = new ServiceBusConnectionStringBuilder("blah.com", "path", "key-name", "key-value");
            var connection = new ServiceBusConnection(builder);
            messageSender = new MessageSender(connection, "path", "via");
        }

        [Fact]
        [DisplayTestMethodName]
        public void Path_should_not_change()
        {
            Assert.Equal("path", messageSender.Path);
        }

        [Fact]
        [DisplayTestMethodName]
        public void TransferDestinationPath_should_not_change()
        {
            Assert.Equal("via", messageSender.TransferDestinationPath);
        }
    }
}