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

        Task<RuleDescription> CreateRuleAsync(string topicName, string subscriptionName, RuleDescription ruleDescription, CancellationToken cancellationToken = default);

        // Delete
        Task DeleteQueueAsync(string queueName, CancellationToken cancellationToken = default);

        Task DeleteTopicAsync(string topicName, CancellationToken cancellationToken = default);

        Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default);

        Task DeleteRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default);

        // Get entity
        Task<QueueDescription> GetQueueAsync(string queueName, bool includeRuntimeInfo = false, CancellationToken cancellationToken = default);

        Task<TopicDescription> GetTopicAsync(string topicName, bool includeRuntimeInfo = false, CancellationToken cancellationToken = default);

        Task<SubscriptionDescription> GetSubscriptionAsync(string formattedSubscriptionPath, bool includeRuntimeInfo = false, CancellationToken cancellationToken = default);

        Task<SubscriptionDescription> GetSubscriptionAsync(string topicName, string subscriptionName, bool includeRuntimeInfo = false, CancellationToken cancellationToken = default);

        Task<RuleDescription> GetRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default);

        // Get entities (max of 100 at a time)
        Task<ICollection<string>> GetQueuesAsync(int count = 100, CancellationToken cancellationToken = default);

        Task<ICollection<string>> GetQueuesAsync(int skip, int count, CancellationToken cancellationToken = default);

        Task<ICollection<string>> GetTopicsAsync(int count = 100, CancellationToken cancellationToken = default);

        Task<ICollection<string>> GetTopicsAsync(int skip, int count, CancellationToken cancellationToken = default);

        Task<ICollection<string>> GetSubscriptionsAsync(string topicName, int count = 100, CancellationToken cancellationToken = default);

        Task<ICollection<string>> GetSubscriptionsAsync(string topicName, int skip, int count, CancellationToken cancellationToken = default);

        Task<ICollection<RuleDescription>> GetRulesAsync(string topicName, string subscriptionName, int count = 100, CancellationToken cancellationToken = default);

        Task<ICollection<RuleDescription>> GetRulesAsync(string topicName, string subscriptionName, int skip, int count, CancellationToken cancellationToken = default);

        // Update entity if exists. Else throws.
        Task<QueueDescription> UpdateQueueAsync(QueueDescription queueDescription, CancellationToken cancellationToken = default);

        Task<TopicDescription> UpdateTopicAsync(TopicDescription topicDescription, CancellationToken cancellationToken = default);

        Task<SubscriptionDescription> UpdateSubscriptionAsync(SubscriptionDescription subscriptionDescription, CancellationToken cancellationToken = default);

        // Exists check
        Task<bool> QueueExistsAsync(string queueName, CancellationToken cancellationToken = default);

        Task<bool> TopicExistsAsync(string topicName, CancellationToken cancellationToken = default);

        Task<bool> SubscriptionExistsAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default);
    }
}