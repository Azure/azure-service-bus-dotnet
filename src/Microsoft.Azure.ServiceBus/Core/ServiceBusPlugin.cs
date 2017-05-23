// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Core
{
    using System.Threading.Tasks;

    public abstract class ServiceBusPlugin
    {
        public abstract string Name { get; }

        public virtual bool ShouldContinueOnException => false;

        public virtual Task<Message> BeforeMessageSend(Message message)
        {
            return Task.FromResult(message);
        }

        public virtual Task<Message> AfterMessageReceive(Message message)
        {
            return Task.FromResult(message);
        }
    }
}