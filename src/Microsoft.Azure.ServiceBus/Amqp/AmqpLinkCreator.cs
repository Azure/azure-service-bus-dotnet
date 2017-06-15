// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus.Amqp
{
    internal abstract class AmqpLinkCreator
    {
        readonly AmqpLinkSettings amqpLinkSettings;
        readonly ICbsTokenProvider cbsTokenProvider;
        readonly string entityPath;
        readonly string[] requiredClaims;
        readonly ServiceBusConnection serviceBusConnection;

        protected AmqpLinkCreator(string entityPath, ServiceBusConnection serviceBusConnection, string[] requiredClaims, ICbsTokenProvider cbsTokenProvider, AmqpLinkSettings amqpLinkSettings)
        {
            this.entityPath = entityPath;
            this.serviceBusConnection = serviceBusConnection;
            this.requiredClaims = requiredClaims;
            this.cbsTokenProvider = cbsTokenProvider;
            this.amqpLinkSettings = amqpLinkSettings;
        }

        public async Task<AmqpObject> CreateAndOpenAmqpLinkAsync()
        {
            var timeoutHelper = new TimeoutHelper(serviceBusConnection.OperationTimeout);

            MessagingEventSource.Log.AmqpGetOrCreateConnectionStart();
            var connection = await serviceBusConnection.ConnectionManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
            MessagingEventSource.Log.AmqpGetOrCreateConnectionStop(entityPath, connection.ToString(), connection.State.ToString());

            // Authenticate over CBS
            var cbsLink = connection.Extensions.Find<AmqpCbsLink>();
            var address = new Uri(serviceBusConnection.Endpoint, entityPath);
            var audience = address.AbsoluteUri;
            var resource = address.AbsoluteUri;

            MessagingEventSource.Log.AmqpSendAuthenticanTokenStart(address, audience, resource, requiredClaims);
            await cbsLink.SendTokenAsync(cbsTokenProvider, address, audience, resource, requiredClaims, timeoutHelper.RemainingTime()).ConfigureAwait(false);
            MessagingEventSource.Log.AmqpSendAuthenticanTokenStop();

            AmqpSession session = null;
            try
            {
                // Create Session
                var sessionSettings = new AmqpSessionSettings
                {
                    Properties = new Fields()
                };
                session = connection.CreateSession(sessionSettings);
                await session.OpenAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.AmqpSessionCreationException(entityPath, connection, exception);
                session?.Abort();
                throw;
            }

            try
            {
                // Create Link
                var link = OnCreateAmqpLink(connection, amqpLinkSettings, session);
                await link.OpenAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
                return link;
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.AmqpLinkCreationException(
                    entityPath,
                    session,
                    connection,
                    exception);

                throw;
            }
        }

        protected abstract AmqpObject OnCreateAmqpLink(AmqpConnection connection, AmqpLinkSettings linkSettings, AmqpSession amqpSession);
    }
}