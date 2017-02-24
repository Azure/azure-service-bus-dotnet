// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using Core;
    using Primitives;

    public class ServiceBusClientFactory : IServiceBusClientFactory
    {
        public IMessageReceiver CreateMessageReceiverFromConnectionString(string entityConnectionString)
        {
            return this.CreateMessageReceiverFromConnectionString(entityConnectionString, ReceiveMode.PeekLock);
        }

        public IMessageReceiver CreateMessageReceiverFromConnectionString(string entityConnectionString, ReceiveMode mode)
        {
            if (string.IsNullOrWhiteSpace(entityConnectionString))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(entityConnectionString));
            }

            ServiceBusEntityConnection entityConnection = new ServiceBusEntityConnection(entityConnectionString);
            return entityConnection.CreateMessageReceiver(entityConnection.EntityPath, mode);
        }

        public IMessageReceiver CreateMessageReceiver(ServiceBusNamespaceConnection namespaceConnection, string entityPath)
        {
            return this.CreateMessageReceiver(namespaceConnection, entityPath, ReceiveMode.PeekLock);
        }

        public IMessageReceiver CreateMessageReceiver(ServiceBusNamespaceConnection namespaceConnection, string entityPath, ReceiveMode mode)
        {
            if (namespaceConnection == null)
            {
                throw Fx.Exception.Argument(
                    nameof(namespaceConnection),
                    "Namespace Connection is null. Create a connection using the NamespaceConnection class");
            }

            if (string.IsNullOrWhiteSpace(entityPath))
            {
                throw Fx.Exception.Argument(nameof(namespaceConnection), "Entity Path is null");
            }

            return namespaceConnection.CreateMessageReceiver(entityPath, mode);
        }

        public IMessageReceiver CreateMessageReceiver(ServiceBusEntityConnection entityConnection)
        {
            return this.CreateMessageReceiver(entityConnection, ReceiveMode.PeekLock);
        }

        public IMessageReceiver CreateMessageReceiver(ServiceBusEntityConnection entityConnection, ReceiveMode mode)
        {
            if (entityConnection == null)
            {
                throw Fx.Exception.Argument(
                    nameof(entityConnection),
                    "Namespace Connection is null. Create a connection using the NamespaceConnection class");
            }

            return entityConnection.CreateMessageReceiver(entityConnection.EntityPath, mode);
        }

        public IMessageSender CreateMessageSenderFromConnectionString(string entityConnectionString)
        {
            if (string.IsNullOrWhiteSpace(entityConnectionString))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(entityConnectionString));
            }

            ServiceBusEntityConnection entityConnection = new ServiceBusEntityConnection(entityConnectionString);
            return entityConnection.CreateMessageSender(entityConnection.EntityPath);
        }

        public IMessageSender CreateMessageSender(ServiceBusNamespaceConnection namespaceConnection, string entityPath)
        {
            if (namespaceConnection == null)
            {
                throw Fx.Exception.Argument(
                    nameof(namespaceConnection),
                    $"Namespace Connection is null. Create a connection using the {nameof(ServiceBusNamespaceConnection)} class");
            }

            if (string.IsNullOrWhiteSpace(entityPath))
            {
                throw Fx.Exception.Argument(nameof(namespaceConnection), "Entity Path is null");
            }

            return namespaceConnection.CreateMessageSender(entityPath);
        }

        public IMessageSender CreateMessageSender(ServiceBusEntityConnection entityConnection)
        {
            if (entityConnection == null)
            {
                throw Fx.Exception.Argument(
                    nameof(entityConnection),
                    $"Entity Connection is null. Create a connection using the {nameof(ServiceBusEntityConnection)} class");
            }

            return entityConnection.CreateMessageSender(entityConnection.EntityPath);
        }
    }
}