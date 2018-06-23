// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Management
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IManagementClient
    {
        // Create if not exist. Else throw.

       Task<QueueDescription> CreateQueueAsync(string queuePath, CancellationToken cancellationToken = default);

        Task<QueueDescription> CreateQueueAsync(QueueDescription queueDescription, CancellationToken cancellationToken = default);

        Task<TopicDescription> CreateTopicAsync(string topicPath, CancellationToken cancellationToken = default);

        Task<TopicDescription> CreateTopicAsync(TopicDescription topicDescription, CancellationToken cancellationToken = default);

        Task<SubscriptionDescription> CreateSubscriptionAsync(string topicPath, string subscriptionName, CancellationToken cancellationToken = default);

        Task<SubscriptionDescription> CreateSubscriptionAsync(SubscriptionDescription subscriptionDescription, CancellationToken cancellationToken = default);

        Task<SubscriptionDescription> CreateSubscriptionAsync(SubscriptionDescription subscriptionDescription, RuleDescription defaultRule, CancellationToken cancellationToken = default);

        Task<RuleDescription> CreateRuleAsync(string topicPath, string subscriptionName, RuleDescription ruleDescription, CancellationToken cancellationToken = default);

        // Delete
        Task DeleteQueueAsync(string queuePath, CancellationToken cancellationToken = default);

        Task DeleteTopicAsync(string topicPath, CancellationToken cancellationToken = default);

        Task DeleteSubscriptionAsync(string topicPath, string subscriptionName, CancellationToken cancellationToken = default);

        Task DeleteRuleAsync(string topicPath, string subscriptionName, string ruleName, CancellationToken cancellationToken = default);

        // Get entity description
        Task<QueueDescription> GetQueueAsync(string queuePath, CancellationToken cancellationToken = default);

        Task<TopicDescription> GetTopicAsync(string topicPath, CancellationToken cancellationToken = default);

        Task<SubscriptionDescription> GetSubscriptionAsync(string topicPath, string subscriptionName, CancellationToken cancellationToken = default);

        Task<RuleDescription> GetRuleAsync(string topicPath, string subscriptionName, string ruleName, CancellationToken cancellationToken = default);

        // Get entity runtime information
        Task<QueueRuntimeInfo> GetQueueRuntimeInfoAsync(string queuePath, CancellationToken cancellationToken = default);

        Task<TopicRuntimeInfo> GetTopicRuntimeInfoAsync(string topicPath, CancellationToken cancellationToken = default);

        Task<SubscriptionRuntimeInfo> GetSubscriptionRuntimeInfoAsync(string topicPath, string subscriptionName, CancellationToken cancellationToken = default);

        // Get entities (max of 100 at a time)
        Task<IList<QueueDescription>> GetQueuesAsync(int count = 100, int skip = 0, CancellationToken cancellationToken = default);

        Task<IList<TopicDescription>> GetTopicsAsync(int count = 100, int skip = 0, CancellationToken cancellationToken = default);

        Task<IList<SubscriptionDescription>> GetSubscriptionsAsync(string topicPath, int count = 100, int skip = 0, CancellationToken cancellationToken = default);

        Task<IList<RuleDescription>> GetRulesAsync(string topicPath, string subscriptionName, int count = 100, int skip = 0, CancellationToken cancellationToken = default);

        // Update entity if exists. Else throws.
        Task<QueueDescription> UpdateQueueAsync(QueueDescription queueDescription, CancellationToken cancellationToken = default);

        Task<TopicDescription> UpdateTopicAsync(TopicDescription topicDescription, CancellationToken cancellationToken = default);

        Task<SubscriptionDescription> UpdateSubscriptionAsync(SubscriptionDescription subscriptionDescription, CancellationToken cancellationToken = default);

        Task<RuleDescription> UpdateRuleAsync(string topicPath, string subscriptionName, RuleDescription ruleDescription, CancellationToken cancellationToken = default);

        // Exists check
        Task<bool> QueueExistsAsync(string queuePath, CancellationToken cancellationToken = default);

        Task<bool> TopicExistsAsync(string topicPath, CancellationToken cancellationToken = default);

        Task<bool> SubscriptionExistsAsync(string topicPath, string subscriptionName, CancellationToken cancellationToken = default);

        Task CloseAsync();
    }
}