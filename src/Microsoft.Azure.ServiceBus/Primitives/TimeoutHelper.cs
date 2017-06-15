// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Azure.ServiceBus.Primitives
{
    [DebuggerStepThrough]
    internal struct TimeoutHelper
    {
        public static readonly TimeSpan MaxWait = TimeSpan.FromMilliseconds(int.MaxValue);
        DateTime deadline;
        bool deadlineSet;

        public TimeoutHelper(TimeSpan timeout)
            : this(timeout, false)
        {
        }

        public TimeoutHelper(TimeSpan timeout, bool startTimeout)
        {
            Fx.Assert(timeout >= TimeSpan.Zero, "timeout must be non-negative");

            OriginalTimeout = timeout;
            deadline = DateTime.MaxValue;
            deadlineSet = timeout == TimeSpan.MaxValue;

            if (startTimeout && !deadlineSet)
                SetDeadline();
        }

        public TimeSpan OriginalTimeout { get; }

        public static bool IsTooLarge(TimeSpan timeout)
        {
            return timeout > MaxWait && timeout != TimeSpan.MaxValue;
        }

        public static TimeSpan FromMilliseconds(int milliseconds)
        {
            if (milliseconds == Timeout.Infinite)
                return TimeSpan.MaxValue;

            return TimeSpan.FromMilliseconds(milliseconds);
        }

        public static int ToMilliseconds(TimeSpan timeout)
        {
            if (timeout == TimeSpan.MaxValue)
                return Timeout.Infinite;

            var ticks = Ticks.FromTimeSpan(timeout);
            if (ticks / TimeSpan.TicksPerMillisecond > int.MaxValue)
                return int.MaxValue;
            return Ticks.ToMilliseconds(ticks);
        }

        public static TimeSpan Min(TimeSpan val1, TimeSpan val2)
        {
            if (val1 > val2)
                return val2;

            return val1;
        }

        public static DateTime Min(DateTime val1, DateTime val2)
        {
            if (val1 > val2)
                return val2;

            return val1;
        }

        public static TimeSpan Add(TimeSpan timeout1, TimeSpan timeout2)
        {
            return Ticks.ToTimeSpan(Ticks.Add(Ticks.FromTimeSpan(timeout1), Ticks.FromTimeSpan(timeout2)));
        }

        public static DateTime Add(DateTime time, TimeSpan timeout)
        {
            if (timeout >= TimeSpan.Zero && DateTime.MaxValue - time <= timeout)
                return DateTime.MaxValue;
            if (timeout <= TimeSpan.Zero && DateTime.MinValue - time >= timeout)
                return DateTime.MinValue;
            return time + timeout;
        }

        public static DateTime Subtract(DateTime time, TimeSpan timeout)
        {
            return Add(time, TimeSpan.Zero - timeout);
        }

        public static TimeSpan Divide(TimeSpan timeout, int factor)
        {
            if (timeout == TimeSpan.MaxValue)
                return TimeSpan.MaxValue;

            return Ticks.ToTimeSpan(Ticks.FromTimeSpan(timeout) / factor + 1);
        }

        public static void ThrowIfNegativeArgument(TimeSpan timeout)
        {
            ThrowIfNegativeArgument(timeout, nameof(timeout));
        }

        public static void ThrowIfNegativeArgument(TimeSpan timeout, string argumentName)
        {
            if (timeout < TimeSpan.Zero)
                throw Fx.Exception.ArgumentOutOfRange(argumentName, timeout, Resources.TimeoutMustBeNonNegative.FormatForUser(argumentName, timeout));
        }

        public static void ThrowIfNonPositiveArgument(TimeSpan timeout)
        {
            ThrowIfNonPositiveArgument(timeout, nameof(timeout));
        }

        public static void ThrowIfNonPositiveArgument(TimeSpan timeout, string argumentName)
        {
            if (timeout <= TimeSpan.Zero)
                throw Fx.Exception.ArgumentOutOfRange(argumentName, timeout, Resources.TimeoutMustBePositive.FormatForUser(argumentName, timeout));
        }

        public static bool WaitOne(WaitHandle waitHandle, TimeSpan timeout)
        {
            ThrowIfNegativeArgument(timeout);
            if (timeout == TimeSpan.MaxValue)
            {
                waitHandle.WaitOne();
                return true;
            }

            return waitHandle.WaitOne(timeout);
        }

        public TimeSpan RemainingTime()
        {
            if (!deadlineSet)
            {
                SetDeadline();
                return OriginalTimeout;
            }

            if (deadline == DateTime.MaxValue)
                return TimeSpan.MaxValue;

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return TimeSpan.Zero;

            return remaining;
        }

        public TimeSpan ElapsedTime()
        {
            return OriginalTimeout - RemainingTime();
        }

        void SetDeadline()
        {
            Fx.Assert(!deadlineSet, "TimeoutHelper deadline set twice.");
            deadline = DateTime.UtcNow + OriginalTimeout;
            deadlineSet = true;
        }
    }
}