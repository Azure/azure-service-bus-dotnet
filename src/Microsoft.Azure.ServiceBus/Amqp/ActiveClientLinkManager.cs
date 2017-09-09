// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Amqp
{
    using Azure.Amqp;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    sealed class ActiveClientLinkManager
    {
        static readonly TimeSpan SendTokenTimeout = TimeSpan.FromMinutes(1);
        static readonly TimeSpan TokenRefreshBuffer = TimeSpan.FromSeconds(10);

        readonly string clientId;
        readonly ICbsTokenProvider cbsTokenProvider;
        Timer sendReceiveLinkCBSTokenRenewalTimer;
        Timer requestResponseLinkCBSTokenRenewalTimer;

        ActiveSendReceiveClientLink activeSendReceiveClientLink;
        ActiveRequestResponseLink activeRequestResponseClientLink;

        public ActiveClientLinkManager(string clientId, ICbsTokenProvider tokenProvider)
        {
            this.clientId = clientId;
            cbsTokenProvider = tokenProvider;
            sendReceiveLinkCBSTokenRenewalTimer = new Timer(OnRenewSendReceiveCBSToken, this, Timeout.Infinite, Timeout.Infinite);
            requestResponseLinkCBSTokenRenewalTimer = new Timer(OnRenewRequestResponseCBSToken, this, Timeout.Infinite, Timeout.Infinite);
        }

        public void Close()
        {
            sendReceiveLinkCBSTokenRenewalTimer.Dispose();
            sendReceiveLinkCBSTokenRenewalTimer = null;
            requestResponseLinkCBSTokenRenewalTimer.Dispose();
            requestResponseLinkCBSTokenRenewalTimer = null;
        }

        public void SetActiveSendReceiveLink(ActiveSendReceiveClientLink sendReceiveClientLink)
        {
            activeSendReceiveClientLink = sendReceiveClientLink;
            activeSendReceiveClientLink.Link.Closed += OnSendReceiveLinkClosed;
            if (activeSendReceiveClientLink.Link.State == AmqpObjectState.Opened)
            {
                SetRenewCBSTokenTimer(sendReceiveClientLink);
            }
        }

        void OnSendReceiveLinkClosed(object sender, EventArgs e)
        {
            ChangeRenewTimer(activeSendReceiveClientLink, Timeout.InfiniteTimeSpan);
        }

        public void SetActiveRequestResponseLink(ActiveRequestResponseLink requestResponseLink)
        {
            activeRequestResponseClientLink = requestResponseLink;
            activeRequestResponseClientLink.Link.Closed += OnRequestResponseLinkClosed;
            if (activeRequestResponseClientLink.Link.State == AmqpObjectState.Opened)
            {
                SetRenewCBSTokenTimer(requestResponseLink);
            }
        }

        static async void OnRenewSendReceiveCBSToken(object state)
        {
            var thisPtr = (ActiveClientLinkManager)state;
            await thisPtr.RenewCBSTokenAsync(thisPtr.activeSendReceiveClientLink).ConfigureAwait(false);
        }

        static async void OnRenewRequestResponseCBSToken(object state)
        {
            var thisPtr = (ActiveClientLinkManager)state;
            await thisPtr.RenewCBSTokenAsync(thisPtr.activeRequestResponseClientLink).ConfigureAwait(false);
        }

        async Task RenewCBSTokenAsync(ActiveClientLinkObject activeClientLinkObject)
        {
            try
            {
                var cbsLink = activeClientLinkObject.Connection.Extensions.Find<AmqpCbsLink>() ?? new AmqpCbsLink(activeClientLinkObject.Connection);

                MessagingEventSource.Log.AmqpSendAuthenticanTokenStart(activeClientLinkObject.EndpointUri, activeClientLinkObject.Audience, activeClientLinkObject.Audience, activeClientLinkObject.RequiredClaims);

                activeClientLinkObject.AuthorizationValidUntilUtc = await cbsLink.SendTokenAsync(
                    cbsTokenProvider,
                    activeClientLinkObject.EndpointUri,
                    activeClientLinkObject.Audience,
                    activeClientLinkObject.Audience,
                    activeClientLinkObject.RequiredClaims,
                    SendTokenTimeout).ConfigureAwait(false);

                SetRenewCBSTokenTimer(activeClientLinkObject);

                MessagingEventSource.Log.AmqpSendAuthenticanTokenStop();
            }
            catch (Exception e)
            {
                // failed to refresh token, no need to do anything since the server will shut the link itself
                MessagingEventSource.Log.AmqpSendAuthenticanTokenException(clientId, e);

                ChangeRenewTimer(activeClientLinkObject, Timeout.InfiniteTimeSpan);
            }
        }

        void OnRequestResponseLinkClosed(object sender, EventArgs e)
        {
            ChangeRenewTimer(activeRequestResponseClientLink, Timeout.InfiniteTimeSpan);
        }

        void SetRenewCBSTokenTimer(ActiveClientLinkObject activeClientLinkObject)
        {
            if (activeClientLinkObject.AuthorizationValidUntilUtc < DateTime.UtcNow)
            {
                return;
            }

            TimeSpan interval = activeClientLinkObject.AuthorizationValidUntilUtc.Subtract(DateTime.UtcNow) - TokenRefreshBuffer;
            ChangeRenewTimer(activeClientLinkObject, interval);
        }

        void ChangeRenewTimer(ActiveClientLinkObject activeClientLinkObject, TimeSpan dueTime)
        {
            if (activeClientLinkObject is ActiveSendReceiveClientLink)
            {
                sendReceiveLinkCBSTokenRenewalTimer?.Change(dueTime, Timeout.InfiniteTimeSpan);
            }
            else
            {
                requestResponseLinkCBSTokenRenewalTimer?.Change(dueTime, Timeout.InfiniteTimeSpan);
            }
        }
    }
}
