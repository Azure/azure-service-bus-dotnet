// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IReceiverClient : IClientEntity
    {
        string Path { get; }

        ReceiveMode ReceiveMode { get; }

        void OnMessageAsync(Func<Message, CancellationToken, Task> callback);

        void OnMessageAsync(Func<Message, CancellationToken, Task> callback, OnMessageOptions onMessageOptions);

        Task CompleteAsync(Guid lockToken);

        Task AbandonAsync(Guid lockToken);

        Task DeadLetterAsync(Guid lockToken);
    }
}