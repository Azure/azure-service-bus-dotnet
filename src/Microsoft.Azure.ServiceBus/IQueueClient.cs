// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.ServiceBus.Core;

namespace Microsoft.Azure.ServiceBus
{
    /// <summary>
    ///     An interface used to describe the <see cref="QueueClient" />QueueClient.
    /// </summary>
    public interface IQueueClient : IReceiverClient, ISenderClient
    {
        /// <summary>
        ///     Gets the name of the queue.
        /// </summary>
        string QueueName { get; }
    }
}