// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core;
    using Primitives;

    sealed class MessageBatchReceivePump
    {
        readonly Func<IList<Message>, CancellationToken, Task> onMessageBatchCallback;
        readonly string endpoint;
        readonly MessageBatchHandlerOptions registerHandlerOptions;
        readonly MessageReceiver messageReceiver;
        readonly CancellationToken pumpCancellationToken;
        readonly SemaphoreSlim maxConcurrentCallsSemaphoreSlim;
        readonly ServiceBusDiagnosticSource diagnosticSource;

        public MessageBatchReceivePump(MessageReceiver messageReceiver,
            MessageBatchHandlerOptions registerHandlerOptions,
            Func<IList<Message>, CancellationToken, Task> callback,
            Uri endpoint,
            CancellationToken pumpCancellationToken)
        {
            this.messageReceiver = messageReceiver ?? throw new ArgumentNullException(nameof(messageReceiver));
            this.registerHandlerOptions = registerHandlerOptions;
            this.onMessageBatchCallback = callback;
            this.endpoint = endpoint.Authority;
            this.pumpCancellationToken = pumpCancellationToken;
            this.maxConcurrentCallsSemaphoreSlim = new SemaphoreSlim(this.registerHandlerOptions.MaxConcurrentCalls);
            this.diagnosticSource = new ServiceBusDiagnosticSource(messageReceiver.Path, endpoint);
        }

        public void StartPump()
        {
            TaskExtensionHelper.Schedule(() => this.MessageBatchPumpTaskAsync());
        }

        bool ShouldRenewLock()
        {
            return
                this.messageReceiver.ReceiveMode == ReceiveMode.PeekLock &&
                this.registerHandlerOptions.AutoRenewLock;
        }

        Task RaiseExceptionReceived(Exception e, string action)
        {
            var eventArgs = new ExceptionReceivedEventArgs(e, action, this.endpoint, this.messageReceiver.Path, this.messageReceiver.ClientId);
            return this.registerHandlerOptions.RaiseExceptionReceived(eventArgs);
        }

        async Task MessageBatchPumpTaskAsync()
        {
            while (!this.pumpCancellationToken.IsCancellationRequested)
            {
                IList<Message> messages = null;
                try
                {
                    await this.maxConcurrentCallsSemaphoreSlim.WaitAsync(this.pumpCancellationToken).ConfigureAwait(false);
                    messages = await this.messageReceiver.ReceiveAsync(this.registerHandlerOptions.MaxMessageCount, this.registerHandlerOptions.ReceiveTimeOut).ConfigureAwait(false);

                    if (messages != null)
                    {
                        TaskExtensionHelper.Schedule(() =>
                        {
                            if (ServiceBusDiagnosticSource.IsEnabled())
                            {
                                return this.MessageBatchDispatchTaskInstrumented(messages);
                            }
                            else
                            {
                                return this.MessageBatchDispatchTask(messages);
                            }
                        });
                    }
                }
                catch (Exception exception)
                {
                    // Not reporting an ObjectDisposedException as we're stopping the pump
                    if (!(exception is ObjectDisposedException && this.pumpCancellationToken.IsCancellationRequested))
                    {
                        MessagingEventSource.Log.MessageReceivePumpTaskException(this.messageReceiver.ClientId, string.Empty, exception);
                        await this.RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.Receive).ConfigureAwait(false);
                    }
                }
                finally
                {
                    // Either an exception or for some reason message was null, release semaphore and retry.
                    if (messages == null)
                    {
                        this.maxConcurrentCallsSemaphoreSlim.Release();
                        MessagingEventSource.Log.MessageReceiverPumpTaskStop(this.messageReceiver.ClientId, this.maxConcurrentCallsSemaphoreSlim.CurrentCount);
                    }
                }
            }
        }

        async Task MessageBatchDispatchTaskInstrumented(IList<Message> messages)
        {
            IEnumerable<Activity> activities = this.diagnosticSource.ProcessStart(messages);
            Task processTask = null;
            try
            {
                processTask = MessageBatchDispatchTask(messages);
                await processTask.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                this.diagnosticSource.ReportException(e);
                throw;
            }
            finally
            {
                this.diagnosticSource.ProcessStop(activities, processTask?.Status);
            }
        }

        async Task MessageBatchDispatchTask(IList<Message> messages)
        {
            CancellationTokenSource renewLockCancellationTokenSource = null;
            Timer autoRenewLockCancellationTimer = null;

            MessagingEventSource.Log.MessageBatchReceiverPumpDispatchTaskStart(this.messageReceiver.ClientId, messages);

            if (this.ShouldRenewLock())
            {
                renewLockCancellationTokenSource = new CancellationTokenSource();
                TaskExtensionHelper.Schedule(() => this.RenewMessageLockTask(messages, renewLockCancellationTokenSource.Token));

                // After a threshold time of renewal('AutoRenewTimeout'), create timer to cancel anymore renewals.
                autoRenewLockCancellationTimer = new Timer(this.CancelAutoRenewLock, renewLockCancellationTokenSource, this.registerHandlerOptions.MaxAutoRenewDuration, TimeSpan.FromMilliseconds(-1));
            }

            try
            {
                MessagingEventSource.Log.MessageBatchReceiverPumpUserCallbackStart(this.messageReceiver.ClientId);
                await this.onMessageBatchCallback(messages, this.pumpCancellationToken).ConfigureAwait(false);

                MessagingEventSource.Log.MessageBatchReceiverPumpUserCallbackStop(this.messageReceiver.ClientId);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageBatchReceiverPumpUserCallbackException(this.messageReceiver.ClientId, exception);
                await this.RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.UserCallback).ConfigureAwait(false);

                // Nothing much to do if UserCallback throws, Abandon message and Release semaphore.
                if (!(exception is MessageLockLostException))
                {
                    await this.AbandonMessagesIfNeededAsync(messages).ConfigureAwait(false);
                }

                if (ServiceBusDiagnosticSource.IsEnabled())
                {
                    this.diagnosticSource.ReportException(exception);
                }
                // AbandonMessageIfNeededAsync should take care of not throwing exception
                this.maxConcurrentCallsSemaphoreSlim.Release();

                return;
            }
            finally
            {
                renewLockCancellationTokenSource?.Cancel();
                renewLockCancellationTokenSource?.Dispose();
                autoRenewLockCancellationTimer?.Dispose();
            }

            // If we've made it this far, user callback completed fine. Complete message and Release semaphore.
            await this.CompleteMessagesIfNeededAsync(messages).ConfigureAwait(false);
            this.maxConcurrentCallsSemaphoreSlim.Release();

            MessagingEventSource.Log.MessageBatchReceiverPumpDispatchTaskStop(this.messageReceiver.ClientId, this.maxConcurrentCallsSemaphoreSlim.CurrentCount);
        }

        void CancelAutoRenewLock(object state)
        {
            var renewLockCancellationTokenSource = (CancellationTokenSource)state;
            try
            {
                renewLockCancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Ignore this race.
            }
        }

        async Task AbandonMessagesIfNeededAsync(IList<Message> messages)
        {
            try
            {
                if (this.messageReceiver.ReceiveMode == ReceiveMode.PeekLock)
                {
                    var lockTokens = messages.Select(x => x.SystemProperties.LockToken).ToList();
                    await this.messageReceiver.AbandonAsync(lockTokens).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                await this.RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.Abandon).ConfigureAwait(false);
            }
        }

        async Task CompleteMessagesIfNeededAsync(IList<Message> messages)
        {
            try
            {
                if (this.messageReceiver.ReceiveMode == ReceiveMode.PeekLock &&
                    this.registerHandlerOptions.AutoComplete)
                {
                    var lockTokens = messages.Select(x => x.SystemProperties.LockToken);
                    await this.messageReceiver.CompleteAsync(lockTokens).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                await this.RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.Complete).ConfigureAwait(false);
            }
        }

        async Task RenewMessageLockTask(IList<Message> messages, CancellationToken renewLockCancellationToken)
        {
            while (!this.pumpCancellationToken.IsCancellationRequested &&
                   !renewLockCancellationToken.IsCancellationRequested)
            {
                try
                {
                    var amount = MessagingUtilities.CalculateRenewAfterDuration(messages.Last().SystemProperties.LockedUntilUtc);
                    MessagingEventSource.Log.MessageBatchReceiverPumpRenewMessageStart(this.messageReceiver.ClientId, amount);

                    // We're awaiting the task created by 'ContinueWith' to avoid awaiting the Delay task which may be canceled
                    // by the renewLockCancellationToken. This way we prevent a TaskCanceledException.
                    var delayTask = await Task.Delay(amount, renewLockCancellationToken)
                        .ContinueWith(t => t, TaskContinuationOptions.ExecuteSynchronously)
                        .ConfigureAwait(false);
                    if (delayTask.IsCanceled)
                    {
                        break;
                    }

                    if (!this.pumpCancellationToken.IsCancellationRequested &&
                        !renewLockCancellationToken.IsCancellationRequested)
                    {
                        await this.messageReceiver.RenewLocksAsync(messages).ConfigureAwait(false);
                        MessagingEventSource.Log.MessageBatchReceiverPumpRenewMessageStop(this.messageReceiver.ClientId);
                    }
                    else
                    {
                        break;
                    }
                }
                catch (Exception exception)
                {
                    MessagingEventSource.Log.MessageReceiverPumpRenewMessageException(this.messageReceiver.ClientId, exception);

                    // ObjectDisposedException should only happen here because the CancellationToken was disposed at which point
                    // this renew exception is not relevant anymore. Lets not bother user with this exception.
                    if (!(exception is ObjectDisposedException))
                    {
                        await this.RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.RenewLock).ConfigureAwait(false);
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