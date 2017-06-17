// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus
{
    internal static class MessagingUtilities
    {
        public static TimeSpan CalculateRenewAfterDuration(DateTime lockedUntilUtc)
        {
            var remaining = lockedUntilUtc - DateTime.UtcNow;
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.FromTicks(Constants.MinimumLockDuration.Ticks / 2);
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