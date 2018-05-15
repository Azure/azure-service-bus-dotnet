using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.ServiceBus.Management
{
    public interface IManagementClient
    {
        // Create if not exist. Else throw.
        Task<QueueDescription> CreateQueueAsync(string queueName);

        Task<QueueDescription> CreateQueueAsync(QueueDescription queueDescription);

        Task<TopicDescription> CreateTopicAsync(string topicName);

        Task<TopicDescription> CreateTopicAsync(TopicDescription topicDescription);

        Task<SubscriptionDescription> CreateSubscriptionAsync(string topicName, string subscriptionName);

        Task<SubscriptionDescription> CreateSubscriptionAsync(SubscriptionDescription subscriptionDescription);

        Task<RuleDescription> CreateRuleAsync(string topicName, string subscriptionName, RuleDescription ruleDescription);

        // Delete
        Task DeleteQueueAsync(string queueName);

        Task DeleteTopicAsync(string topicName);

        Task DeleteSubscriptionAsync(string topicName, string subscriptionName);

        Task DeleteRuleAsync(string topicName, string subscriptionName, string ruleName);

        // Get entity
        Task<QueueDescription> GetQueueAsync(string queueName);

        Task<QueueDescription> GetQueueRuntimeInfoAsync(string queueName);

        Task<TopicDescription> GetTopicAsync(string topicName);

        Task<TopicDescription> GetTopicRuntimeInfoAsync(string topicName);

        Task<SubscriptionDescription> GetSubscriptionAsync(string formattedSubscriptionPath);

        Task<SubscriptionDescription> GetSubscriptionAsync(string topicName, string subscriptionName);

        Task<SubscriptionRuntimeInfo> GetSubscriptionRuntimeInfoAsync(string topicName, string subscriptionName);

        Task<RuleDescription> GetRuleAsync(string topicName, string subscriptionName, string ruleName);

        // Get entities
        // Limits to first 100.
        Task<ICollection<string>> GetQueuesAsync();

        // TODO: Alternatively, expose it as IEnumerable<> and in background keep calling GetQueues() in a batch of 100.
        // TODO: Should we expose GetQueuesCount()?
        Task<ICollection<string>> GetQueuesAsync(int skip, int count);

        Task<ICollection<string>> GetTopicsAsync();

        Task<ICollection<string>> GetTopicsAsync(int skip, int count);

        Task<ICollection<string>> GetSubscriptionsAsync(string topicName);

        Task<ICollection<string>> GetSubscriptionsAsync(string topicName, int skip, int count);

        Task<ICollection<RuleDescription>> GetRulesAsync(string topicName, string subscriptionName);

        Task<ICollection<RuleDescription>> GetRulesAsync(string topicName, string subscriptionName, int skip, int count);

        // Update entity if exists. Else throws.
        Task<QueueDescription> UpdateQueueAsync(QueueDescription queueDescription);

        Task<TopicDescription> UpdateTopicAsync(TopicDescription topicDescription);

        Task<SubscriptionDescription> UpdateSubscriptionAsync(SubscriptionDescription subscriptionDescription);

        // Upsert entity - Creates or updates
        // TODO: Should this be called upsert? Better name v/s descriptive name?
        Task<QueueDescription> CreateOrUpdateQueueAsync(QueueDescription queueDescription);

        Task<TopicDescription> CreateOrUpdateTopicAsync(TopicDescription topicDescription);

        Task<SubscriptionDescription> CreateOrUpdateSubscriptionAsync(SubscriptionDescription subscriptionDescription);

        // TODO: Do we have CreateOrUpdate for rule?

        // Exists check
        Task<bool> QueueExistsAsync(string queueName);

        Task<bool> TopicExistsAsync(string topicName);

        Task<bool> SubscriptionExistsAsync(string topicName, string subscriptionName);
    }
}
