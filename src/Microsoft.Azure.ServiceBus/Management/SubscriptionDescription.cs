using System;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class SubscriptionDescription
    {
        public SubscriptionDescription(string topicPath, string subscriptionName)
        {
            this.TopicPath = topicPath;
            this.SubscriptionName = subscriptionName;
        }

        public TimeSpan LockDuration { get; set; }

        public bool RequiresSession { get; set; }

        public TimeSpan DefaultMessageTimeToLive { get; set; }

        public TimeSpan AutoDeleteOnIdle { get; set; }

        public bool EnableDeadLetteringOnMessageExpiration { get; set; }

        public bool EnableDeadLetteringOnFilterEvaluationExceptions { get; set; }

        public string TopicPath { get; set; }

        public string SubscriptionName { get; set; }

        public int MaxDeliveryCount { get; set; }

        public EntityStatus Status { get; set; }

        public string ForwardTo { get; set; }

        public string ForwardDeadLetteredMessagesTo { get; set; }
    }
}
