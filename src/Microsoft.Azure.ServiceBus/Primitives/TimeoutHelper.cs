// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Primitives
{
    using System;
    using System.Diagnostics;

    [DebuggerStepThrough]
    struct TimeoutHelper
    {
        DateTime deadline;
        bool deadlineSet;
        TimeSpan originalTimeout;

        public TimeoutHelper(TimeSpan timeout)
            : this(timeout, false)
        {
        }

        public TimeoutHelper(TimeSpan timeout, bool startTimeout)
        {
            Fx.Assert(timeout >= TimeSpan.Zero, "timeout must be non-negative");

            originalTimeout = timeout;
            deadline = DateTime.MaxValue;
            deadlineSet = (timeout == TimeSpan.MaxValue);

            if (startTimeout && !deadlineSet)
            {
                SetDeadline();
            }
        }

        public static void ThrowIfNegativeArgument(TimeSpan timeout)
        {
            ThrowIfNegativeArgument(timeout, nameof(timeout));
        }

        public static void ThrowIfNegativeArgument(TimeSpan timeout, string argumentName)
        {
            if (timeout < TimeSpan.Zero)
            {
                throw Fx.Exception.ArgumentOutOfRange(argumentName, timeout, Resources.TimeoutMustBeNonNegative.FormatForUser(argumentName, timeout));
            }
        }

        public static void ThrowIfNonPositiveArgument(TimeSpan timeout)
        {
            ThrowIfNonPositiveArgument(timeout, nameof(timeout));
        }

        public static void ThrowIfNonPositiveArgument(TimeSpan timeout, string argumentName)
        {
            if (timeout <= TimeSpan.Zero)
            {
                throw Fx.Exception.ArgumentOutOfRange(argumentName, timeout, Resources.TimeoutMustBePositive.FormatForUser(argumentName, timeout));
            }
        }

        public TimeSpan RemainingTime()
        {
            if (!deadlineSet)
            {
                SetDeadline();
                return originalTimeout;
            }

            if (deadline == DateTime.MaxValue)
            {
                return TimeSpan.MaxValue;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            return remaining;
        }

        void SetDeadline()
        {
            Fx.Assert(!deadlineSet, "TimeoutHelper deadline set twice.");
            deadline = DateTime.UtcNow + originalTimeout;
            deadlineSet = true;
        }
    }
}