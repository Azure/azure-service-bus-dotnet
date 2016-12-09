// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;
    using Microsoft.Azure.ServiceBus.Primitives;

    public class PartitionedTopicClientTests : TopicClientTestBase
    {
        public PartitionedTopicClientTests(ITestOutputHelper output)
            : base(output)
        {
            this.ConnectionString = Environment.GetEnvironmentVariable("PARTITIONEDTOPICCLIENTCONNECTIONSTRING");

            this.ConnectionString =
                "Endpoint=sb://newvinsu1028.servicebus.windows.net/;EntityPath=partTopic;SharedAccessKeyName=saspolicy1;SharedAccessKey=5gcHr9YydwgPcKgSXZsq7126RfLOow2elrQCfve7cDE=";

            this.SubscriptionName = Environment.GetEnvironmentVariable("SUBSCRIPTIONNAME");
            this.SubscriptionName = "nonsessionsub1";

            if (string.IsNullOrWhiteSpace(this.ConnectionString))
            {
                throw new InvalidOperationException("TOPICCLIENTCONNECTIONSTRING environment variable was not found!");
            }

            if (string.IsNullOrWhiteSpace(this.SubscriptionName))
            {
                throw new InvalidOperationException("SUBSCRIPTIONNAME environment variable was not found!");
            }
        }

        [Fact]
        async Task PeekLockTest()
        {
            await this.TopicClientPeekLockTestCase(messageCount: 10);
        }

        [Fact]
        async Task ReceiveDeleteTest()
        {
            await this.TopicClientReceiveDeleteTestCase(messageCount: 10);
        }

        [Fact]
        async Task PeekLockWithAbandonTest()
        {
            await this.TopicClientPeekLockWithAbandonTestCase(messageCount: 10);
        }

        [Fact]
        async Task PeekLockWithDeadLetterTest()
        {
            await this.TopicClientPeekLockWithDeadLetterTestCase(messageCount: 10);
        }

        [Fact]
        async Task PeekLockDeferTest()
        {
            await this.TopicClientPeekLockDeferTestCase(messageCount: 10);
        }

        // Request Response Tests
        [Fact]
        async Task BasicRenewLockTest()
        {
            await this.TopicClientRenewLockTestCase(messageCount: 1);
        }
    }
}
