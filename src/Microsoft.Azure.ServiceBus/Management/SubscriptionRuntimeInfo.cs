// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Management
{
    using System;

    public class SubscriptionRuntimeInfo
    {
        public SubscriptionRuntimeInfo(string topicPath, string subscriptionName)
        {
            this.TopicPath = topicPath;
            this.SubscriptionName = subscriptionName;
        }

        public string TopicPath { get; internal set; }

        public string SubscriptionName { get; internal set; }

        public long MessageCount { get; internal set; }

        public MessageCountDetails MessageCountDetails { get; set; }

        public DateTime AccessedAt { get; internal set; }

        public DateTime CreatedAt { get; internal set; }

        public DateTime UpdatedAt { get; internal set; }
    }
}
