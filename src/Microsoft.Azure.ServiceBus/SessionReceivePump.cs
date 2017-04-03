// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus.Amqp;
    using Microsoft.Azure.ServiceBus.Core;
    using Microsoft.Azure.ServiceBus.Primitives;

    sealed class SessionReceivePump
    {
        const int MaxInitialReceiveRetryCount = 3;
        readonly IInnerSenderReceiver client;
        readonly Func<IMessageSession, Message, CancellationToken, Task> userOnSessionCallback;
        readonly RegisterSessionHandlerOptions registerSessionHandlerOptions;
        readonly CancellationToken pumpCancellationToken;
        readonly SemaphoreSlim maxConcurrentSessionsSemaphoreSlim;
        readonly SemaphoreSlim maxPendingAcceptSessionsSemaphoreSlim;

        public SessionReceivePump(
            IInnerSenderReceiver client,
            RegisterSessionHandlerOptions registerSessionHandlerOptions,
            Func<IMessageSession, Message, CancellationToken, Task> callback,
            CancellationToken token)
        {
            if (this.client == null)
            {
                throw new ArgumentException(nameof(this.client));
            }

            this.client = client;
            this.registerSessionHandlerOptions = registerSessionHandlerOptions;
            this.userOnSessionCallback = callback;
            this.pumpCancellationToken = token;
            this.maxConcurrentSessionsSemaphoreSlim = new SemaphoreSlim(this.registerSessionHandlerOptions.MaxConcurrentSessions);
            this.maxPendingAcceptSessionsSemaphoreSlim = new SemaphoreSlim(this.registerSessionHandlerOptions.MaxPendingAcceptSessionCalls);
        }

        public async Task StartPumpAsync()
        {
            IMessageSession initialSession = null;
            int retryCount = 0;

            // Do a first receive of a Session on entity to flush any non-transient errors.
            // Timeout is a valid exception.
            while (true)
            {
                try
                {
                    initialSession = await this.client.AcceptMessageSessionAsync().ConfigureAwait(false);

                    // TODO: MessagingEventSource.Log.SessionReceiverPumpInitialSessionReceived(this.client.ClientId, session);
                    break;
                }
                catch (Exception exception)
                {
                    // TODO: Log the exception
                    if (exception is TimeoutException)
                    {
                        break;
                    }

                    retryCount++;
                    if (!MessagingUtilities.ShouldRetry(exception) || retryCount >= MaxInitialReceiveRetryCount)
                    {
                        throw;
                    }

                    TimeSpan backOffTime = MessagingUtilities.GetBackOffTime(exception);
                    await Task.Delay(backOffTime, this.pumpCancellationToken).ConfigureAwait(false);
                }
            }

            // Schedule Tasks for doing PendingAcceptSession calls
            for (int i = 0; i < this.registerSessionHandlerOptions.MaxPendingAcceptSessionCalls; i++)
            {
                if (i == 0)
                {
                    TaskExtensionHelper.Schedule(() => this.SessionPumpTaskAsync(initialSession));
                }
                else
                {
                    TaskExtensionHelper.Schedule(() => this.SessionPumpTaskAsync(null));
                }
            }
        }

        static void CancelAndDisposeCancellationTokenSource(CancellationTokenSource renewLockCancellationTokenSource)
        {
            renewLockCancellationTokenSource?.Cancel();
            renewLockCancellationTokenSource?.Dispose();
        }

        static void OnUserCallBackTimeout(object state)
        {
            CancellationTokenSource renewCancellationTokenSource = (CancellationTokenSource)state;
            renewCancellationTokenSource?.Cancel();
            renewCancellationTokenSource?.Dispose();
        }

        bool ShouldRenewLock()
        {
            return
                this.client.ReceiveMode == ReceiveMode.PeekLock &&
                this.registerSessionHandlerOptions.AutoRenewLock;
        }

        void RaiseExceptionRecieved(Exception e, string action)
        {
            var eventArgs = new ExceptionReceivedEventArgs(e, action);
            this.registerSessionHandlerOptions.RaiseExceptionReceived(eventArgs);
        }

        async Task CompleteMessageIfNeededAsync(IMessageSession session, Message message)
        {
            try
            {
                if (this.client.ReceiveMode == ReceiveMode.PeekLock &&
                    this.registerSessionHandlerOptions.AutoComplete)
                {
                    await session.CompleteAsync(new[] { message.LockToken }).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                this.registerSessionHandlerOptions.RaiseExceptionReceived(new ExceptionReceivedEventArgs(exception, "Complete"));
            }
        }

        async Task AbandonMessageIfNeededAsync(IMessageSession session, Message message)
        {
            try
            {
                if (session.ReceiveMode == ReceiveMode.PeekLock)
                {
                    await session.AbandonAsync(message.LockToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                this.registerSessionHandlerOptions.RaiseExceptionReceived(new ExceptionReceivedEventArgs(exception, "Abandon"));
            }
        }

        async Task SessionPumpTaskAsync(IMessageSession initialSession)
        {
            while (!this.pumpCancellationToken.IsCancellationRequested)
            {
                bool concurrentSessionSemaphoreAquired = false;
                try
                {
                    await this.maxConcurrentSessionsSemaphoreSlim.WaitAsync(this.pumpCancellationToken).ConfigureAwait(false);
                    concurrentSessionSemaphoreAquired = true;

                    if (initialSession != null)
                    {
                        TaskExtensionHelper.Schedule(() => this.MessagePumpTaskAsync(initialSession));
                        initialSession = null;
                    }
                    else
                    {
                        await this.maxPendingAcceptSessionsSemaphoreSlim.WaitAsync(this.pumpCancellationToken).ConfigureAwait(false);
                        IMessageSession session = await this.client.AcceptMessageSessionAsync().ConfigureAwait(false);
                        if (session == null)
                        {
                            continue;
                        }
                        TaskExtensionHelper.Schedule(() => this.MessagePumpTaskAsync(session));
                    }
                }
                catch (Exception exception)
                {
                    // TODO: Log the Session pump exception
                    if (concurrentSessionSemaphoreAquired)
                    {
                        this.maxConcurrentSessionsSemaphoreSlim.Release();
                    }

                    if (!MessagingUtilities.ShouldRetry(exception))
                    {
                        break;
                    }

                    if (!(exception is TimeoutException))
                    {
                        this.RaiseExceptionRecieved(exception, "AcceptMessageSession");
                    }

                    TimeSpan backOffTimeSpan = MessagingUtilities.GetBackOffTime(exception);
                    await Task.Delay(backOffTimeSpan, this.pumpCancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    this.maxPendingAcceptSessionsSemaphoreSlim.Release();
                }
            }
        }

        async Task MessagePumpTaskAsync(IMessageSession session)
        {
            CancellationTokenSource renewLockCancellationTokenSource = new CancellationTokenSource();
            if (this.ShouldRenewLock())
            {
                TaskExtensionHelper.Schedule(() => this.RenewSessionLockTaskAsync(session, renewLockCancellationTokenSource.Token));
            }

            while (!this.pumpCancellationToken.IsCancellationRequested &&
                   !session.IsClosedOrClosing)
            {
                Message message;
                try
                {
                    message = await session.ReceiveAsync(this.registerSessionHandlerOptions.MessageWaitTimeout).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    this.RaiseExceptionRecieved(exception, "Receive Message");
                    CancelAndDisposeCancellationTokenSource(renewLockCancellationTokenSource);
                    await session.CloseAsync().ConfigureAwait(false);
                    this.maxConcurrentSessionsSemaphoreSlim.Release();
                    break;
                }

                Timer userCallbackTimer = new Timer(
                    OnUserCallBackTimeout,
                    renewLockCancellationTokenSource,
                    this.registerSessionHandlerOptions.MaxAutoRenewTimeout,
                    TimeSpan.FromMilliseconds(-1));

                bool callbackExceptionOccured = false;
                try
                {
                    await this.userOnSessionCallback(session, message, this.pumpCancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    this.RaiseExceptionRecieved(exception, "User Callback Exception");
                    callbackExceptionOccured = true;
                    await this.AbandonMessageIfNeededAsync(session, message).ConfigureAwait(false);
                }
                finally
                {
                    userCallbackTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    userCallbackTimer.Dispose();
                }

                if (!callbackExceptionOccured)
                {
                    await this.CompleteMessageIfNeededAsync(session, message).ConfigureAwait(false);
                }
                else if (session.IsClosedOrClosing)
                {
                    // If User closed the session as part of the callback, break out of the loop
                    CancelAndDisposeCancellationTokenSource(renewLockCancellationTokenSource);
                    this.maxConcurrentSessionsSemaphoreSlim.Release();
                    break;
                }
            }
        }

        async Task RenewSessionLockTaskAsync(IMessageSession session, CancellationToken renewLockCancellationToken)
        {
            while (!this.pumpCancellationToken.IsCancellationRequested &&
                   !renewLockCancellationToken.IsCancellationRequested)
            {
                try
                {
                    TimeSpan amount = MessagingUtilities.CalculateRenewAfterDuration(session.LockedUntilUtc);

                    // TODO: log RenewSessionLock Start message
                    await Task.Delay(amount, renewLockCancellationToken).ConfigureAwait(false);

                    if (!this.pumpCancellationToken.IsCancellationRequested &&
                        !renewLockCancellationToken.IsCancellationRequested)
                    {
                        await session.RenewLockAsync(session.SessionId).ConfigureAwait(false);

                        // TODO: Log RenewSessionLock Completed message
                    }
                    else
                    {
                        break;
                    }
                }
                catch (Exception exception)
                {
                    // TODO: Log RenewSessionLock Exception message

                    // TaskCancelled is expected here as renewTasks will be cancelled after the Complete call is made.
                    // Lets not bother user with this exception.
                    if (!(exception is TaskCanceledException))
                    {
                        this.registerSessionHandlerOptions.RaiseExceptionReceived(new ExceptionReceivedEventArgs(exception, "RenewLock"));
                    }
                    if (!MessagingUtilities.ShouldRetry(exception))
                    {
                        break;
                    }

                    TimeSpan backoffTimeSpan = MessagingUtilities.GetBackOffTime(exception);
                    await Task.Delay(backoffTimeSpan, this.pumpCancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}