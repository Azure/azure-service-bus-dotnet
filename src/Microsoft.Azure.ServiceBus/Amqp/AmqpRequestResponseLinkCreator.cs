// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.Amqp;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus.Amqp
{
    internal class AmqpRequestResponseLinkCreator : AmqpLinkCreator
    {
        readonly string entityPath;

        public AmqpRequestResponseLinkCreator(string entityPath, ServiceBusConnection serviceBusConnection, string[] requiredClaims, ICbsTokenProvider cbsTokenProvider, AmqpLinkSettings linkSettings)
            : base(entityPath, serviceBusConnection, requiredClaims, cbsTokenProvider, linkSettings)
        {
            this.entityPath = entityPath;
        }

        protected override AmqpObject OnCreateAmqpLink(AmqpConnection connection, AmqpLinkSettings linkSettings, AmqpSession amqpSession)
        {
            AmqpObject link = new RequestResponseAmqpLink(AmqpClientConstants.EntityTypeManagement, amqpSession, entityPath, linkSettings.Properties);
            linkSettings.LinkName = $"{connection.Settings.ContainerId};{connection.Identifier}:{amqpSession.Identifier}:{link.Identifier}";
            return link;
        }
    }
}