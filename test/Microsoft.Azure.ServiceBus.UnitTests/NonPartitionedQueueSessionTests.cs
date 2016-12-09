// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class NonPartitionedQueueSessionTests : QueueSessionTestBase
    {
        public NonPartitionedQueueSessionTests(ITestOutputHelper output)
            : base(output)
        {
            ConnectionString = Environment.GetEnvironmentVariable("NONPARTITIONEDSESSIONQUEUECONNECTIONSTRING");

            ConnectionString =
                "Endpoint=sb://newvinsu1028.servicebus.windows.net/;EntityPath=nonpartsessionq;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=7oMVG2as0pelFCFujgSb2JExro7/tZ6oIGcECpljubc=";

            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                throw new InvalidOperationException("SESSIONQUEUECLIENTCONNECTIONSTRING environment variable was not found!");
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
