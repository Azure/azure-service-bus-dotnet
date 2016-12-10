// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class PartitionedTopicSessionTests : TopicSessionTestBase
    {
        public PartitionedTopicSessionTests(ITestOutputHelper output)
            : base(output)
        {
            this.ConnectionString = Environment.GetEnvironmentVariable("PARTITIONEDTOPICCLIENTCONNECTIONSTRING");
            this.SubscriptionName = Environment.GetEnvironmentVariable("SESSIONFULSUBSCRIPTIONNAME");

            this.ConnectionString =
                "Endpoint=sb://newvinsu1028.servicebus.windows.net/;EntityPath=partSessionTopic;SharedAccessKeyName=saspolicy1;SharedAccessKey=xeyuC1Uw1+uzTD7JwJ+6jxhKF7hYvC0j6p+2eHNEqOE=";

            this.SubscriptionName = "sessionsub1";

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
        async Task SessionTest()
        {
            await this.SessionTestCase();
        }

        [Fact]
        async Task GetAndSetSessionStateTest()
        {
            await this.GetAndSetSessionStateTestCase();
        }

        [Fact]
        async Task SessionRenewLockTest()
        {
            await this.SessionRenewLockTestCase();
        }
    }
}