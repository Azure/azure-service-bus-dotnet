﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Primitives
{
    using System;

    /// <summary>Provides data for the <see cref="MessageHandlerOptions.ExceptionReceived" /> event.</summary>
    public sealed class ExceptionReceivedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ExceptionReceivedEventArgs" /> class.</summary>
        /// <param name="exception">The exception that this event data belongs to.</param>
        /// <param name="action">The action associated with the event.</param>
        /// <param name="namespaceName">The namespace name used when this exception occurred.</param>
        /// <param name="entityName">The entity path used when this exception occurred.</param>
        public ExceptionReceivedEventArgs(Exception exception, string action, string namespaceName, string entityName)
        {
            this.Exception = exception;
            this.ExceptionReceivedContext = new ExceptionReceivedContext(action, namespaceName, entityName);
        }

        /// <summary>Gets the parent class exception to which this event data belongs.</summary>
        /// <value>The exception, generated by the parent class, to which this event data belongs.</value>
        public Exception Exception { get; private set; }

        /// <summary>
        /// Gets the context of the exception (action, namespace name, and entity path).
        /// </summary>
        public ExceptionReceivedContext ExceptionReceivedContext { get; private set; }
    }
}