// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Management
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Primitives;

    public interface IManagementClient
    {
        // Create if not exist. Else throw.

        /// <summary>
        /// Creates a new queue in the service namespace with the given name.
        /// </summary>
        /// <remarks>Throws if a queue already exists.</remarks>
        /// <param name="queueName">The name of the queue relative to the service namespace base address.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The <see cref="QueueDescription"/> of the newly created queue.</returns>
        /// <exception cref="ArgumentNullException">Queue name is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The length of name is greater than 260 characters.</exception>
        /// <exception cref="MessagingEntityAlreadyExistsException">A queue with the same nameexists under the same service namespace.</exception>
        /// <exception cref="ServiceBusTimeoutException">The operation times out.</exception>
        /// <exception cref="UnauthorizedAccessException">No sufficient permission to perform this operation. You should check to ensure that your <see cref="ManagementClient"/> has the correct <see cref="TokenProvider"/> credentials to perform this operation.</exception>
        /// <exception cref="QuotaExceededException">Either the specified size in the description is not supported or the maximum allowable quota has been reached. You must specify one of the supported size values, delete existing entities, or increase your quota size.</exception>
        /// <exception cref="ServerBusyException">The server is overloaded with logical operations. You can consider any of the following actions:Wait and retry calling this function.Remove entities before retry (for example, receive messages before sending any more).</exception>
        /// <exception cref="ServiceBusException">An internal error or unexpected exception occurs.</exception>
        Task<QueueDescription> CreateQueueAsync(string queueName, CancellationToken cancellationToken = default);

        Task<QueueDescription> CreateQueueAsync(QueueDescription queueDescription, CancellationToken cancellationToken = default);

        Task<TopicDescription> CreateTopicAsync(string topicName, CancellationToken cancellationToken = default);

        Task<TopicDescription> CreateTopicAsync(TopicDescription topicDescription, CancellationToken cancellationToken = default);

        Task<SubscriptionDescription> CreateSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default);

        Task<SubscriptionDescription> CreateSubscriptionAsync(SubscriptionDescription subscriptionDescription, CancellationToken cancellationToken = default);

        Task<SubscriptionDescription> CreateSubscriptionAsync(SubscriptionDescription subscriptionDescription, RuleDescription defaultRule, CancellationToken cancellationToken = default);

        Task<RuleDescription> CreateRuleAsync(string topicName, string subscriptionName, RuleDescription ruleDescription, CancellationToken cancellationToken = default);

        // Delete
        Task DeleteQueueAsync(string queueName, CancellationToken cancellationToken = default);

        Task DeleteTopicAsync(string topicName, CancellationToken cancellationToken = default);

        Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default);

        Task DeleteRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default);

        // Get entity description
        Task<QueueDescription> GetQueueAsync(string queueName, CancellationToken cancellationToken = default);

        Task<TopicDescription> GetTopicAsync(string topicName, CancellationToken cancellationToken = default);

        Task<SubscriptionDescription> GetSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default);

        Task<RuleDescription> GetRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default);

        // Get entity runtime information
        Task<QueueRuntimeInfo> GetQueueRuntimeInfoAsync(string queueName, CancellationToken cancellationToken = default);

        Task<TopicRuntimeInfo> GetTopicRuntimeInfoAsync(string topicName, CancellationToken cancellationToken = default);

        Task<SubscriptionRuntimeInfo> GetSubscriptionRuntimeInfoAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default);

        // Get entities (max of 100 at a time)
        Task<IList<QueueDescription>> GetQueuesAsync(int count = 100, int skip = 0, CancellationToken cancellationToken = default);

        Task<IList<TopicDescription>> GetTopicsAsync(int count = 100, int skip = 0, CancellationToken cancellationToken = default);

        Task<IList<SubscriptionDescription>> GetSubscriptionsAsync(string topicName, int count = 100, int skip = 0, CancellationToken cancellationToken = default);

        Task<IList<RuleDescription>> GetRulesAsync(string topicName, string subscriptionName, int count = 100, int skip = 0, CancellationToken cancellationToken = default);

        // Update entity if exists. Else throws.
        Task<QueueDescription> UpdateQueueAsync(QueueDescription queueDescription, CancellationToken cancellationToken = default);

        Task<TopicDescription> UpdateTopicAsync(TopicDescription topicDescription, CancellationToken cancellationToken = default);

        Task<SubscriptionDescription> UpdateSubscriptionAsync(SubscriptionDescription subscriptionDescription, CancellationToken cancellationToken = default);

        Task<RuleDescription> UpdateRuleAsync(string topicName, string subscriptionName, RuleDescription ruleDescription, CancellationToken cancellationToken = default);

        // Exists check
        Task<bool> QueueExistsAsync(string queueName, CancellationToken cancellationToken = default);

        Task<bool> TopicExistsAsync(string topicName, CancellationToken cancellationToken = default);

        Task<bool> SubscriptionExistsAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default);

        Task CloseAsync();
    }
}