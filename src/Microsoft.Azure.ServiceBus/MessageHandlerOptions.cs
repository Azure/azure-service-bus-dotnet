// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus
{
    /// <summary>
    ///     Provides options associated with message pump processing using
    ///     <see
    ///         cref="QueueClient.RegisterMessageHandler(System.Func{Microsoft.Azure.ServiceBus.Message,System.Threading.CancellationToken,System.Threading.Tasks.Task},MessageHandlerOptions)" />
    ///     and
    ///     <see
    ///         cref="SubscriptionClient.RegisterMessageHandler(System.Func{Microsoft.Azure.ServiceBus.Message,System.Threading.CancellationToken,System.Threading.Tasks.Task},MessageHandlerOptions)" />
    ///     .
    /// </summary>
    public sealed class MessageHandlerOptions
    {
        TimeSpan maxAutoRenewDuration;
        int maxConcurrentCalls;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MessageHandlerOptions" /> class.
        ///     Default Values:
        ///     <see cref="MaxConcurrentCalls" /> = 1
        ///     <see cref="AutoComplete" /> = true
        ///     <see cref="ReceiveTimeOut" /> = 1 minute
        ///     <see cref="MaxAutoRenewDuration" /> = 5 minutes
        /// </summary>
        public MessageHandlerOptions()
        {
            MaxConcurrentCalls = 1;
            AutoComplete = true;
            ReceiveTimeOut = Constants.DefaultOperationTimeout;
            MaxAutoRenewDuration = Constants.ClientPumpRenewLockTimeout;
        }

        /// <summary>Gets or sets the maximum number of concurrent calls to the callback the message pump should initiate.</summary>
        /// <value>The maximum number of concurrent calls to the callback.</value>
        public int MaxConcurrentCalls
        {
            get => maxConcurrentCalls;

            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(Resources.MaxConcurrentCallsMustBeGreaterThanZero.FormatForUser(value));
                }

                maxConcurrentCalls = value;
            }
        }

        /// <summary>
        ///     Gets or sets a value that indicates whether the message-pump should call
        ///     <see cref="QueueClient.CompleteAsync(string)" /> or
        ///     <see cref="SubscriptionClient.CompleteAsync(string)" /> on messages after the callback has completed processing.
        /// </summary>
        /// <value>
        ///     true to complete the message processing automatically on successful execution of the operation; otherwise,
        ///     false.
        /// </value>
        public bool AutoComplete { get; set; }

        /// <summary>
        ///     Gets or sets the maximum duration within which the lock will be renewed automatically. This
        ///     value should be greater than the longest message lock duration; for example, the LockDuration Property.
        /// </summary>
        /// <value>The maximum duration during which locks are automatically renewed.</value>
        public TimeSpan MaxAutoRenewDuration
        {
            get => maxAutoRenewDuration;

            set
            {
                TimeoutHelper.ThrowIfNegativeArgument(value, nameof(value));
                maxAutoRenewDuration = value;
            }
        }

        internal bool AutoRenewLock => MaxAutoRenewDuration > TimeSpan.Zero;

        internal ClientEntity MessageClientEntity { get; set; }

        internal TimeSpan ReceiveTimeOut { get; set; }

        /// <summary>
        ///     Occurs when an exception is received. Enables you to be notified of any errors encountered by the message pump.
        ///     When errors are received calls will automatically be retried, so this is informational.
        /// </summary>
        public event EventHandler<ExceptionReceivedEventArgs> ExceptionReceived;

        internal void RaiseExceptionReceived(ExceptionReceivedEventArgs e)
        {
            ExceptionReceived?.Invoke(MessageClientEntity, e);
        }
    }
}