// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using Core;

    /// <summary>
    /// Interface used to access a Topic to perform run-time operations.
    /// </summary>
    /// <example>
    /// <code>
    ///
    /// // Create the TopicClient
    /// var connectionStringBuilder = new ServiceBusConnectionStringBuilder(serviceBusConnectionString)
    ///     {
    ///          EntityPath = TopicName
    ///     };
    /// ServiceBusClientFactory factory = new ServiceBusClientFactory();
    /// ITopicClient myTopicClient = factory.CreateTopicClientFromConnectionString(connectionStringBuilder.ToString());
    ///
    /// //********************************************************************************
    /// //                          Sending messages to a Topic
    /// //********************************************************************************
    ///
    /// // Send messages
    /// List &lt;object&gt; Issues = new List &lt;object&gt;();
    /// foreach (var issue in Issues)
    /// {
    ///    myTopicClient.Send(new Message(issue));
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="ServiceBusClientFactory"/>
    public interface ITopicClient : IMessageSender
    {
        string TopicName { get; }
    }
}