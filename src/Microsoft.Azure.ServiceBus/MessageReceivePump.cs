// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus
{
    internal sealed class MessageReceivePump
    {
        readonly SemaphoreSlim maxConcurrentCallsSemaphoreSlim;
        readonly IMessageReceiver messageReceiver;
        readonly Func<Message, CancellationToken, Task> onMessageCallback;
        readonly CancellationToken pumpCancellationToken;
        readonly MessageHandlerOptions registerHandlerOptions;

        public MessageReceivePump(
            IMessageReceiver messageReceiver,
            MessageHandlerOptions registerHandlerOptions,
            Func<Message, CancellationToken, Task> callback,
            CancellationToken pumpCancellationToken)
        {
            this.messageReceiver = messageReceiver ?? throw new ArgumentNullException(nameof(messageReceiver));
            this.registerHandlerOptions = registerHandlerOptions;
            onMessageCallback = callback;
            this.pumpCancellationToken = pumpCancellationToken;
            maxConcurrentCallsSemaphoreSlim = new SemaphoreSlim(this.registerHandlerOptions.MaxConcurrentCalls);
        }

        public async Task StartPumpAsync()
        {
            var initialMessage = await messageReceiver.ReceiveAsync().ConfigureAwait(false);
            if (initialMessage != null)
                MessagingEventSource.Log.MessageReceiverPumpInitialMessageReceived(messageReceiver.ClientId, initialMessage);

            TaskExtensionHelper.Schedule(() => MessagePumpTask(initialMessage));
        }

        bool ShouldRenewLock()
        {
            return
                messageReceiver.ReceiveMode == ReceiveMode.PeekLock &&
                registerHandlerOptions.AutoRenewLock;
        }

        async Task MessagePumpTask(Message initialMessage)
        {
            while (!pumpCancellationToken.IsCancellationRequested)
            {
                Message message = null;
                try
                {
                    await maxConcurrentCallsSemaphoreSlim.WaitAsync(pumpCancellationToken).ConfigureAwait(false);

                    if (initialMessage == null)
                    {
                        message = await messageReceiver.ReceiveAsync(registerHandlerOptions.ReceiveTimeOut).ConfigureAwait(false);
                    }
                    else
                    {
                        message = initialMessage;
                        initialMessage = null;
                    }

                    if (message != null)
                    {
                        MessagingEventSource.Log.MessageReceiverPumpTaskStart(messageReceiver.ClientId, message, maxConcurrentCallsSemaphoreSlim.CurrentCount);
                        TaskExtensionHelper.Schedule(() => MessageDispatchTask(message));
                    }
                }
                catch (Exception exception)
                {
                    MessagingEventSource.Log.MessageReceivePumpTaskException(messageReceiver.ClientId, string.Empty, exception);
                    registerHandlerOptions.RaiseExceptionReceived(new ExceptionReceivedEventArgs(exception, ExceptionReceivedEventArgsAction.Receive));
                }
                finally
                {
                    // Either an exception or for some reason message was null, release semaphore and retry.
                    if (message == null)
                    {
                        maxConcurrentCallsSemaphoreSlim.Release();
                        MessagingEventSource.Log.MessageReceiverPumpTaskStop(messageReceiver.ClientId, maxConcurrentCallsSemaphoreSlim.CurrentCount);
                    }
                }
            }
        }

        async Task MessageDispatchTask(Message message)
        {
            CancellationTokenSource renewLockCancellationTokenSource = null;
            Timer autoRenewLockCancellationTimer = null;

            MessagingEventSource.Log.MessageReceiverPumpDispatchTaskStart(messageReceiver.ClientId, message);

            if (ShouldRenewLock())
            {
                renewLockCancellationTokenSource = new CancellationTokenSource();
                TaskExtensionHelper.Schedule(() => RenewMessageLockTask(message, renewLockCancellationTokenSource.Token));

                // After a threshold time of renewal('AutoRenewTimeout'), create timer to cancel anymore renewals.
                autoRenewLockCancellationTimer = new Timer(CancelAutoRenewlock, renewLockCancellationTokenSource, registerHandlerOptions.MaxAutoRenewDuration, TimeSpan.FromMilliseconds(-1));
            }

            try
            {
                MessagingEventSource.Log.MessageReceiverPumpUserCallbackStart(messageReceiver.ClientId, message);
                await onMessageCallback(message, pumpCancellationToken).ConfigureAwait(false);
                MessagingEventSource.Log.MessageReceiverPumpUserCallbackStop(messageReceiver.ClientId, message);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageReceiverPumpUserCallbackException(messageReceiver.ClientId, message, exception);
                registerHandlerOptions.RaiseExceptionReceived(new ExceptionReceivedEventArgs(exception, ExceptionReceivedEventArgsAction.UserCallback));

                // Nothing much to do if UserCallback throws, Abandon message and Release semaphore.
                await AbandonMessageIfNeededAsync(message).ConfigureAwait(false);
                maxConcurrentCallsSemaphoreSlim.Release();
                return;
            }
            finally
            {
                renewLockCancellationTokenSource?.Cancel();
                renewLockCancellationTokenSource?.Dispose();
                autoRenewLockCancellationTimer?.Dispose();
            }

            // If we've made it this far, user callback completed fine. Complete message and Release semaphore.
            await CompleteMessageIfNeededAsync(message).ConfigureAwait(false);
            maxConcurrentCallsSemaphoreSlim.Release();

            MessagingEventSource.Log.MessageReceiverPumpDispatchTaskStop(messageReceiver.ClientId, message, maxConcurrentCallsSemaphoreSlim.CurrentCount);
        }

        void CancelAutoRenewlock(object state)
        {
            var renewLockCancellationTokenSource = (CancellationTokenSource) state;
            try
            {
                renewLockCancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Ignore this race.
            }
        }

        async Task AbandonMessageIfNeededAsync(Message message)
        {
            try
            {
                if (messageReceiver.ReceiveMode == ReceiveMode.PeekLock)
                    await messageReceiver.AbandonAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                registerHandlerOptions.RaiseExceptionReceived(new ExceptionReceivedEventArgs(exception, ExceptionReceivedEventArgsAction.Abandon));
            }
        }

        async Task CompleteMessageIfNeededAsync(Message message)
        {
            try
            {
                if (messageReceiver.ReceiveMode == ReceiveMode.PeekLock &&
                    registerHandlerOptions.AutoComplete)
                    await messageReceiver.CompleteAsync(new[] {message.SystemProperties.LockToken}).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                registerHandlerOptions.RaiseExceptionReceived(new ExceptionReceivedEventArgs(exception, ExceptionReceivedEventArgsAction.Complete));
            }
        }

        async Task RenewMessageLockTask(Message message, CancellationToken renewLockCancellationToken)
        {
            while (!pumpCancellationToken.IsCancellationRequested &&
                   !renewLockCancellationToken.IsCancellationRequested)
                try
                {
                    var amount = MessagingUtilities.CalculateRenewAfterDuration(message.SystemProperties.LockedUntilUtc);
                    MessagingEventSource.Log.MessageReceiverPumpRenewMessageStart(messageReceiver.ClientId, message, amount);
                    await Task.Delay(amount, renewLockCancellationToken).ConfigureAwait(false);

                    if (!pumpCancellationToken.IsCancellationRequested &&
                        !renewLockCancellationToken.IsCancellationRequested)
                    {
                        await messageReceiver.RenewLockAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
                        MessagingEventSource.Log.MessageReceiverPumpRenewMessageStop(messageReceiver.ClientId, message);
                    }
                    else
                    {
                        break;
                    }
                }
                catch (Exception exception)
                {
                    MessagingEventSource.Log.MessageReceiverPumpRenewMessageException(messageReceiver.ClientId, message, exception);

                    // TaskCancelled is expected here as renewTasks will be cancelled after the Complete call is made.
                    // Lets not bother user with this exception.
                    if (!(exception is TaskCanceledException))
                        registerHandlerOptions.RaiseExceptionReceived(new ExceptionReceivedEventArgs(exception, ExceptionReceivedEventArgsAction.RenewLock));

                    if (!MessagingUtilities.ShouldRetry(exception))
                        break;
                }
        }
    }
}