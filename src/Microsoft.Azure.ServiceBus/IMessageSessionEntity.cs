// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Threading.Tasks;

    public interface IMessageSessionEntity
    {
        Task<MessageSession> AcceptMessageSessionAsync();

        Task<MessageSession> AcceptMessageSessionAsync(TimeSpan serverWaitTime);

        Task<MessageSession> AcceptMessageSessionAsync(string sessionId);

        Task<MessageSession> AcceptMessageSessionAsync(string sessionId, TimeSpan serverWaitTime);
    }
}