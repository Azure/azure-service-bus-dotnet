// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Messaging.Amqp
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Amqp;

    public class AmqpLinkHelper
    {
        internal static async Task<AmqpObject> CreateAndOpenAmqpLinkAsync(AmqpQueueClient amqpQueueClient, string entityPath, string[] requiredClaims, AmqpLinkSettings linkSettings, bool isRequestResponseLink)
        {
            var connectionSettings = amqpQueueClient.ConnectionSettings;
            var timeoutHelper = new TimeoutHelper(connectionSettings.OperationTimeout);
            AmqpConnection connection = await amqpQueueClient.ConnectionManager.GetOrCreateAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);

            // Authenticate over CBS
            var cbsLink = connection.Extensions.Find<AmqpCbsLink>();
            ICbsTokenProvider cbsTokenProvider = amqpQueueClient.CbsTokenProvider;
            Uri address = new Uri(connectionSettings.Endpoint, entityPath);
            string audience = address.AbsoluteUri;
            string resource = address.AbsoluteUri;
            await cbsLink.SendTokenAsync(cbsTokenProvider, address, audience, resource, requiredClaims, timeoutHelper.RemainingTime()).ConfigureAwait(false);

            AmqpSession session = null;
            try
            {
                // Create our Session
                var sessionSettings = new AmqpSessionSettings { Properties = new Fields() };
                session = connection.CreateSession(sessionSettings);
                await session.OpenAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);

                // Create our Link
                AmqpObject link;
                if (isRequestResponseLink)
                {
                    link = (AmqpObject) new RequestResponseAmqpLink(AmqpClientConstants.EntityTypeManagement, session, entityPath, linkSettings.Properties);
                }
                else
                {
                    link = (linkSettings.IsReceiver()) ? (AmqpObject) new ReceivingAmqpLink(linkSettings) : (AmqpObject) new SendingAmqpLink(linkSettings);
                }

                linkSettings.LinkName = $"{amqpQueueClient.ContainerId};{connection.Identifier}:{session.Identifier}:{link.Identifier}";
                if (!isRequestResponseLink)
                {
                    ((AmqpLink)link).AttachTo(session);
                }

                await link.OpenAsync(timeoutHelper.RemainingTime()).ConfigureAwait(false);
                return link;
            }
            catch (Exception)
            {
                session?.Abort();
                throw;
            }
        }

        public static bool IsReceiver(Attach attach)
        {
            return attach.Role.HasValue && attach.Role.Value;
        }
    }
}
