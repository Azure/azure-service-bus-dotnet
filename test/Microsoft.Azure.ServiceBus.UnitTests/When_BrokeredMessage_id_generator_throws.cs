using System;
using Xunit;

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    public class When_BrokeredMessage_id_generator_throws
    {
        [Fact]
        public void Should_throw_with_original_exception_included()
        {
            var exceptionToThrow = new Exception("boom!");
            Func<string> idGenerator = () =>
            {
                throw exceptionToThrow;
            };
            BrokeredMessage.SetMessageIdGenerator(idGenerator);

            var exception = Assert.Throws<InvalidOperationException>(() => new BrokeredMessage());
            Assert.Equal(exceptionToThrow, exception.InnerException);
        }           
    }
}