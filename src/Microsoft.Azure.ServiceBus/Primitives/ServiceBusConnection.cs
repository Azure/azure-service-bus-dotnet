// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Transport;
using Microsoft.Azure.ServiceBus.Amqp;

namespace Microsoft.Azure.ServiceBus.Primitives
{
    internal abstract class ServiceBusConnection
    {
        static readonly Version AmqpVersion = new Version(1, 0, 0, 0);

        protected ServiceBusConnection(TimeSpan operationTimeout, RetryPolicy retryPolicy)
        {
            OperationTimeout = operationTimeout;
            RetryPolicy = retryPolicy;
        }

        public Uri Endpoint { get; set; }

        /// <summary>
        ///     OperationTimeout is applied in erroneous situations to notify the caller about the relevant
        ///     <see cref="ServiceBusException" />
        /// </summary>
        public TimeSpan OperationTimeout { get; set; }

        /// <summary>
        ///     Get the retry policy instance that was created as part of this builder's creation.
        /// </summary>
        public RetryPolicy RetryPolicy { get; set; }

        /// <summary>
        ///     Get the shared access policy key value from the connection string
        /// </summary>
        /// <value>Shared Access Signature key</value>
        public string SasKey { get; set; }

        /// <summary>
        ///     Get the shared access policy owner name from the connection string
        /// </summary>
        public string SasKeyName { get; set; }

        internal FaultTolerantAmqpObject<AmqpConnection> ConnectionManager { get; set; }

        public Task CloseAsync()
        {
            return ConnectionManager.CloseAsync();
        }

        protected void InitializeConnection(ServiceBusConnectionStringBuilder builder)
        {
            Endpoint = builder.Endpoint;
            SasKeyName = builder.SasKeyName;
            SasKey = builder.SasKey;
            ConnectionManager = new FaultTolerantAmqpObject<AmqpConnection>(CreateConnectionAsync, CloseConnection);
        }

        static void CloseConnection(AmqpConnection connection)
        {
            MessagingEventSource.Log.AmqpConnectionClosed(connection);
            connection.SafeClose();
        }

        async Task<AmqpConnection> CreateConnectionAsync(TimeSpan timeout)
        {
            var hostName = Endpoint.Host;
            var networkHost = Endpoint.Host;
            var port = Endpoint.Port;

            var timeoutHelper = new TimeoutHelper(timeout);
            var amqpSettings = AmqpConnectionHelper.CreateAmqpSettings(
                AmqpVersion,
                true,
                true);

            var tpSettings = AmqpConnectionHelper.CreateTcpTransportSettings(
                networkHost,
                hostName,
                port,
                true);

            var initiator = new AmqpTransportInitiator(amqpSettings, tpSettings);
            var transport = await initiator.ConnectTaskAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);

            var containerId = Guid.NewGuid().ToString();
            var amqpConnectionSettings = AmqpConnectionHelper.CreateAmqpConnectionSettings(AmqpConstants.DefaultMaxFrameSize, containerId, hostName);
            var connection = new AmqpConnection(transport, amqpSettings, amqpConnectionSettings);
            await connection.OpenAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);

            // Always create the CBS Link + Session
            var cbsLink = new AmqpCbsLink(connection);
            if (connection.Extensions.Find<AmqpCbsLink>() == null)
            {
                connection.Extensions.Add(cbsLink);
            }

            MessagingEventSource.Log.AmqpConnectionCreated(hostName, connection);

            return connection;
        }
    }
}