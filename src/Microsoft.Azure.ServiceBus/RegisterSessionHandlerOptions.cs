// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using Microsoft.Azure.ServiceBus.Primitives;

    /// <summary>Provides options associated with session pump processing using
    /// <see cref="QueueClient.RegisterSessionHandler(System.Func{MessageSession, Message,System.Threading.CancellationToken,System.Threading.Tasks.Task},RegisterSessionHandlerOptions)" /> and
    /// <see cref="SubscriptionClient.RegisterSessionHandler(System.Func{MessageSession, Message,System.Threading.CancellationToken,System.Threading.Tasks.Task},RegisterSessionHandlerOptions)" />.</summary>
    public sealed class RegisterSessionHandlerOptions
    {
        int maxConcurrentSessions;
        TimeSpan messageWaitTimeout;
        TimeSpan maxAutoRenewTimeout;

        /// <summary>Initializes a new instance of the <see cref="RegisterSessionHandlerOptions" /> class.
        /// Default Values:
        ///     <see cref="MaxConcurrentSessions"/> = 2000
        ///     <see cref="AutoComplete"/> = true
        ///     <see cref="MessageWaitTimeout"/> = 1 minute
        ///     <see cref="MaxAutoRenewTimeout"/> = 5 minutes
        /// </summary>
        public RegisterSessionHandlerOptions()
        {
            // These are default values
            this.AutoComplete = true;
            this.MaxConcurrentSessions = 2000;
            this.MessageWaitTimeout = TimeSpan.FromMinutes(1);
            this.MaxAutoRenewTimeout = Constants.ClientPumpRenewLockTimeout;
        }

        /// <summary>Occurs when an exception is received. Enables you to be notified of any errors encountered by the session pump.
        /// When errors are received calls will automatically be retried, so this is informational. </summary>
        public event EventHandler<ExceptionReceivedEventArgs> ExceptionReceived;

        /// <summary>Gets or sets the time needed before the session renew its state.</summary>
        /// <value>The time needed before the session renew its state.</value>
        public TimeSpan MaxAutoRenewTimeout
        {
            get
            {
                return this.maxAutoRenewTimeout;
            }

            set
            {
                TimeoutHelper.ThrowIfNegativeArgument(value, nameof(value));
                this.maxAutoRenewTimeout = value;
            }
        }

        /// <summary>Gets or sets the time needed before the message waiting expires.</summary>
        /// <value>The time needed before the message waiting expires.</value>
        public TimeSpan MessageWaitTimeout
        {
            get
            {
                return this.messageWaitTimeout;
            }

            set
            {
                TimeoutHelper.ThrowIfNegativeArgument(value, nameof(value));
                this.messageWaitTimeout = value;
            }
        }

        /// <summary>Gets or sets the maximum number of existing sessions.</summary>
        /// <value>The maximum number of existing sessions.</value>
        public int MaxConcurrentSessions
        {
            get
            {
                return this.maxConcurrentSessions;
            }

            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(Resources.MaxConcurrentCallsMustBeGreaterThanZero.FormatForUser(value));
                }

                this.maxConcurrentSessions = value;
                this.MaxPendingAcceptSessionCalls = Math.Min(value, Environment.ProcessorCount);
            }
        }

        /// <summary>Gets or sets whether the autocomplete option of the session handler is enabled.</summary>
        /// <value>true if the autocomplete option of the session handler is enabled; otherwise, false.</value>
        public bool AutoComplete { get; set; }

        internal bool AutoRenewLock => this.MaxAutoRenewTimeout > TimeSpan.Zero;

        internal int MaxPendingAcceptSessionCalls { get; set; }

        internal void RaiseExceptionReceived(ExceptionReceivedEventArgs e)
        {
            this.ExceptionReceived?.Invoke(this, e);
        }
    }
}