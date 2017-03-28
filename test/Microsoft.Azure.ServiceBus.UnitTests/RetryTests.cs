// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using System.Security.Authentication;
    using Xunit;

    public class RetryTests
    {
        [Fact]
        void RetryPolicyDefaultShouldBeRetryExponential()
        {
            var retry = RetryPolicy.Default;
            Assert.True(retry is RetryExponential);
        }

        [Fact]
        void RetryExponentialShouldRetryTest()
        {
            // Tuple<ExceptionType, CurrentRetryCount, ShouldRetry>
            Tuple<Exception, int, bool>[] listOfExceptions =
            {
                // Retry-able exceptions
                new Tuple<Exception, int, bool>(new ServiceBusCommunicationException("A"), 0, true),
                new Tuple<Exception, int, bool>(new ServerBusyException("A"), 0, true),
                new Tuple<Exception, int, bool>(new ServerBusyException("A"), 5, true),

                // Non retry-able exceptions
                new Tuple<Exception, int, bool>(new ServerBusyException("A"), 6, false),
                new Tuple<Exception, int, bool>(new TimeoutException(), 0, false),
                new Tuple<Exception, int, bool>(new AuthenticationException(), 0, false),
                new Tuple<Exception, int, bool>(new ArgumentException(), 0, false),
                new Tuple<Exception, int, bool>(new FormatException(), 0, false),
                new Tuple<Exception, int, bool>(new InvalidOperationException(""), 0, false),
                new Tuple<Exception, int, bool>(new QuotaExceededException(""), 0, false),
                new Tuple<Exception, int, bool>(new MessagingEntityNotFoundException(""), 0, false),
                new Tuple<Exception, int, bool>(new MessageLockLostException(""), 0, false),
                new Tuple<Exception, int, bool>(new MessagingEntityDisabledException(""), 0, false),
                new Tuple<Exception, int, bool>(new ReceiverDisconnectedException(""), 0, false),
                new Tuple<Exception, int, bool>(new SessionLockLostException(""), 0, false)  
            };

            var retry = new RetryExponential(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(20), 5);
            var remainingTime = Constants.DefaultOperationTimeout;

            foreach (var exceptionTuple in listOfExceptions)
            {
                TimeSpan retryInterval;
                bool shouldRetry = retry.ShouldRetry(remainingTime, exceptionTuple.Item2, exceptionTuple.Item1, out retryInterval);
                Assert.True(shouldRetry == exceptionTuple.Item3);
            }
        }

        [Fact]
        void RetryExponentialRetryIntervalShouldIncreaseTest()
        {
            RetryExponential policy = (RetryExponential)RetryPolicy.Default.Clone();
            bool retry = true;
            int retryCount = 0;
            TimeSpan duration = Constants.DefaultOperationTimeout;
            TimeSpan lastRetryInterval = TimeSpan.Zero;
            ServiceBusException exception = new ServiceBusException(true, "");
            while (retry)
            {
                TimeSpan retryInterval;
                retry = policy.ShouldRetry(duration, retryCount, exception, out retryInterval);
                if (retry)
                {
                    Assert.True(retryInterval >= lastRetryInterval, $"Retry sleep should not decrease. Retry = [{retryInterval}]");
                    retryCount++;
                    lastRetryInterval = retryInterval;
                }
            }
        }

        [Fact]
        public void RetryExponentialEnsureRandomTest()
        {
            // We use a constant retryCount to just test random-ness. We are
            // not testing increasing interval.
            int retryCount = 1;
            RetryExponential policy1 = (RetryExponential)RetryPolicy.Default.Clone();
            RetryExponential policy2 = (RetryExponential)RetryPolicy.Default.Clone();
            ServiceBusException exception = new ServiceBusException(true, "");
            int retryMatchingInstances = 0;
            for (int i = 0; i < 10; i++)
            {
                TimeSpan retryInterval1;
                policy1.ShouldRetry(Constants.DefaultOperationTimeout, retryCount, exception, out retryInterval1);
                TimeSpan retryInterval2;
                policy2.ShouldRetry(Constants.DefaultOperationTimeout, retryCount, exception, out retryInterval2);
                if (retryInterval1 == retryInterval2)
                {
                    retryMatchingInstances++;
                }
            }

            Assert.True(retryMatchingInstances <= 3, "Out of 10 times we have 3 or more matching instances, which is alarming.");
        }

        [Fact]
        void RetryPolicyCloneShouldCloneServerBusy()
        {
            var policy1 = RetryPolicy.Default;
            Assert.False(policy1.IsServerBusy);

            policy1.SetServerBusy(RetryPolicy.DefaultServerBusyException);
            Assert.True(policy1.IsServerBusy);

            RetryExponential policy2 = (RetryExponential)policy1.Clone();
            Assert.True(policy2.IsServerBusy);

            policy1.ResetServerBusy();
            Assert.False(policy1.IsServerBusy);
            Assert.True(policy2.IsServerBusy);
        }

        [Fact]
        void RetryExponentialServerBusyShouldSelfResetTest()
        {
            RetryExponential policy1 = (RetryExponential)RetryPolicy.Default;
            int retryCount = 0;
            TimeSpan duration = Constants.DefaultOperationTimeout;
            ServerBusyException exception = new ServerBusyException("");
            TimeSpan retryInterval;

            // First ServerBusy exception
            Assert.False(policy1.IsServerBusy, "policy1.IsServerBusy should start with false");
            Assert.True(policy1.ShouldRetry(duration, retryCount, exception, out retryInterval), "We should retry, but it returned false");
            Assert.True(policy1.IsServerBusy, "policy1.IsServerBusy should be true");

            System.Threading.Thread.Sleep(3000);

            // Setting it a second time should not prolong the call.
            Assert.True(policy1.IsServerBusy, "policy1.IsServerBusy should be true");
            Assert.True(policy1.ShouldRetry(duration, retryCount, exception, out retryInterval), "We should retry, but it return false");
            Assert.True(policy1.IsServerBusy, "policy1.IsServerBusy should be true");

            System.Threading.Thread.Sleep(8000); // 3 + 8 = 11s
            Assert.False(policy1.IsServerBusy, "policy1.IsServerBusy should stay false after 11s");

            // Setting ServerBusy for second time.
            Assert.True(policy1.ShouldRetry(duration, retryCount, exception, out retryInterval), "We should retry, but it return false");
            Assert.True(policy1.IsServerBusy, "policy1.IsServerBusy is not true");
        }

        [Fact]
        async void RunOperationShouldReturnImmediatelyIfRetryIntervalIsGreaterThanOperationTimeout()
        {
            var policy = RetryPolicy.Default;
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                await policy.RunOperation(() => { throw new ServiceBusException(true, ""); }, TimeSpan.FromSeconds(3));
            }
            catch (Exception)
            {
                // Expected
            }
            Assert.True(watch.Elapsed.TotalSeconds < 3);
        }

        [Fact]
        async void RunOperationShouldWaitFor10SecondsForOperationIfServerBusy()
        {
            var policy = RetryPolicy.Default;
            policy.SetServerBusy(RetryPolicy.DefaultServerBusyException);
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                await policy.RunOperation(() => Task.CompletedTask, TimeSpan.FromMinutes(3));
            }
            catch (Exception)
            {
                // Expected
            }
            Assert.True(watch.Elapsed.TotalSeconds > 9);
            Assert.False(policy.IsServerBusy);
        }

        [Fact]
        async void RunOperationShouldWaitForAllOperationsToSucceed()
        {
            var policy = RetryPolicy.Default;
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                await policy.RunOperation(async () =>
                {
                    for (int i = 0; i < 5; i++)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }
                }, TimeSpan.FromMinutes(3));
            }
            catch (Exception)
            {
                // Expected
            }
            Assert.True(watch.Elapsed.TotalSeconds > 9);
            Assert.False(policy.IsServerBusy);
        }
    }
}
