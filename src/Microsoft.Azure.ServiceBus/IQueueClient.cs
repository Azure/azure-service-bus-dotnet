// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.ServiceBus.Core;

namespace Microsoft.Azure.ServiceBus
{
    public interface IQueueClient : IReceiverClient, IMessageSender
    {
        string QueueName { get; }
    }
}