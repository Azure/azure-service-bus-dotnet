using System;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class TopicInfo
    {
        public long SizeInBytes { get; internal set; }

        public DateTime CreatedAt { get; internal set; }

        public DateTime UpdatedAt { get; internal set; }

        public DateTime AccessedAt { get; internal set; }

        public int SubscriptionCount { get; internal set; }

        public TopicDescription TopicDescription { get; set; }
    }
}
