// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class RetryPolicy
    {
        internal const string DefaultServerBusyException = "This request has been blocked because the entity or namespace is being throttled. Please retry the operation, and if condition continues, please slow down your rate of request.";
        internal static readonly TimeSpan ServerBusyBaseSleepTime = TimeSpan.FromSeconds(10);

        const int DefaultRetryMaxCount = 5;
        static readonly TimeSpan DefaultRetryMinBackoff = TimeSpan.Zero;
        static readonly TimeSpan DefaultRetryMaxBackoff = TimeSpan.FromSeconds(30);

        readonly object serverBusyLock = new object();
        Timer serverBusyResetTimer;

        // This is a volatile copy of IsServerBusy. IsServerBusy is synchronized with a lock, whereas encounteredServerBusy is kept volatile for performance reasons.
        volatile bool encounteredServerBusy;

        // Should this be changed to NoRetry?
        public static RetryPolicy Default => new RetryExponential(DefaultRetryMinBackoff, DefaultRetryMaxBackoff, DefaultRetryMaxCount);

        public bool IsServerBusy { get; protected set; }

        public string ServerBusyExceptionMessage { get; protected set; }

        public async Task RunOperation(Func<Task> operation, TimeSpan operationTimeout)
        {
            int currentRetryCount = 0;
            List<Exception> exceptions = null;
            TimeoutHelper timeoutHelper = new TimeoutHelper(operationTimeout);

            if (this.IsServerBusy && timeoutHelper.RemainingTime() < RetryPolicy.ServerBusyBaseSleepTime)
            {
                // We are in a server busy state before we start processing.
                // Since ServerBusyBaseSleepTime > remaining time for the operation, we don't wait for the entire Sleep time.
                await Task.Delay(timeoutHelper.RemainingTime());
                throw new ServerBusyException(this.ServerBusyExceptionMessage);
            }

            while (true)
            {
                if (this.IsServerBusy)
                {
                    await Task.Delay(RetryPolicy.ServerBusyBaseSleepTime);
                }

                try
                {
                    await operation();

                    // Its a successful operation. Preemptively reset ServerBusy status.
                    this.ResetServerBusy();
                    break;
                }
                catch (Exception exception)
                {
                    TimeSpan retryInterval;
                    currentRetryCount++;
                    if (exceptions == null)
                    {
                        exceptions = new List<Exception>();
                    }
                    exceptions.Add(exception);

                    if (this.ShouldRetry(
                        timeoutHelper.RemainingTime(), currentRetryCount, exception, out retryInterval)
                        && retryInterval < timeoutHelper.RemainingTime())
                    {
                        await Task.Delay(retryInterval);
                        continue;
                    }

                    if (exceptions.Count == 1)
                    {
                        throw;
                    }

                    // Wrap all the exceptions in an aggregate exception if there are more than one.
                    throw new AggregateException(exceptions);
                }
            }
        }

        public virtual bool IsRetryableException(Exception exception)
        {
            if (exception == null)
            {
                throw Fx.Exception.ArgumentNull("lastException");
            }

            ServiceBusException serviceBusException = exception as ServiceBusException;
            if (serviceBusException != null)
            {
                return serviceBusException.IsTransient;
            }

            return false;
        }

        public abstract RetryPolicy Clone();

        internal bool ShouldRetry(TimeSpan remainingTime, int currentRetryCount, Exception lastException, out TimeSpan retryInterval)
        {
            if (lastException is ServerBusyException)
            {
                this.SetServerBusy(lastException.Message);
            }

            if (lastException == null)
            {
                // there are no exceptions.
                retryInterval = TimeSpan.Zero;
                return false;
            }

            if (this.IsRetryableException(lastException))
            {
                return this.OnShouldRetry(remainingTime, currentRetryCount, out retryInterval);
            }

            retryInterval = TimeSpan.Zero;
            return false;
        }

        internal void SetServerBusy(string exceptionMessage)
        {
            // multiple call to this method will not prolong the timer.
            if (this.encounteredServerBusy)
            {
                return;
            }

            lock (this.serverBusyLock)
            {
                if (!this.encounteredServerBusy)
                {
                    this.encounteredServerBusy = true;
                    this.ServerBusyExceptionMessage = string.IsNullOrWhiteSpace(exceptionMessage) ?
                        DefaultServerBusyException
                        : exceptionMessage;
                    this.IsServerBusy = true;
                    this.serverBusyResetTimer = new Timer(OnTimerCallback, this, RetryPolicy.ServerBusyBaseSleepTime, TimeSpan.FromMilliseconds(-1));
                }
            }
        }

        internal void ResetServerBusy()
        {
            if (!this.encounteredServerBusy)
            {
                return;
            }

            lock (this.serverBusyLock)
            {
                if (this.encounteredServerBusy)
                {
                    this.encounteredServerBusy = false;
                    this.ServerBusyExceptionMessage = DefaultServerBusyException;
                    this.IsServerBusy = false;
                    this.serverBusyResetTimer.Dispose();
                    this.serverBusyResetTimer = null;
                }
            }
        }

        protected abstract bool OnShouldRetry(TimeSpan remainingTime, int currentRetryCount, out TimeSpan retryInterval);

        static void OnTimerCallback(object state)
        {
            var thisPtr = (RetryPolicy)state;
            thisPtr.ResetServerBusy();
        }
    }
}