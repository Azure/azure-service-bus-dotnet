// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;

    public class NoRetry : RetryPolicy
    {
        /// <summary>
        /// Creates a copy of this instance.
        /// </summary>
        /// <returns>The created copy of this instance.</returns>
        public override RetryPolicy Clone()
        {
            return new NoRetry();
        }

        protected override bool OnShouldRetry(
            TimeSpan remainingTime,
            int currentRetryCount,
            out TimeSpan retryInterval)
        {
            retryInterval = TimeSpan.Zero;
            return false;
        }
    }
}