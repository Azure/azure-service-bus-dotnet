// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;

    /// <summary>
    /// Exception for ServiceBus plugin errors.
    /// </summary>
    public class ServiceBusPluginException : Exception
    {
        public ServiceBusPluginException(bool shouldCompleteOperation)
        {
            this.ShouldCompleteOperation = shouldCompleteOperation;
        }

        public ServiceBusPluginException(bool shouldCompleteOperation, string message)
            : base(message)
        {
            this.ShouldCompleteOperation = shouldCompleteOperation;
        }

        public ServiceBusPluginException(bool shouldCompleteOperation, Exception innerException)
            : base(innerException.Message, innerException)
        {
            this.ShouldCompleteOperation = shouldCompleteOperation;
        }

        public ServiceBusPluginException(bool shouldCompleteOperation, string message, Exception innerException)
            : base(message, innerException)
        {
            this.ShouldCompleteOperation = shouldCompleteOperation;
        }

        /// <summary>
        /// A boolean indicating if operation should continue without processing the plugin operation.
        /// </summary>
        public bool ShouldCompleteOperation { get; }
    }
}