// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Filters
{
    using System;

    /// <summary>The exception that is thrown for signaling filter action errors and is thrown when a filter related operation fails.</summary>
    public sealed class RuleActionException : ServiceBusException
    {
        /// <summary>Initializes a new instance of the
        /// <see cref="RuleActionException" /> class with the specified error message.</summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public RuleActionException(string message)
            : base(isTransient: false, message: message)
        {
        }

        /// <summary>Initializes a new instance of the
        /// <see cref="RuleActionException" /> class with the specified error message and a reference to the inner exception that is the cause of this exception.</summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public RuleActionException(string message, Exception innerException)
            : base(isTransient: false, message: message, innerException: innerException)
        {
        }
    }
}