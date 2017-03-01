// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Threading.Tasks;

    public interface IMessageSessionEntity
    {
        Task<IMessageSession> AcceptMessageSessionAsync();

        Task<IMessageSession> AcceptMessageSessionAsync(TimeSpan serverWaitTime);

        Task<IMessageSession> AcceptMessageSessionAsync(string sessionId);

        Task<IMessageSession> AcceptMessageSessionAsync(string sessionId, TimeSpan serverWaitTime);
    }
}