using System;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class QueueRuntimeInfo
    {
        public long SizeInBytes { get; internal set; }

        public MessageCountDetails MessageCountDetails { get; internal set; }

        public EntityAvailabilityStatus AvailabilityStatus { get; internal set; }

        public DateTime CreatedAt { get; internal set; }

        public DateTime UpdatedAt { get; internal set; }

        public DateTime AccessedAt { get; internal set; }
    }
}
