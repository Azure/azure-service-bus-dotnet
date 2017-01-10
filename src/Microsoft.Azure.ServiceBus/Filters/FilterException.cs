// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Filters
{
    using System;

    /// <summary>
    /// The exception that is thrown for signaling filter action errors.
    /// </summary>
    public sealed class FilterException : ServiceBusException
    {
        /// <summary>Initializes a new instance of the
        /// <see cref="FilterException" /> class using the specified message.</summary>
        /// <param name="message">The exception message.</param>
        public FilterException(string message)
            : base(isTransient: false, message: message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterException" /> class using the specified message and inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        public FilterException(string message, Exception innerException)
            : base(isTransient: false, message: message, innerException: innerException)
        {
        }
    }
}