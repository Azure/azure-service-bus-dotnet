// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.ServiceBus.Primitives
{
    /// <summary>
    ///     The exception that is thrown when the Messaging Entity is not found.  Verify Entity Exists.
    /// </summary>
    public sealed class MessagingEntityNotFoundException : ServiceBusException
    {
        internal MessagingEntityNotFoundException(string message)
            : this(message, null)
        {
        }

        internal MessagingEntityNotFoundException(string message, Exception innerException)
            : base(false, message, innerException)
        {
        }
    }
}