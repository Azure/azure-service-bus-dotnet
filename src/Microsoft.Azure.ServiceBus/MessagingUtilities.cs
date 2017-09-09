﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;

    static class MessagingUtilities
    {
        public static TimeSpan CalculateRenewAfterDuration(DateTime lockedUntilUtc)
        {
            var remaining = lockedUntilUtc - DateTime.UtcNow;

            if(remaining < TimeSpan.FromMilliseconds(400))
            {
                return TimeSpan.Zero;
            }

            var buffer = TimeSpan.FromTicks(Math.Min(remaining.Ticks / 2, Constants.MaximumRenewBufferDuration.Ticks));
            var renewAfter = remaining - buffer;

            return renewAfter;
        }

        public static bool ShouldRetry(Exception exception)
        {
            var serviceBusException = exception as ServiceBusException;
            return serviceBusException?.IsTransient == true;
        }
    }
}