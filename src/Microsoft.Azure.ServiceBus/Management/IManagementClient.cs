using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.ServiceBus.Management
{
    public interface IManagementClient
    {
        // Create if not exist. Else throw.
        Task<QueueDescription> CreateQueueAsync(string queueName);

        Task<QueueDescription> CreateQueueAsync(string queueName, CancellationToken cancellationToken);

        Task<QueueDescription> CreateQueueAsync(QueueDescription queueDescription);

        Task<QueueDescription> CreateQueueAsync(QueueDescription queueDescription, CancellationToken cancellationToken);

        Task<TopicDescription> CreateTopicAsync(string topicName);

        Task<TopicDescription> CreateTopicAsync(string topicName, CancellationToken cancellationToken);

        Task<TopicDescription> CreateTopicAsync(TopicDescription topicDescription);

        Task<TopicDescription> CreateTopicAsync(TopicDescription topicDescription, CancellationToken cancellationToken);

        Task<SubscriptionDescription> CreateSubscriptionAsync(string topicName, string subscriptionName);

        Task<SubscriptionDescription> CreateSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken);

        Task<SubscriptionDescription> CreateSubscriptionAsync(SubscriptionDescription subscriptionDescription);

        Task<SubscriptionDescription> CreateSubscriptionAsync(SubscriptionDescription subscriptionDescription, CancellationToken cancellationToken);

        Task<RuleDescription> CreateRuleAsync(string topicName, string subscriptionName, RuleDescription ruleDescription);

        Task<RuleDescription> CreateRuleAsync(string topicName, string subscriptionName, RuleDescription ruleDescription, CancellationToken cancellationToken);

        // Delete
        Task DeleteQueueAsync(string queueName);

        Task DeleteQueueAsync(string queueName, CancellationToken cancellationToken);

        Task DeleteTopicAsync(string topicName);

        Task DeleteTopicAsync(string topicName, CancellationToken cancellationToken);

        Task DeleteSubscriptionAsync(string topicName, string subscriptionName);

        Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken);

        Task DeleteRuleAsync(string topicName, string subscriptionName, string ruleName);

        Task DeleteRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken);

        // Get entity
        Task<QueueDescription> GetQueueAsync(string queueName);

        Task<QueueDescription> GetQueueAsync(string queueName, CancellationToken cancellationToken);

        Task<QueueInfo> GetQueueRuntimeInfoAsync(string queueName);

        Task<QueueInfo> GetQueueRuntimeInfoAsync(string queueName, CancellationToken cancellationToken);

        Task<TopicDescription> GetTopicAsync(string topicName);

        Task<TopicDescription> GetTopicAsync(string topicName, CancellationToken cancellationToken);

        Task<TopicInfo> GetTopicRuntimeInfoAsync(string topicName);

        Task<TopicInfo> GetTopicRuntimeInfoAsync(string topicName, CancellationToken cancellationToken);

        Task<SubscriptionDescription> GetSubscriptionAsync(string formattedSubscriptionPath);

        Task<SubscriptionDescription> GetSubscriptionAsync(string formattedSubscriptionPath, CancellationToken cancellationToken);

        Task<SubscriptionDescription> GetSubscriptionAsync(string topicName, string subscriptionName);

        Task<SubscriptionDescription> GetSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken);

        Task<SubscriptionInfo> GetSubscriptionRuntimeInfoAsync(string topicName, string subscriptionName);

        Task<SubscriptionInfo> GetSubscriptionRuntimeInfoAsync(string topicName, string subscriptionName, CancellationToken cancellationToken);

        Task<RuleDescription> GetRuleAsync(string topicName, string subscriptionName, string ruleName);

        Task<RuleDescription> GetRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken);

        // Get entities
        // Limits to first 100.
        Task<ICollection<string>> GetQueuesAsync(int count = 100);

        // TODO: Should we expose GetQueuesCount()?
        Task<ICollection<string>> GetQueuesAsync(int skip, int count);

        Task<ICollection<string>> GetTopicsAsync(int count = 100);

        Task<ICollection<string>> GetTopicsAsync(int skip, int count);

        Task<ICollection<string>> GetSubscriptionsAsync(string topicName, int count = 100);

        Task<ICollection<string>> GetSubscriptionsAsync(string topicName, int skip, int count);

        Task<ICollection<RuleDescription>> GetRulesAsync(string topicName, string subscriptionName, int count = 100);

        Task<ICollection<RuleDescription>> GetRulesAsync(string topicName, string subscriptionName, int skip, int count);

        // Update entity if exists. Else throws.
        Task<QueueDescription> UpdateQueueAsync(QueueDescription queueDescription);

        Task<QueueDescription> UpdateQueueAsync(QueueDescription queueDescription, CancellationToken cancellationToken);

        Task<TopicDescription> UpdateTopicAsync(TopicDescription topicDescription);

        Task<TopicDescription> UpdateTopicAsync(TopicDescription topicDescription, CancellationToken cancellationToken);

        Task<SubscriptionDescription> UpdateSubscriptionAsync(SubscriptionDescription subscriptionDescription);

        Task<SubscriptionDescription> UpdateSubscriptionAsync(SubscriptionDescription subscriptionDescription, CancellationToken cancellationToken);

        // Exists check
        Task<bool> QueueExistsAsync(string queueName);

        Task<bool> QueueExistsAsync(string queueName, CancellationToken cancellationToken);

        Task<bool> TopicExistsAsync(string topicName);

        Task<bool> TopicExistsAsync(string topicName, CancellationToken cancellationToken);

        Task<bool> SubscriptionExistsAsync(string topicName, string subscriptionName);

        Task<bool> SubscriptionExistsAsync(string topicName, string subscriptionName, CancellationToken cancellationToken);
    }
}