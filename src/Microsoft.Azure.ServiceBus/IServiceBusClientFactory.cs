// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using Core;
    using Primitives;

    // TODO: Remove this interface and class after refactoring of MessageSender and MessageReceiver
    public interface IServiceBusClientFactory
    {
        IMessageReceiver CreateMessageReceiverFromConnectionString(
            string entityConnectionString);

        IMessageReceiver CreateMessageReceiverFromConnectionString(
            string entityConnectionString,
            ReceiveMode mode);

        IMessageSender CreateMessageSenderFromConnectionString(
            string entityConnectionString);

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