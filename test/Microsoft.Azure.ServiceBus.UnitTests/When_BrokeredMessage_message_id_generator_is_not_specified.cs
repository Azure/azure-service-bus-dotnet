// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using Xunit;

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
