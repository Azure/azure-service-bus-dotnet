using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class QueueDescription
    {
        public QueueDescription(string path)
        {

        }

        public string Path { get; set; }

        public TimeSpan LockDuration { get; set; }

        public long MaxSizeInMegabytes { get; set; }

        public bool RequiresDuplicateDetection { get; set; }

        public bool RequiresSession { get; set; }

        public TimeSpan DefaultMessageTimeToLive { get; set; }

        public TimeSpan AutoDeleteOnIdle { get; set; }

        public bool EnableDeadLetteringOnMessageExpiration { get; set; }

        public TimeSpan DuplicateDetectionHistoryTimeWindow { get; set; }

        public int MaxDeliveryCount { get; set; }

        public AuthorizationRules AuthorizationRules { get; set; }

        public EntityStatus Status { get; set; }

        public string ForwardTo { get; set; }

        public string ForwardDeadLetteredMessagesTo { get; set; }

        public DateTime CreatedAt { get; internal set; }

        public DateTime UpdatedAt { get; internal set; }

        public DateTime AccessedAt { get; internal set; }

        public bool EnablePartitioning { get; set; }
    }
}
