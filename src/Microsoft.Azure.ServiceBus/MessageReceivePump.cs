// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus.Primitives;

    public sealed class MessageReceivePump
    {
        static TimeSpan RenewLockThresholdTime = TimeSpan.FromSeconds(30);
        readonly Func<BrokeredMessage, CancellationToken, Task> onMessageCallback;
        readonly OnMessageOptions onMessageOptions;
        readonly MessageReceiver messageReceiver;
        readonly CancellationToken pumpCancellationToken;
        readonly SemaphoreSlim maxConcurrentCallsSemaphoreSlim;

        public MessageReceivePump(MessageReceiver messageReceiver, OnMessageOptions onMessageOptions,
            Func<BrokeredMessage, CancellationToken, Task> callback, CancellationToken pumpCancellationToken)
        {
            if (messageReceiver == null)
            {
                throw new ArgumentNullException(nameof(messageReceiver));
            }

            this.messageReceiver = messageReceiver;
            this.onMessageOptions = onMessageOptions;
            this.onMessageCallback = callback;
            this.pumpCancellationToken = pumpCancellationToken;
            this.maxConcurrentCallsSemaphoreSlim = new SemaphoreSlim(1, this.onMessageOptions.MaxConcurrentCalls);
        }

        // Start Pump method
        // Pump will start of a ReceiveTask which will:
        //  1. Start of a loop for Receives.
        //  2. Before starting the loop for receives it checks for cancellation token to stop
        //  3. Call Receive. 
        //  4. If message Received.
        //      a. Start a Dispatch task for the receivedMessage which again takes in a CancellationToken
        //      b. If MaxConcurrent calls > 1, call DispatchTask for that message and continue receiving on this thread.
        //      c. Have a semaphore to control the number of tasks to limit the maxConcurrent receives.
        //      b. If semaphore is not available, then wait, else go back to receive and dispatch
        //      e. In the dispatch task call callback and also start a RenewLockTask with the same cancellationToken.
        //      f. After callback is executed, If AutoComplete is set to true, call complete on the message.
        //      g. After the callback is executed, the Dispatch task will call cancel on the RenewLock task to stop the renewal.
        //  
        //  When Receiver Close is called, it will call the Cancel on the cancellationToken
        //  Then the Close on the MessageReceivePump is called.
        public void Start()
        {
            Task.Factory.StartNew(async () => await this.MessageReceiveTask().ConfigureAwait(false));
        }

        async Task MessageReceiveTask()
        {
            while (!this.pumpCancellationToken.IsCancellationRequested)
            {
                try
                {
                    BrokeredMessage message = await this.messageReceiver.ReceiveAsync();
                    await this.MessageDispatchTask(message).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    //TODO: Handle specific exceptions  like ServerBust, Timeout, etc better for retry
                    this.onMessageOptions.RaiseExceptionReceived(new ExceptionReceivedEventArgs(e, "Receive"));
                    throw;
                }
            }
        }

        async Task MessageDispatchTask(BrokeredMessage message)
        {
            try
            {
                CancellationTokenSource renewLockCancellationTokenSource = new CancellationTokenSource();
                TaskExtensionHelper.FireAndForget(() => this.RenewMessageLockTask(message, renewLockCancellationTokenSource.Token));

                await this.onMessageCallback(message, this.pumpCancellationToken);
                renewLockCancellationTokenSource.Cancel();
                renewLockCancellationTokenSource.Dispose();

                if (this.onMessageOptions.AutoComplete && !this.pumpCancellationToken.IsCancellationRequested)
                {
                    await message.CompleteAsync().ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                //TODO: Handle specific exceptions  like ServerBust, Timeout, etc better for retry
                this.onMessageOptions.RaiseExceptionReceived(new ExceptionReceivedEventArgs(e, "Dispatch"));
                throw;
            }
        }

        async Task RenewMessageLockTask(BrokeredMessage message, CancellationToken renewLockCancellationToken)
        {
            while (!this.pumpCancellationToken.IsCancellationRequested && 
                   !renewLockCancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (message.LockedUntilUtc - DateTime.UtcNow < MessageReceivePump.RenewLockThresholdTime)
                    {
                        await message.RenewLockAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), renewLockCancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    //TODO: Handle specific exceptions  like ServerBust, Timeout, etc better for retry
                    this.onMessageOptions.RaiseExceptionReceived(new ExceptionReceivedEventArgs(e, "RenewLock"));
                    throw;
                }
            }
        }
    }
}
