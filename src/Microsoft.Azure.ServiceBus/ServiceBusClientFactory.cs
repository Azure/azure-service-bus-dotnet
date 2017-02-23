// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.ServiceBus.Core;

namespace Microsoft.Azure.ServiceBus
{
    using Primitives;

    public class ServiceBusClientFactory : IServiceBusClientFactory
    {
        public IQueueClient CreateQueueClientFromConnectionString(string entityConnectionString)
        {
            return this.CreateQueueClientFromConnectionString(entityConnectionString, ReceiveMode.PeekLock);
        }

        public IQueueClient CreateQueueClientFromConnectionString(string entityConnectionString, ReceiveMode mode)
        {
            if (string.IsNullOrWhiteSpace(entityConnectionString))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(entityConnectionString));
            }

            ServiceBusEntityConnection entityConnection = new ServiceBusEntityConnection(entityConnectionString);
            return entityConnection.CreateQueueClient(entityConnection.EntityPath, mode);
        }

        public IQueueClient CreateQueueClient(ServiceBusNamespaceConnection namespaceConnection, string entityPath)
        {
            return this.CreateQueueClient(namespaceConnection, entityPath, ReceiveMode.PeekLock);
        }

        public IQueueClient CreateQueueClient(ServiceBusNamespaceConnection namespaceConnection, string entityPath, ReceiveMode mode)
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

            return namespaceConnection.CreateQueueClient(entityPath, mode);
        }

        public IQueueClient CreateQueueClient(ServiceBusEntityConnection entityConnection)
        {
            return this.CreateQueueClient(entityConnection, ReceiveMode.PeekLock);
        }

        public IQueueClient CreateQueueClient(ServiceBusEntityConnection entityConnection, ReceiveMode mode)
        {
            if (entityConnection == null)
            {
                throw Fx.Exception.Argument(
                    nameof(entityConnection),
                    "Namespace Connection is null. Create a connection using the NamespaceConnection class");
            }

            return entityConnection.CreateQueueClient(entityConnection.EntityPath, mode);
        }

        public ITopicClient CreateTopicClientFromConnectionString(string entityConnectionString)
        {
            if (string.IsNullOrWhiteSpace(entityConnectionString))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(entityConnectionString));
            }

            ServiceBusEntityConnection entityConnection = new ServiceBusEntityConnection(entityConnectionString);
            return entityConnection.CreateTopicClient(entityConnection.EntityPath);
        }

        public ITopicClient CreateTopicClient(ServiceBusNamespaceConnection namespaceConnection, string entityPath)
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

            return namespaceConnection.CreateTopicClient(entityPath);
        }

        public ITopicClient CreateTopicClient(ServiceBusEntityConnection entityConnection)
        {
            if (entityConnection == null)
            {
                throw Fx.Exception.Argument(
                    nameof(entityConnection),
                    "Namespace Connection is null. Create a connection using the NamespaceConnection class");
            }

            return entityConnection.CreateTopicClient(entityConnection.EntityPath);
        }

        public ISubscriptionClient CreateSubscriptionClientFromConnectionString(string topicEntityConnectionString, string subscriptionName)
        {
            return this.CreateSubscriptionClientFromConnectionString(
                topicEntityConnectionString,
                subscriptionName,
                ReceiveMode.PeekLock);
        }

        public ISubscriptionClient CreateSubscriptionClientFromConnectionString(string topicEntityConnectionString, string subscriptionName, ReceiveMode mode)
        {
            if (string.IsNullOrWhiteSpace(topicEntityConnectionString))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(topicEntityConnectionString));
            }

            ServiceBusEntityConnection topicConnection = new ServiceBusEntityConnection(topicEntityConnectionString);
            return topicConnection.CreateSubscriptionClient(topicConnection.EntityPath, subscriptionName, mode);
        }

        public ISubscriptionClient CreateSubscriptionClient(ServiceBusNamespaceConnection namespaceConnection, string topicPath, string subscriptionName)
        {
            return this.CreateSubscriptionClient(namespaceConnection, topicPath, subscriptionName, ReceiveMode.PeekLock);
        }

        public ISubscriptionClient CreateSubscriptionClient(ServiceBusNamespaceConnection namespaceConnection, string topicPath, string subscriptionName, ReceiveMode mode)
        {
            if (namespaceConnection == null)
            {
                throw Fx.Exception.Argument(
                    nameof(namespaceConnection),
                    "Namespace Connection is null. Create a connection using the NamespaceConnection class");
            }

            if (string.IsNullOrWhiteSpace(topicPath))
            {
                throw Fx.Exception.Argument(nameof(namespaceConnection), "Topic Path is null");
            }

            return namespaceConnection.CreateSubscriptionClient(topicPath, subscriptionName, mode);
        }

        public ISubscriptionClient CreateSubscriptionClient(ServiceBusEntityConnection topicConnection, string subscriptionName)
        {
            return this.CreateSubscriptionClient(topicConnection, subscriptionName, ReceiveMode.PeekLock);
        }

        public ISubscriptionClient CreateSubscriptionClient(ServiceBusEntityConnection topicConnection, string subscriptionName, ReceiveMode mode)
        {
            if (topicConnection == null)
            {
                throw Fx.Exception.Argument(
                    nameof(topicConnection),
                    "Namespace Connection is null. Create a connection using the NamespaceConnection class");
            }

            return topicConnection.CreateSubscriptionClient(topicConnection.EntityPath, subscriptionName, mode);
        }

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