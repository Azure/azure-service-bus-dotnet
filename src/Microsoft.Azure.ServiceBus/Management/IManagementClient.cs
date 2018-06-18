using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.ServiceBus.Management
{
    public interface IManagementClient
    {
        // Create if not exist. Else throw.
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