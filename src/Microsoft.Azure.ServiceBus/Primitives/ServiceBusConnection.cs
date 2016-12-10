// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.ServiceBus.Amqp;
    using Microsoft.Azure.ServiceBus.Primitives;

    public abstract class ServiceBusConnection
    {
        public static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromMinutes(1);
        static readonly Version AmqpVersion = new Version(1, 0, 0, 0);
        int prefetchCount;

        protected ServiceBusConnection(TimeSpan operationTimeout, RetryPolicy retryPolicy)
        {
            this.OperationTimeout = operationTimeout;
            this.RetryPolicy = retryPolicy;
        }

        public Uri Endpoint { get; set; }

        /// <summary>
        /// OperationTimeout is applied in erroneous situations to notify the caller about the relevant <see cref="ServiceBusException"/>
        /// </summary>
        public TimeSpan OperationTimeout { get; set; }

        /// <summary>
        /// Get the retry policy instance that was created as part of this builder's creation.
        /// </summary>
        public RetryPolicy RetryPolicy { get; set; }

        /// <summary>
        /// Get the shared access policy key value from the connection string
        /// </summary>
        /// <value>Shared Access Signature key</value>
        public string SasKey { get; set; }

        /// <summary>
        /// Get the shared access policy owner name from the connection string
        /// </summary>
        public string SasKeyName { get; set; }

        /// <summary>Gets or sets the number of messages that the message receiver can simultaneously request.</summary>
        /// <value>The number of messages that the message receiver can simultaneously request.</value>
        public int PrefetchCount
        {
            get
            {
                return this.prefetchCount;
            }

            set
            {
                if (value < 0)
                {
                    throw Fx.Exception.ArgumentOutOfRange(nameof(this.PrefetchCount), value, "Value must be greater than 0");
                }

                this.prefetchCount = value;
            }
        }

        internal FaultTolerantAmqpObject<AmqpConnection> ConnectionManager { get; set; }

        public QueueClient CreateQueueClient(ServiceBusEntityConnection entityConnection, ReceiveMode mode)
        {
            return new AmqpQueueClient(this, entityConnection.EntityPath, mode);
        }

        public QueueClient CreateQueueClient(ServiceBusNamespaceConnection namespaceConnection, string entityPath, ReceiveMode mode)
        {
            return new AmqpQueueClient(this, entityPath, mode);
        }

        public TopicClient CreateTopicClient(ServiceBusEntityConnection entityConnection)
        {
            return new AmqpTopicClient(this, entityConnection.EntityPath);
        }

        public TopicClient CreateTopicClient(ServiceBusNamespaceConnection namespaceConnection, string topicPath)
        {
            return new AmqpTopicClient(this, topicPath);
        }

        public SubscriptionClient CreateSubscriptionClient(ServiceBusEntityConnection entityConnection, string subscriptionName, ReceiveMode mode)
        {
            return new AmqpSubscriptionClient(this, entityConnection.EntityPath, subscriptionName, mode);
        }

        public SubscriptionClient CreateSubscriptionClient(ServiceBusNamespaceConnection namespaceConnection, string topicPath, string subscriptionName, ReceiveMode mode)
        {
            return new AmqpSubscriptionClient(this, topicPath, subscriptionName, mode);
        }

        public Task CloseAsync()
        {
            return this.ConnectionManager.CloseAsync();
        }

        protected void InitializeConnection(ServiceBusConnectionStringBuilder builder)
        {
            this.Endpoint = builder.Endpoint;
            this.SasKeyName = builder.SasKeyName;
            this.SasKey = builder.SasKey;
            this.ConnectionManager = new FaultTolerantAmqpObject<AmqpConnection>(this.CreateConnectionAsync, this.CloseConnection);
        }

        async Task<AmqpConnection> CreateConnectionAsync(TimeSpan timeout)
        {
            string hostName = this.Endpoint.Host;
            string networkHost = this.Endpoint.Host;
            int port = this.Endpoint.Port;

            var timeoutHelper = new TimeoutHelper(timeout);
            var amqpSettings = AmqpConnectionHelper.CreateAmqpSettings(
                amqpVersion: ServiceBusConnection.AmqpVersion,
                useSslStreamSecurity: true,
                hasTokenProvider: true);

            TransportSettings tpSettings = AmqpConnectionHelper.CreateTcpTransportSettings(
                networkHost: networkHost,
                hostName: hostName,
                port: port,
                useSslStreamSecurity: true);

            var initiator = new AmqpTransportInitiator(amqpSettings, tpSettings);
            var transport = await initiator.ConnectTaskAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);

            string containerId = Guid.NewGuid().ToString();
            var amqpConnectionSettings = AmqpConnectionHelper.CreateAmqpConnectionSettings(AmqpConstants.DefaultMaxFrameSize, containerId, hostName);
            var connection = new AmqpConnection(transport, amqpSettings, amqpConnectionSettings);
            await connection.OpenAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);

            // Always create the CBS Link + Session
            var cbsLink = new AmqpCbsLink(connection);
            if (connection.Extensions.Find<AmqpCbsLink>() == null)
            {
                connection.Extensions.Add(cbsLink);
            }

            return connection;
        }

        void CloseConnection(AmqpConnection connection)
        {
            connection.SafeClose();
        }
    }
}