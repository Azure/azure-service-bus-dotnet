// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Primitives
{
    /// <summary>
    ///     This class can be used to format the path for different Service Bus entity types.
    /// </summary>
    public static class EntityNameHelper
    {
        const string PathDelimiter = @"/";
        const string Subscriptions = "Subscriptions";
        const string SubQueuePrefix = "$";
        const string DeadLetterQueueSuffix = "DeadLetterQueue";
        const string DeadLetterQueueName = SubQueuePrefix + DeadLetterQueueSuffix;
        const string Transfer = "Transfer";
        const string TransferDeadLetterQueueName = SubQueuePrefix + Transfer + PathDelimiter + DeadLetterQueueName;

        /// <summary>
        ///     Formats the dead letter path for either a queue, or a subscription.
        /// </summary>
        /// <param name="entityPath">The name of the queue, or path of the subscription.</param>
        /// <returns>The path as a string of the dead letter entity.</returns>
        public static string FormatDeadLetterPath(string entityPath)
        {
            return FormatSubQueuePath(entityPath, DeadLetterQueueName);
        }

        /// <summary>
        ///     Formats the subqueue path for either a queue, or a subscription.
        /// </summary>
        /// <param name="entityPath">The name of the queue, or path of the subscription.</param>
        /// <param name="subQueueName">The name of the subqueue.</param>
        /// <returns>The path as a string of the subqueue entity.</returns>
        public static string FormatSubQueuePath(string entityPath, string subQueueName)
        {
            return string.Concat(entityPath, PathDelimiter, subQueueName);
        }

        /// <summary>
        ///     Formats the subscription path, based on the topic path and subscription name.
        /// </summary>
        /// <param name="topicPath">The name of the topic, including slashes.</param>
        /// <param name="subscriptionName">The name of the subscription.</param>
        /// <returns></returns>
        public static string FormatSubscriptionPath(string topicPath, string subscriptionName)
        {
            return string.Concat(topicPath, PathDelimiter, Subscriptions, PathDelimiter, subscriptionName);
        }

        /// <summary>
        ///     Utility method that creates the name for the transfer dead letter receiver, specified by
        ///     <paramref name="entityPath" />
        /// </summary>
        public static string Format​Transfer​Dead​Letter​Path(string entityPath)
        {
            return string.Concat(entityPath, PathDelimiter, TransferDeadLetterQueueName);
        }
    }
}