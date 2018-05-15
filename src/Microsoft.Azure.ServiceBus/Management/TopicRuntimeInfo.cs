using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class TopicRuntimeInfo
    {
        public long SizeInBytes { get; internal set; }

        public EntityAvailabilityStatus AvailabilityStatus { get; internal set; }

        public DateTime CreatedAt { get; internal set; }

        public DateTime UpdatedAt { get; internal set; }

        public DateTime AccessedAt { get; internal set; }

        public int SubscriptionCount { get; internal set; }
    }
}
