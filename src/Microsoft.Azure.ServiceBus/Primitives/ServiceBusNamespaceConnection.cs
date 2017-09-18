﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Primitives
{
    using System;

    internal class ServiceBusNamespaceConnection : ServiceBusConnection
    {
        public ServiceBusNamespaceConnection(string namespaceConnectionString)
            : this(namespaceConnectionString, Constants.DefaultOperationTimeout, RetryPolicy.Default)
        {
        }

        public ServiceBusNamespaceConnection(string namespaceConnectionString, TimeSpan operationTimeout, RetryPolicy retryPolicy)
            : base(operationTimeout, retryPolicy)
        {
            Guard.AgainstNullAndEmpty(nameof(namespaceConnectionString), namespaceConnectionString);

            var serviceBusConnectionStringBuilder = new ServiceBusConnectionStringBuilder(namespaceConnectionString);
            if (!string.IsNullOrWhiteSpace(serviceBusConnectionStringBuilder.EntityPath))
            {
                throw Fx.Exception.Argument(nameof(namespaceConnectionString), "NamespaceConnectionString should not contain EntityPath.");
            }

            this.InitializeConnection(serviceBusConnectionStringBuilder);
        }
    }
}