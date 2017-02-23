// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.ServiceBus.Core
{
    public interface IReceiverClient : IClientEntity
    {
        string Path { get; }

        ReceiveMode ReceiveMode { get; }

        Task CompleteAsync(Guid lockToken);

        Task AbandonAsync(Guid lockToken);

        Task DeferAsync(Guid lockToken);

        Task DeadLetterAsync(Guid lockToken);

    }
}
