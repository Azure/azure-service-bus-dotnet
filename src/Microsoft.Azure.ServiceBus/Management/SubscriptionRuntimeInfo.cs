using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class SubscriptionRuntimeInfo
    {
        public MessageCountDetails MessageCountDetails { get; set; }

        public EntityAvailabilityStatus AvailabilityStatus { get; set; }

        public DateTime CreatedAt { get; internal set; }

        public DateTime UpdatedAt { get; internal set; }

        public DateTime AccessedAt { get; internal set; }
    }
}
