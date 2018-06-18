using System;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class TopicDescription : IEquatable<TopicDescription>
    {
        string path;
        TimeSpan defaultMessageTimeToLive = TimeSpan.MaxValue;
        TimeSpan autoDeleteOnIdle = TimeSpan.MaxValue;
        TimeSpan duplicateDetectionHistoryTimeWindow = TimeSpan.FromSeconds(30);
        AuthorizationRules authorizationRules = null;

        public TopicDescription(string path)
        {
            this.Path = path;
        }

        public TimeSpan DefaultMessageTimeToLive
        {
            get => this.defaultMessageTimeToLive;
            set
            {
                if (value < ManagementClientConstants.MinimumAllowedTimeToLive || value > ManagementClientConstants.MaximumAllowedTimeToLive)
                {
                    throw new ArgumentOutOfRangeException(nameof(DefaultMessageTimeToLive),
                        $"The value must be between {ManagementClientConstants.MinimumAllowedTimeToLive} and {ManagementClientConstants.MaximumAllowedTimeToLive}");
                }

                this.defaultMessageTimeToLive = value;
            }
        }

        public TimeSpan AutoDeleteOnIdle
        {
            get => this.autoDeleteOnIdle;
            set
            {
                if (value < ManagementClientConstants.MinimumAllowedAutoDeleteOnIdle)
                {
                    throw new ArgumentOutOfRangeException(nameof(AutoDeleteOnIdle),
                        $"The value must be greater than {ManagementClientConstants.MinimumAllowedAutoDeleteOnIdle}");
                }

                this.autoDeleteOnIdle = value;
            }
        }

        public long MaxSizeInMB { get; set; } = 1024;

        public bool RequiresDuplicateDetection { get; set; } = false;

        public TimeSpan DuplicateDetectionHistoryTimeWindow
        {
            get => this.duplicateDetectionHistoryTimeWindow;
            set
            {
                if (value < ManagementClientConstants.MinimumDuplicateDetectionHistoryTimeWindow || value > ManagementClientConstants.MaximumDuplicateDetectionHistoryTimeWindow)
                {
                    throw new ArgumentOutOfRangeException(nameof(DuplicateDetectionHistoryTimeWindow),
                        $"The value must be between {ManagementClientConstants.MinimumDuplicateDetectionHistoryTimeWindow} and {ManagementClientConstants.MaximumDuplicateDetectionHistoryTimeWindow}");
                }

                this.duplicateDetectionHistoryTimeWindow = value;
            }
        }

        public string Path
        {
            get => this.path;
            set
            {
                EntityNameHelper.CheckValidTopicName(value, nameof(Path));
                this.path = value;
            }
        }

        public AuthorizationRules AuthorizationRules
        {
            get
            {
                if (this.authorizationRules == null)
                {
                    this.authorizationRules = new AuthorizationRules();
                }

                return this.authorizationRules;
            }
            internal set
            {
                this.authorizationRules = value;
            }
        }

        public EntityStatus Status { get; set; } = EntityStatus.Active;

        public bool EnablePartitioning { get; set; } = false;

        public bool SupportOrdering { get; set; } = false;

        public bool EnableBatchedOperations { get; set; } = true;

        public bool Equals(TopicDescription otherDescription)
        {
            if (otherDescription is TopicDescription other && this.Path.Equals(other.Path, StringComparison.OrdinalIgnoreCase)
                && this.AutoDeleteOnIdle.Equals(other.AutoDeleteOnIdle)
                && this.DefaultMessageTimeToLive.Equals(other.DefaultMessageTimeToLive)
                && this.DuplicateDetectionHistoryTimeWindow.Equals(other.DuplicateDetectionHistoryTimeWindow)
                && this.EnableBatchedOperations == other.EnableBatchedOperations
                && this.EnablePartitioning == other.EnablePartitioning
                && this.MaxSizeInMB == other.MaxSizeInMB
                && this.RequiresDuplicateDetection.Equals(other.RequiresDuplicateDetection)
                && this.Status.Equals(other.Status)
                && (this.authorizationRules != null && other.authorizationRules != null
                    || this.authorizationRules == null && other.authorizationRules == null)
                && this.authorizationRules != null && this.AuthorizationRules.Equals(other.AuthorizationRules))
            {
                return true;
            }

            return false;
        }
    }
}
