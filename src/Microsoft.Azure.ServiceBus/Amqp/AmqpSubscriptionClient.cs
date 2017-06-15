// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Filters;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus.Amqp
{
    internal sealed class AmqpSubscriptionClient : IInnerSubscriptionClient
    {
        readonly object syncLock;
        MessageReceiver innerReceiver;
        int prefetchCount;

        public AmqpSubscriptionClient(
            string path,
            ServiceBusConnection servicebusConnection,
            RetryPolicy retryPolicy,
            ICbsTokenProvider cbsTokenProvider,
            int prefetchCount = 0,
            ReceiveMode mode = ReceiveMode.ReceiveAndDelete)
        {
            syncLock = new object();
            Path = path;
            ServiceBusConnection = servicebusConnection;
            RetryPolicy = retryPolicy;
            CbsTokenProvider = cbsTokenProvider;
            PrefetchCount = prefetchCount;
            ReceiveMode = mode;
        }

        ServiceBusConnection ServiceBusConnection { get; }

        RetryPolicy RetryPolicy { get; }

        ICbsTokenProvider CbsTokenProvider { get; }

        ReceiveMode ReceiveMode { get; }

        string Path { get; }

        public MessageReceiver InnerReceiver
        {
            get
            {
                if (innerReceiver == null)
                    lock (syncLock)
                    {
                        if (innerReceiver == null)
                            innerReceiver = new MessageReceiver(
                                Path,
                                MessagingEntityType.Subscriber,
                                ReceiveMode,
                                ServiceBusConnection,
                                CbsTokenProvider,
                                RetryPolicy,
                                PrefetchCount);
                    }

                return innerReceiver;
            }
        }

        /// <summary>
        ///     Gets or sets the number of messages that the subscription client can simultaneously request.
        /// </summary>
        /// <value>The number of messages that the subscription client can simultaneously request.</value>
        public int PrefetchCount
        {
            get => prefetchCount;
            set
            {
                if (value < 0)
                    throw Fx.Exception.ArgumentOutOfRange(nameof(PrefetchCount), value, "Value cannot be less than 0.");
                prefetchCount = value;
                if (innerReceiver != null)
                    innerReceiver.PrefetchCount = value;
            }
        }

        public Task CloseAsync()
        {
            return innerReceiver?.CloseAsync();
        }

        public async Task OnAddRuleAsync(RuleDescription description)
        {
            try
            {
                var amqpRequestMessage = AmqpRequestMessage.CreateRequest(
                    ManagementConstants.Operations.AddRuleOperation,
                    ServiceBusConnection.OperationTimeout,
                    null);
                amqpRequestMessage.Map[ManagementConstants.Properties.RuleName] = description.Name;
                amqpRequestMessage.Map[ManagementConstants.Properties.RuleDescription] =
                    AmqpMessageConverter.GetRuleDescriptionMap(description);

                var response = await InnerReceiver.ExecuteRequestResponseAsync(amqpRequestMessage).ConfigureAwait(false);

                if (response.StatusCode != AmqpResponseStatusCode.OK)
                    throw response.ToMessagingContractException();
            }
            catch (Exception exception)
            {
                throw AmqpExceptionHelper.GetClientException(exception);
            }
        }

        public async Task OnRemoveRuleAsync(string ruleName)
        {
            try
            {
                var amqpRequestMessage =
                    AmqpRequestMessage.CreateRequest(
                        ManagementConstants.Operations.RemoveRuleOperation,
                        ServiceBusConnection.OperationTimeout,
                        null);
                amqpRequestMessage.Map[ManagementConstants.Properties.RuleName] = ruleName;

                var response = await InnerReceiver.ExecuteRequestResponseAsync(amqpRequestMessage).ConfigureAwait(false);

                if (response.StatusCode != AmqpResponseStatusCode.OK)
                    throw response.ToMessagingContractException();
            }
            catch (Exception exception)
            {
                throw AmqpExceptionHelper.GetClientException(exception);
            }
        }
    }
}