// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Threading.Tasks;

    public interface IExtensions
    {
        /// <summary>Register message ID generator to be used.</summary>
        IExtensions MessageIdGenerator(Func<Message, string> generator);

        /// <summary>Register incoming message mutator.</summary>
        IExtensions IncomingMessageMutator(Func<Message, Task<Message>> mutator);

        /// <summary>Register incoming message mutator.</summary>
        IExtensions OutgoingMessageMutator(Func<Message, Task<Message>> mutator);
    }
}