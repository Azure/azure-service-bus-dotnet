// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Amqp
{
    using System;
    using System.Threading.Tasks;
    using Azure.Amqp;
    using Azure.Amqp.Framing;
    using Primitives;

    internal abstract class AmqpLinkCreator
    {
        readonly string entityPath;
        readonly ServiceBusConnection serviceBusConnection;
        readonly Uri endpointAddress;
        readonly string[] requiredClaims;
        readonly ICbsTokenProvider cbsTokenProvider;
        readonly AmqpLinkSettings amqpLinkSettings;

        protected AmqpLinkCreator(string entityPath, ServiceBusConnection serviceBusConnection, Uri endpointAddress, string[] requiredClaims, ICbsTokenProvider cbsTokenProvider, AmqpLinkSettings amqpLinkSettings, string clientId)
        {
            this.entityPath = entityPath;
            this.serviceBusConnection = serviceBusConnection;
            this.endpointAddress = endpointAddress;
            this.requiredClaims = requiredClaims;
            this.cbsTokenProvider = cbsTokenProvider;
            this.amqpLinkSettings = amqpLinkSettings;
            ClientId = clientId;
        }

        protected string ClientId { get; }

        public async Task<Tuple<AmqpObject, DateTime>> CreateAndOpenAmqpLinkAsync()
        {
            var timeoutHelper = new TimeoutHelper(serviceBusConnection.OperationTimeout);

            MessagingEventSource.Log.AmqpGetOrCreateConnectionStart();
            AmqpConnection connection = await serviceBusConnection.ConnectionManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
            MessagingEventSource.Log.AmqpGetOrCreateConnectionStop(entityPath, connection.ToString(), connection.State.ToString());

            // Authenticate over CBS
            var cbsLink = connection.Extensions.Find<AmqpCbsLink>();

            var resource = endpointAddress.AbsoluteUri;
            MessagingEventSource.Log.AmqpSendAuthenticanTokenStart(endpointAddress, resource, resource, requiredClaims);
            DateTime cbsTokenExpiresAtUtc = await cbsLink.SendTokenAsync(cbsTokenProvider, endpointAddress, resource, resource, requiredClaims, timeoutHelper.RemainingTime()).ConfigureAwait(false);
            MessagingEventSource.Log.AmqpSendAuthenticanTokenStop();

            AmqpSession session = null;
            try
            {
                // Create Session
                var sessionSettings = new AmqpSessionSettings { Properties = new Fields() };
                session = connection.CreateSession(sessionSettings);
                await session.OpenAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.AmqpSessionCreationException(entityPath, connection, exception);
                session?.Abort();
                throw AmqpExceptionHelper.GetClientException(exception, null, session.GetInnerException());
            }

            AmqpObject link = null;
            try
            {
                // Create Link
                link = OnCreateAmqpLink(connection, amqpLinkSettings, session);
                await link.OpenAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
                return new Tuple<AmqpObject, DateTime>(link, cbsTokenExpiresAtUtc);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.AmqpLinkCreationException(
                    entityPath,
                    session,
                    connection,
                    exception);

                throw AmqpExceptionHelper.GetClientException(exception, null, link?.GetInnerException(), session.IsClosing());
            }
        }

        protected abstract AmqpObject OnCreateAmqpLink(AmqpConnection connection, AmqpLinkSettings linkSettings, AmqpSession amqpSession);
    }
}