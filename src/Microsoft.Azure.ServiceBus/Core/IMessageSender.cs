// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IMessageSender : IClientEntity
    {
        Task SendAsync(BrokeredMessage brokeredMessage);

        Task SendAsync(IList<BrokeredMessage> brokeredMessages);

        Task<long> ScheduleMessageAsync(BrokeredMessage message, DateTimeOffset scheduleEnqueueTimeUtc);

        Task CancelScheduledMessageAsync(long sequenceNumber);
    }
}