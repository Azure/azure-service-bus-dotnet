﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Primitives;

    sealed class SessionReceivePump
    {
        readonly string clientId;
        readonly ISessionClient client;
        readonly Func<IMessageSession, Message, CancellationToken, Task> userOnSessionCallback;
        readonly SessionHandlerOptions sessionHandlerOptions;
        readonly string endpoint;
        readonly string entityPath;
        readonly CancellationToken pumpCancellationToken;
        readonly SemaphoreSlim maxConcurrentSessionsSemaphoreSlim;
        readonly SemaphoreSlim maxPendingAcceptSessionsSemaphoreSlim;

        public SessionReceivePump(string clientId,
            ISessionClient client,
            ReceiveMode receiveMode,
            SessionHandlerOptions sessionHandlerOptions,
            Func<IMessageSession, Message, CancellationToken, Task> callback,
            string endpoint,
            CancellationToken token)
        {
            this.client = client ?? throw new ArgumentException(nameof(client));
            this.clientId = clientId;
            ReceiveMode = receiveMode;
            this.sessionHandlerOptions = sessionHandlerOptions;
            userOnSessionCallback = callback;
            this.endpoint = endpoint;
            entityPath = client.EntityPath;
            pumpCancellationToken = token;
            maxConcurrentSessionsSemaphoreSlim = new SemaphoreSlim(this.sessionHandlerOptions.MaxConcurrentSessions);
            maxPendingAcceptSessionsSemaphoreSlim = new SemaphoreSlim(this.sessionHandlerOptions.MaxConcurrentAcceptSessionCalls);
        }

        ReceiveMode ReceiveMode { get; }

        public void StartPump()
        {
            // Schedule Tasks for doing PendingAcceptSession calls
            for (int i = 0; i < sessionHandlerOptions.MaxConcurrentAcceptSessionCalls; i++)
            {
                TaskExtensionHelper.Schedule(SessionPumpTaskAsync);
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

        bool ShouldRenewSessionLock()
        {
            return
                ReceiveMode == ReceiveMode.PeekLock &&
                sessionHandlerOptions.AutoRenewLock;
        }

        Task RaiseExceptionReceived(Exception e, string action)
        {
            var eventArgs = new ExceptionReceivedEventArgs(e, action, endpoint, entityPath, clientId);
            return sessionHandlerOptions.RaiseExceptionReceived(eventArgs);
        }

        async Task CompleteMessageIfNeededAsync(IMessageSession session, Message message)
        {
            try
            {
                if (ReceiveMode == ReceiveMode.PeekLock &&
                    sessionHandlerOptions.AutoComplete)
                {
                    await session.CompleteAsync(new[] { message.SystemProperties.LockToken }).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                await RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.Complete).ConfigureAwait(false);
            }
        }

        async Task AbandonMessageIfNeededAsync(IMessageSession session, Message message)
        {
            try
            {
                if (session.ReceiveMode == ReceiveMode.PeekLock)
                {
                    await session.AbandonAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                await RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.Abandon).ConfigureAwait(false);
            }
        }

        async Task SessionPumpTaskAsync()
        {
            while (!pumpCancellationToken.IsCancellationRequested)
            {
                bool concurrentSessionSemaphoreAquired = false;
                try
                {
                    await maxConcurrentSessionsSemaphoreSlim.WaitAsync(pumpCancellationToken).ConfigureAwait(false);
                    concurrentSessionSemaphoreAquired = true;

                    await maxPendingAcceptSessionsSemaphoreSlim.WaitAsync(pumpCancellationToken).ConfigureAwait(false);
                    IMessageSession session = await client.AcceptMessageSessionAsync().ConfigureAwait(false);
                    if (session == null)
                    {
                        await Task.Delay(Constants.NoMessageBackoffTimeSpan, pumpCancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    // `session` needs to be copied to another local variable before passing to Schedule
                    // because of the way variables are captured. (Refer 'Captured variables')
                    IMessageSession messageSession = session;
                    TaskExtensionHelper.Schedule(() => MessagePumpTaskAsync(messageSession));
                }
                catch (Exception exception)
                {
                    MessagingEventSource.Log.SessionReceivePumpSessionReceiveException(clientId, exception);

                    if (concurrentSessionSemaphoreAquired)
                    {
                        maxConcurrentSessionsSemaphoreSlim.Release();
                    }

                    if (exception is ServiceBusTimeoutException)
                    {
                        await Task.Delay(Constants.NoMessageBackoffTimeSpan, pumpCancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.AcceptMessageSession).ConfigureAwait(false);
                        if (!MessagingUtilities.ShouldRetry(exception))
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    maxPendingAcceptSessionsSemaphoreSlim.Release();
                }
            }
        }

        async Task MessagePumpTaskAsync(IMessageSession session)
        {
            if (session == null)
            {
                return;
            }

            CancellationTokenSource renewLockCancellationTokenSource = new CancellationTokenSource();
            if (ShouldRenewSessionLock())
            {
                TaskExtensionHelper.Schedule(() => RenewSessionLockTaskAsync(session, renewLockCancellationTokenSource.Token));
            }

            Timer userCallbackTimer = new Timer(
                OnUserCallBackTimeout,
                renewLockCancellationTokenSource,
                Timeout.Infinite,
                Timeout.Infinite);

            try
            {
                while (!pumpCancellationToken.IsCancellationRequested && !session.IsClosedOrClosing)
                {
                    Message message;
                    try
                    {
                        message = await session.ReceiveAsync(sessionHandlerOptions.MessageWaitTimeout).ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        MessagingEventSource.Log.MessageReceivePumpTaskException(clientId, session.SessionId, exception);
                        if (exception is ServiceBusTimeoutException)
                        {
                            // Timeout Exceptions are pretty common. Not alerting the User on this.
                            continue;
                        }

                        await RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.Receive).ConfigureAwait(false);
                        break;
                    }

                    if (message == null)
                    {
                        MessagingEventSource.Log.SessionReceivePumpSessionEmpty(clientId, session.SessionId);
                        break;
                    }

                    // Set the timer
                    userCallbackTimer.Change(sessionHandlerOptions.MaxAutoRenewDuration, TimeSpan.FromMilliseconds(-1));
                    bool callbackExceptionOccured = false;
                    try
                    {
                        await userOnSessionCallback(session, message, pumpCancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        MessagingEventSource.Log.MessageReceivePumpTaskException(clientId, session.SessionId, exception);
                        await RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.UserCallback).ConfigureAwait(false);
                        callbackExceptionOccured = true;
                        if (!(exception is MessageLockLostException || exception is SessionLockLostException))
                        {
                            await AbandonMessageIfNeededAsync(session, message).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        userCallbackTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    }

                    if (!callbackExceptionOccured)
                    {
                        await CompleteMessageIfNeededAsync(session, message).ConfigureAwait(false);
                    }
                    else if (session.IsClosedOrClosing)
                    {
                        // If User closed the session as part of the callback, break out of the loop
                        break;
                    }
                }
            }
            finally
            {
                userCallbackTimer.Dispose();
                CancelAndDisposeCancellationTokenSource(renewLockCancellationTokenSource);
                await CloseSessionIfNeededAsync(session).ConfigureAwait(false);
                maxConcurrentSessionsSemaphoreSlim.Release();
            }
        }

        async Task CloseSessionIfNeededAsync(IMessageSession session)
        {
            if (!session.IsClosedOrClosing)
            {
                try
                {
                    await session.CloseAsync().ConfigureAwait(false);
                    MessagingEventSource.Log.SessionReceivePumpSessionClosed(clientId, session.SessionId);
                }
                catch (Exception exception)
                {
                    MessagingEventSource.Log.SessionReceivePumpSessionCloseException(clientId, session.SessionId, exception);
                    await RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.CloseMessageSession).ConfigureAwait(false);
                }
            }
        }

        async Task RenewSessionLockTaskAsync(IMessageSession session, CancellationToken renewLockCancellationToken)
        {
            while (!pumpCancellationToken.IsCancellationRequested &&
                   !renewLockCancellationToken.IsCancellationRequested)
            {
                try
                {
                    TimeSpan amount = MessagingUtilities.CalculateRenewAfterDuration(session.LockedUntilUtc);

                    MessagingEventSource.Log.SessionReceivePumpSessionRenewLockStart(clientId, session.SessionId, amount);
                    await Task.Delay(amount, renewLockCancellationToken).ConfigureAwait(false);

                    if (!pumpCancellationToken.IsCancellationRequested &&
                        !renewLockCancellationToken.IsCancellationRequested)
                    {
                        await session.RenewSessionLockAsync().ConfigureAwait(false);
                        MessagingEventSource.Log.SessionReceivePumpSessionRenewLockStop(clientId, session.SessionId);
                    }
                    else
                    {
                        break;
                    }
                }
                catch (Exception exception)
                {
                    MessagingEventSource.Log.SessionReceivePumpSessionRenewLockException(clientId, session.SessionId, exception);

                    // TaskCancelled is expected here as renewTasks will be cancelled after the Complete call is made.
                    // Lets not bother user with this exception.
                    if (!(exception is TaskCanceledException))
                    {
                        await RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.RenewLock).ConfigureAwait(false);
                    }
                    if (!MessagingUtilities.ShouldRetry(exception))
                    {
                        break;
                    }
                }
            }
        }
    }
}