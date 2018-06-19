// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Management
{
    using System;
    using Microsoft.Azure.ServiceBus.Primitives;

    public class SubscriptionDescription : IEquatable<SubscriptionDescription>
    {
        string topicPath, subscriptionName;
        TimeSpan lockDuration = TimeSpan.FromSeconds(60);
        TimeSpan defaultMessageTimeToLive = TimeSpan.MaxValue;
        TimeSpan autoDeleteOnIdle = TimeSpan.MaxValue;
        TimeSpan duplicateDetectionHistoryTimeWindow = TimeSpan.FromSeconds(30);
        int maxDeliveryCount = 10;
        string forwardTo = null;
        string forwardDeadLetteredMessagesTo = null;

        public SubscriptionDescription(string topicPath, string subscriptionName)
        {
            this.TopicPath = topicPath;
            this.SubscriptionName = subscriptionName;
        }

        public TimeSpan LockDuration
        {
            get => this.lockDuration;
            set
            {
                TimeoutHelper.ThrowIfNonPositiveArgument(value, nameof(LockDuration));
                this.lockDuration = value;
            }
        }

        public bool RequiresSession { get; set; } = false;

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

        public bool EnableDeadLetteringOnMessageExpiration { get; set; } = false;

        public bool EnableDeadLetteringOnFilterEvaluationExceptions { get; set; } = true;

        public string TopicPath
        {
            get => this.topicPath;
            set
            {
                EntityNameHelper.CheckValidTopicName(value, nameof(TopicPath));
                this.topicPath = value;
            }
        }

        public string SubscriptionName
        {
            get => this.subscriptionName;
            set
            {
                EntityNameHelper.CheckValidSubscriptionName(value, nameof(SubscriptionName));
                this.subscriptionName = value;
            }
        }

        public int MaxDeliveryCount
        {
            get => this.maxDeliveryCount;
            set
            {
                if (value < ManagementClientConstants.MinAllowedMaxDeliveryCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaxDeliveryCount),
                        $"The value must be greater than {ManagementClientConstants.MinAllowedMaxDeliveryCount}");
                }

                this.maxDeliveryCount = value;
            }
        }

        public EntityStatus Status { get; set; } = EntityStatus.Active;

        public string ForwardTo
        {
            get => this.forwardTo;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    this.forwardTo = value;
                    return;
                }

                EntityNameHelper.CheckValidQueueName(value, nameof(ForwardTo));
                if (this.topicPath.Equals(value, StringComparison.CurrentCultureIgnoreCase))
                {
                    throw new InvalidOperationException("Entity cannot have auto-forwarding policy to itself");
                }

                this.forwardTo = value;
            }
        }

        public string ForwardDeadLetteredMessagesTo
        {
            get => this.forwardDeadLetteredMessagesTo;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    this.forwardDeadLetteredMessagesTo = value;
                    return;
                }

                EntityNameHelper.CheckValidQueueName(value, nameof(ForwardDeadLetteredMessagesTo));
                if (this.topicPath.Equals(value, StringComparison.CurrentCultureIgnoreCase))
                {
                    throw new InvalidOperationException("Entity cannot have auto-forwarding policy to itself");
                }

                this.forwardDeadLetteredMessagesTo = value;
            }
        }

        public bool Equals(SubscriptionDescription otherDescription)
        {
            if (otherDescription is SubscriptionDescription other && this.SubscriptionName.Equals(other.SubscriptionName, StringComparison.OrdinalIgnoreCase)
                && this.TopicPath.Equals(other.TopicPath, StringComparison.OrdinalIgnoreCase)
                && this.AutoDeleteOnIdle.Equals(other.AutoDeleteOnIdle)
                && this.DefaultMessageTimeToLive.Equals(other.DefaultMessageTimeToLive)
                && this.EnableDeadLetteringOnMessageExpiration == other.EnableDeadLetteringOnMessageExpiration
                && this.EnableDeadLetteringOnFilterEvaluationExceptions == other.EnableDeadLetteringOnFilterEvaluationExceptions
                && string.Equals(this.ForwardDeadLetteredMessagesTo, other.ForwardDeadLetteredMessagesTo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(this.ForwardTo, other.ForwardTo, StringComparison.OrdinalIgnoreCase)
                && this.LockDuration.Equals(other.LockDuration)
                && this.MaxDeliveryCount == other.MaxDeliveryCount
                && this.RequiresSession.Equals(other.RequiresSession)
                && this.Status.Equals(other.Status))
            {
                return true;
            }

            return false;
        }
    }
}
