// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using Microsoft.Azure.ServiceBus.Primitives;

    public class ServiceBusConnection : ServiceBusConnectionBase
    {
        public ServiceBusConnection(ServiceBusConnectionStringBuilder connectionStringBuilder)
            : this(connectionStringBuilder?.GetNamespaceConnectionString())
        {
        }

        public ServiceBusConnection(string namespaceConnectionString)
            : this(namespaceConnectionString, Constants.DefaultOperationTimeout, RetryPolicy.Default)
        {
        }

        public ServiceBusConnection(string namespaceConnectionString, TimeSpan operationTimeout, RetryPolicy retryPolicy)
            : base(operationTimeout, retryPolicy)
        {
            if (string.IsNullOrWhiteSpace(namespaceConnectionString))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(namespaceConnectionString));
            }

            var serviceBusConnectionStringBuilder = new ServiceBusConnectionStringBuilder(namespaceConnectionString);
            if (!string.IsNullOrWhiteSpace(serviceBusConnectionStringBuilder.EntityPath))
            {
                throw Fx.Exception.Argument(nameof(namespaceConnectionString), "NamespaceConnectionString should not contain EntityPath.");
            }

            this.InitializeConnection(serviceBusConnectionStringBuilder);
        }

        public ServiceBusConnection(string endpoint, TransportType transportType, RetryPolicy retryPolicy)
            : base(Constants.DefaultOperationTimeout, retryPolicy)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(endpoint));
            }

            var serviceBusConnectionStringBuilder = new ServiceBusConnectionStringBuilder()
            {
                Endpoint = endpoint,
                TransportType = transportType
            };

            this.InitializeConnection(serviceBusConnectionStringBuilder);
        }
    }
}