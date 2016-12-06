// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using Xunit;

    public class When_BrokeredMessage_message_is_given_a_null_id_generator
    {
        [Fact]
        public void Should_throw_an_exception()
        {
            Assert.Throws<ArgumentNullException>(() => BrokeredMessage.SetMessageIdGenerator(null));
        }
    }
}