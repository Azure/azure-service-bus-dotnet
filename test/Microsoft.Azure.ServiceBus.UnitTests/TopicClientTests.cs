// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus.Primitives;
    using Xunit;
    using Xunit.Abstractions;

    public class TopicClientTests : SenderReceiverClientTestBase
    {
        public TopicClientTests(ITestOutputHelper output)
            : base(output)
        {
        }

        public static IEnumerable<object[]> EnvironmentVariablesData => new[]
        {
            new object[] { "NONPARTITIONEDTOPICCLIENTCONNECTIONSTRING", "SUBSCRIPTIONNAME" },
            new object[] { "PARTITIONEDTOPICCLIENTCONNECTIONSTRING", "SUBSCRIPTIONNAME" }
        };

        protected string ConnectionString { get; set; }

        protected string SubscriptionName { get; set; }

        [Theory]
        [MemberData("EnvironmentVariablesData")]
        public async Task PeekLockTest(string connectionStringEnvVar, string subscriptionEnvVar, int messageCount = 10)
        {
            this.AssignConnectionStringAndSubscription(connectionStringEnvVar, subscriptionEnvVar);

            var topicClient = TopicClient.CreateFromConnectionString(this.ConnectionString);
            var subscriptionClient = SubscriptionClient.CreateFromConnectionString(this.ConnectionString, this.SubscriptionName);
            try
            {
                await this.PeekLockTestCase(topicClient.InnerSender, subscriptionClient.InnerReceiver, messageCount);
            }
            finally
            {
                await subscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData("EnvironmentVariablesData")]
        public async Task TopicClientReceiveDeleteTestCase(string connectionStringEnvVar, string subscriptionEnvVar, int messageCount = 10)
        {
            this.AssignConnectionStringAndSubscription(connectionStringEnvVar, subscriptionEnvVar);

            var topicClient = TopicClient.CreateFromConnectionString(this.ConnectionString);
            var subscriptionClient = SubscriptionClient.CreateFromConnectionString(this.ConnectionString, this.SubscriptionName, ReceiveMode.ReceiveAndDelete);
            try
            {
                await this.ReceiveDeleteTestCase(topicClient.InnerSender, subscriptionClient.InnerReceiver, messageCount);
            }
            finally
            {
                await subscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData("EnvironmentVariablesData")]
        public async Task TopicClientPeekLockWithAbandonTestCase(string connectionStringEnvVar, string subscriptionEnvVar, int messageCount = 10)
        {
            this.AssignConnectionStringAndSubscription(connectionStringEnvVar, subscriptionEnvVar);

            var topicClient = TopicClient.CreateFromConnectionString(this.ConnectionString);
            var subscriptionClient = SubscriptionClient.CreateFromConnectionString(this.ConnectionString, this.SubscriptionName);
            try
            {
                await this.PeekLockWithAbandonTestCase(topicClient.InnerSender, subscriptionClient.InnerReceiver, messageCount);
            }
            finally
            {
                await subscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData("EnvironmentVariablesData")]
        public async Task TopicClientPeekLockWithDeadLetterTestCase(string connectionStringEnvVar, string subscriptionEnvVar, int messageCount = 10)
        {
            this.AssignConnectionStringAndSubscription(connectionStringEnvVar, subscriptionEnvVar);

            var topicClient = TopicClient.CreateFromConnectionString(this.ConnectionString);
            var subscriptionClient = SubscriptionClient.CreateFromConnectionString(this.ConnectionString, this.SubscriptionName);

            // Create DLQ Client To Receive DeadLetteredMessages
            ServiceBusConnectionStringBuilder builder = new ServiceBusConnectionStringBuilder(this.ConnectionString);
            string subscriptionDeadletterPath = EntityNameHelper.FormatDeadLetterPath(this.SubscriptionName);
            SubscriptionClient deadLetterSubscriptionClient = SubscriptionClient.CreateFromConnectionString(this.ConnectionString, subscriptionDeadletterPath);

            try
            {
                await this.PeekLockWithDeadLetterTestCase(topicClient.InnerSender, subscriptionClient.InnerReceiver, deadLetterSubscriptionClient.InnerReceiver, messageCount);
            }
            finally
            {
                await deadLetterSubscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData("EnvironmentVariablesData")]
        public async Task TopicClientPeekLockDeferTestCase(string connectionStringEnvVar, string subscriptionEnvVar, int messageCount = 10)
        {
            this.AssignConnectionStringAndSubscription(connectionStringEnvVar, subscriptionEnvVar);

            var topicClient = TopicClient.CreateFromConnectionString(this.ConnectionString);
            var subscriptionClient = SubscriptionClient.CreateFromConnectionString(this.ConnectionString, this.SubscriptionName);
            try
            {
                await this.PeekLockDeferTestCase(topicClient.InnerSender, subscriptionClient.InnerReceiver, messageCount);
            }
            finally
            {
                await subscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
            }
        }

        [Theory]
        [MemberData("EnvironmentVariablesData")]
        public async Task TopicClientRenewLockTestCase(string connectionStringEnvVar, string subscriptionEnvVar, int messageCount = 1)
        {
            this.AssignConnectionStringAndSubscription(connectionStringEnvVar, subscriptionEnvVar);

            var topicClient = TopicClient.CreateFromConnectionString(this.ConnectionString);
            var subscriptionClient = SubscriptionClient.CreateFromConnectionString(this.ConnectionString, this.SubscriptionName);
            try
            {
                await this.RenewLockTestCase(topicClient.InnerSender, subscriptionClient.InnerReceiver, messageCount);
            }
            finally
            {
                await subscriptionClient.CloseAsync();
                await topicClient.CloseAsync();
            }
        }

        private void AssignConnectionStringAndSubscription(string connectionStringEnvVar, string subscriptionEnvVar)
        {
            this.ConnectionString = Environment.GetEnvironmentVariable(connectionStringEnvVar);
            this.SubscriptionName = Environment.GetEnvironmentVariable(subscriptionEnvVar);

            if (string.IsNullOrWhiteSpace(this.ConnectionString))
            {
                throw new InvalidOperationException($"'{connectionStringEnvVar}' environment variable was not found!");
            }

            if (string.IsNullOrWhiteSpace(this.SubscriptionName))
            {
                throw new InvalidOperationException($"'{connectionStringEnvVar}' environment variable was not found!");
            }
        }
    }
}