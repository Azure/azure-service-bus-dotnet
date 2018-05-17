using System;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class TopicDescription
    {
        public TopicDescription(string path)
        {
            this.Path = path;
        }

        public TimeSpan DefaultMessageTimeToLive { get; set; }

        public TimeSpan AutoDeleteOnIdle { get; set; }

        public long MaxSizeInMegabytes { get; set; }

        public bool RequiresDuplicateDetection { get; set; }

        public TimeSpan DuplicateDetectionHistoryTimeWindow { get; set; }

        public string Path { get; set; }

        public AuthorizationRules Authorization { get; set; }

        public EntityStatus Status { get; set; }

        public bool EnablePartitioning { get; set; }

        public TopicRuntimeInfo TopicRuntimeInfo { get; internal set; }
    }
}
