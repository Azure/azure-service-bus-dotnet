// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    static class Constants
    {
        internal const int MaxAttemptsCount = 5;

        internal const string ConnectionStringEnvironmentVariable = "SERVICEBUSCONNECTIONSTRING";

        internal const string PartitionedQueueName = "partitionedqueue";
        internal const string NonPartitionedQueueName = "nonpartitionedqueue";

        internal const string SessionPartitionedQueueName = "partitionedsessionqueue";
        internal const string SessionNonPartitionedQueueName = "nonpartitionedsessionqueue";

        internal const string PartitionedTopicName = "partitionedtopic";
        internal const string NonPartitionedTopicName = "nonpartitionedtopic";

        internal const string SubscriptionName = "subscription";
        internal const string SessionSubscriptionName = "sessionsubscription";
    }
}