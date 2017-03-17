// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class Extensions : IExtensions
    {
        private Func<Message, string> messageIdGenerator = message => null;
        private List<Func<Message, Task<Message>>> incomingMutators = new List<Func<Message, Task<Message>>>();
        private List<Func<Message, Task<Message>>> outgoingMutators = new List<Func<Message, Task<Message>>>();

        public IExtensions MessageIdGenerator(Func<Message, string> generator)
        {
            this.messageIdGenerator = generator;

            return this;
        }

        public IExtensions IncomingMessageMutator(Func<Message, Task<Message>> mutator)
        {
            this.incomingMutators.Add(mutator);
            return this;
        }

        public IExtensions OutgoingMessageMutator(Func<Message, Task<Message>> mutator)
        {
            this.outgoingMutators.Add(mutator);
            return this;
        }
    }
}