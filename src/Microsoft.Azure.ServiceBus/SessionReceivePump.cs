// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus
{
    internal sealed class SessionReceivePump
    {
        readonly IMessageSessionEntity client;
        readonly string clientId;
        readonly SemaphoreSlim maxConcurrentSessionsSemaphoreSlim;
        readonly SemaphoreSlim maxPendingAcceptSessionsSemaphoreSlim;
        readonly CancellationToken pumpCancellationToken;
        readonly SessionHandlerOptions sessionHandlerOptions;
        readonly Func<IMessageSession, Message, CancellationToken, Task> userOnSessionCallback;

        public SessionReceivePump(
            string clientId,
            IMessageSessionEntity client,
            ReceiveMode receiveMode,
            SessionHandlerOptions sessionHandlerOptions,
            Func<IMessageSession, Message, CancellationToken, Task> callback,
            CancellationToken token)
        {
            if (client == null)
                throw new ArgumentException(nameof(client));

            this.client = client;
            this.clientId = clientId;
            ReceiveMode = receiveMode;
            this.sessionHandlerOptions = sessionHandlerOptions;
            userOnSessionCallback = callback;
            pumpCancellationToken = token;
            maxConcurrentSessionsSemaphoreSlim = new SemaphoreSlim(this.sessionHandlerOptions.MaxConcurrentSessions);
            maxPendingAcceptSessionsSemaphoreSlim = new SemaphoreSlim(this.sessionHandlerOptions.MaxConcurrentAcceptSessionCalls);
        }

        ReceiveMode ReceiveMode { get; }

        public async Task StartPumpAsync()
        {
            IMessageSession initialSession = null;

            // Do a first receive of a Session on entity to flush any non-transient errors.
            // Timeout is a valid exception.
            try
            {
                initialSession = await client.AcceptMessageSessionAsync().ConfigureAwait(false);

                // MessagingEventSource.Log.SessionReceiverPumpInitialSessionReceived(this.client.ClientId, initialSession);
            }
            catch (TimeoutException)
            {
            }

            // Schedule Tasks for doing PendingAcceptSession calls
            for (var i = 0; i < sessionHandlerOptions.MaxConcurrentAcceptSessionCalls; i++)
                if (i == 0)
                    TaskExtensionHelper.Schedule(() => SessionPumpTaskAsync(initialSession));
                else
                    TaskExtensionHelper.Schedule(() => SessionPumpTaskAsync(null));
        }

        static void CancelAndDisposeCancellationTokenSource(CancellationTokenSource renewLockCancellationTokenSource)
        {
            renewLockCancellationTokenSource?.Cancel();
            renewLockCancellationTokenSource?.Dispose();
        }

        static void OnUserCallBackTimeout(object state)
        {
            var renewCancellationTokenSource = (CancellationTokenSource) state;
            renewCancellationTokenSource?.Cancel();
            renewCancellationTokenSource?.Dispose();
        }

        bool ShouldRenewSessionLock()
        {
            return
                ReceiveMode == ReceiveMode.PeekLock &&
                sessionHandlerOptions.AutoRenewLock;
        }

        void RaiseExceptionRecieved(Exception e, string action)
        {
            var eventArgs = new ExceptionReceivedEventArgs(e, action);
            sessionHandlerOptions.RaiseExceptionReceived(eventArgs);
        }

        async Task CompleteMessageIfNeededAsync(IMessageSession session, Message message)
        {
            try
            {
                if (ReceiveMode == ReceiveMode.PeekLock &&
                    sessionHandlerOptions.AutoComplete)
                    await session.CompleteAsync(new[] {message.SystemProperties.LockToken}).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                sessionHandlerOptions.RaiseExceptionReceived(new ExceptionReceivedEventArgs(exception, ExceptionReceivedEventArgsAction.Complete));
            }
        }

        async Task AbandonMessageIfNeededAsync(IMessageSession session, Message message)
        {
            try
            {
                if (session.ReceiveMode == ReceiveMode.PeekLock)
                    await session.AbandonAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                sessionHandlerOptions.RaiseExceptionReceived(new ExceptionReceivedEventArgs(exception, ExceptionReceivedEventArgsAction.Abandon));
            }
        }

        async Task SessionPumpTaskAsync(IMessageSession initialSession)
        {
            IMessageSession session;
            while (!pumpCancellationToken.IsCancellationRequested)
            {
                var concurrentSessionSemaphoreAquired = false;
                try
                {
                    await maxConcurrentSessionsSemaphoreSlim.WaitAsync(pumpCancellationToken).ConfigureAwait(false);
                    concurrentSessionSemaphoreAquired = true;

                    if (initialSession != null)
                    {
                        session = initialSession;
                        TaskExtensionHelper.Schedule(() => MessagePumpTaskAsync(session));
                        initialSession = null;
                    }
                    else
                    {
                        await maxPendingAcceptSessionsSemaphoreSlim.WaitAsync(pumpCancellationToken).ConfigureAwait(false);
                        session = await client.AcceptMessageSessionAsync().ConfigureAwait(false);
                        if (session == null)
                        {
                            await Task.Delay(Constants.NoMessageBackoffTimeSpan, pumpCancellationToken).ConfigureAwait(false);
                            continue;
                        }
                        TaskExtensionHelper.Schedule(() => MessagePumpTaskAsync(session));
                    }
                }
                catch (Exception exception)
                {
                    MessagingEventSource.Log.SessionReceivePumpSessionReceiveException(clientId, exception);

                    if (concurrentSessionSemaphoreAquired)
                        maxConcurrentSessionsSemaphoreSlim.Release();

                    if (!(exception is TimeoutException))
                    {
                        RaiseExceptionRecieved(exception, ExceptionReceivedEventArgsAction.AcceptMessageSession);
                        if (!MessagingUtilities.ShouldRetry(exception))
                            break;
                    }
                    else
                    {
                        await Task.Delay(Constants.NoMessageBackoffTimeSpan, pumpCancellationToken).ConfigureAwait(false);
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
                return;

            var renewLockCancellationTokenSource = new CancellationTokenSource();
            if (ShouldRenewSessionLock())
                TaskExtensionHelper.Schedule(() => RenewSessionLockTaskAsync(session, renewLockCancellationTokenSource.Token));

            var userCallbackTimer = new Timer(
                OnUserCallBackTimeout,
                renewLockCancellationTokenSource,
                Timeout.Infinite,
                Timeout.Infinite);

            try
            {
                while (!pumpCancellationToken.IsCancellationRequested &&
                       !session.IsClosedOrClosing)
                {
                    Message message;
                    try
                    {
                        message = await session.ReceiveAsync(sessionHandlerOptions.MessageWaitTimeout).ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        MessagingEventSource.Log.MessageReceivePumpTaskException(clientId, session.SessionId, exception);
                        if (exception is TimeoutException)
                            continue;

                        RaiseExceptionRecieved(exception, ExceptionReceivedEventArgsAction.Receive);
                        break;
                    }

                    if (message == null)
                    {
                        MessagingEventSource.Log.SessionReceivePumpSessionEmpty(clientId, session.SessionId);
                        break;
                    }

                    // Set the timer
                    userCallbackTimer.Change(sessionHandlerOptions.MaxAutoRenewDuration, TimeSpan.FromMilliseconds(-1));
                    var callbackExceptionOccured = false;
                    try
                    {
                        await userOnSessionCallback(session, message, pumpCancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        MessagingEventSource.Log.MessageReceivePumpTaskException(clientId, session.SessionId, exception);
                        RaiseExceptionRecieved(exception, ExceptionReceivedEventArgsAction.UserCallback);
                        callbackExceptionOccured = true;
                        await AbandonMessageIfNeededAsync(session, message).ConfigureAwait(false);
                    }
                    finally
                    {
                        userCallbackTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    }

                    if (!callbackExceptionOccured)
                        await CompleteMessageIfNeededAsync(session, message).ConfigureAwait(false);
                    else if (session.IsClosedOrClosing)
                        break;
                }
            }
            finally
            {
                userCallbackTimer.Dispose();
                await CloseSessionIfNeededAsync(session).ConfigureAwait(false);
                CancelAndDisposeCancellationTokenSource(renewLockCancellationTokenSource);
                maxConcurrentSessionsSemaphoreSlim.Release();
            }
        }

        async Task CloseSessionIfNeededAsync(IMessageSession session)
        {
            if (!session.IsClosedOrClosing)
                try
                {
                    await session.CloseAsync().ConfigureAwait(false);
                    MessagingEventSource.Log.SessionReceivePumpSessionClosed(clientId, session.SessionId);
                }
                catch (Exception exception)
                {
                    MessagingEventSource.Log.SessionReceivePumpSessionCloseException(clientId, session.SessionId, exception);
                    RaiseExceptionRecieved(exception, ExceptionReceivedEventArgsAction.CloseMessageSession);
                }
        }

        async Task RenewSessionLockTaskAsync(IMessageSession session, CancellationToken renewLockCancellationToken)
        {
            while (!pumpCancellationToken.IsCancellationRequested &&
                   !renewLockCancellationToken.IsCancellationRequested)
                try
                {
                    var amount = MessagingUtilities.CalculateRenewAfterDuration(session.LockedUntilUtc);

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
                    MessagingEventSource.Log.SessionReceivePumpSessionRenewLockExeption(clientId, session.SessionId, exception);

                    // TaskCancelled is expected here as renewTasks will be cancelled after the Complete call is made.
                    // Lets not bother user with this exception.
                    if (!(exception is TaskCanceledException))
                        sessionHandlerOptions.RaiseExceptionReceived(new ExceptionReceivedEventArgs(exception, ExceptionReceivedEventArgsAction.RenewLock));
                    if (!MessagingUtilities.ShouldRetry(exception))
                        break;
                }
        }
    }
}