// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using Primitives;

    public interface IServiceBusFactory
    {
        IQueueClient CreateQueueClientFromConnectionString(
            string entityConnectionString);

        IQueueClient CreateQueueClientFromConnectionString(
            string entityConnectionString,
            ReceiveMode mode);

        ITopicClient CreateTopicClientFromConnectionString(
            string entityConnectionString);

        ISubscriptionClient CreateSubscriptionClientFromConnectionString(
            string topicEntityConnectionString,
            string subscriptionName);

        ISubscriptionClient CreateSubscriptionClientFromConnectionString(
            string topicEntityConnectionString,
            string subscriptionName,
            ReceiveMode mode);

        IMessageReceiver CreateMessageReceiverFromConnectionString(
            string entityConnectionString);

        IMessageReceiver CreateMessageReceiverFromConnectionString(
            string entityConnectionString,
            ReceiveMode mode);

        IMessageSender CreateMessageSenderFromConnectionString(
            string entityConnectionString);

        IQueueClient CreateQueueClient(
            ServiceBusNamespaceConnection namespaceConnection,
            string entityPath);

        IQueueClient CreateQueueClient(
            ServiceBusNamespaceConnection namespaceConnection,
            string entityPath,
            ReceiveMode mode);

        IQueueClient CreateQueueClient(
            ServiceBusEntityConnection entityConnection);

        IQueueClient CreateQueueClient(
            ServiceBusEntityConnection entityConnection,
            ReceiveMode mode);

        ITopicClient CreateTopicClient(
            ServiceBusNamespaceConnection namespaceConnection,
            string entityPath);

        ITopicClient CreateTopicClient(
            ServiceBusEntityConnection entityConnection);

        ISubscriptionClient CreateSubscriptionClient(
            ServiceBusNamespaceConnection namespaceConnection,
            string topicPath,
            string subscriptionName);

        ISubscriptionClient CreateSubscriptionClient(
            ServiceBusNamespaceConnection namespaceConnection,
            string topicPath,
            string subscriptionName,
            ReceiveMode mode);

        ISubscriptionClient CreateSubscriptionClient(
            ServiceBusEntityConnection topicConnection,
            string subscriptionName);

        ISubscriptionClient CreateSubscriptionClient(
            ServiceBusEntityConnection topicConnection,
            string subscriptionName,
            ReceiveMode mode);

        IMessageReceiver CreateMessageReceiver(
            ServiceBusNamespaceConnection namespaceConnection,
            string entityPath);

        IMessageReceiver CreateMessageReceiver(
            ServiceBusNamespaceConnection namespaceConnection,
            string entityPath,
            ReceiveMode mode);

        IMessageReceiver CreateMessageReceiver(
            ServiceBusEntityConnection entityConnection);

        IMessageReceiver CreateMessageReceiver(
            ServiceBusEntityConnection entityConnection,
            ReceiveMode mode);

        IMessageSender CreateMessageSender(
            ServiceBusNamespaceConnection namespaceConnection,
            string entityPath);

        IMessageSender CreateMessageSender(
            ServiceBusEntityConnection entityConnection);
    }
}