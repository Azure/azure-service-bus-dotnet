// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Amqp;
using Microsoft.Azure.Amqp.Encoding;
using Microsoft.Azure.Amqp.Framing;

namespace Microsoft.Azure.ServiceBus.Amqp
{
    internal sealed class AmqpResponseMessage
    {
        AmqpResponseMessage(AmqpMessage responseMessage)
        {
            AmqpMessage = responseMessage;
            StatusCode = AmqpMessage.GetResponseStatusCode();
            string trackingId;
            if (AmqpMessage.ApplicationProperties.Map.TryGetValue(ManagementConstants.Properties.TrackingId, out trackingId))
            {
                TrackingId = trackingId;
            }

            if (responseMessage.ValueBody != null)
            {
                Map = responseMessage.ValueBody.Value as AmqpMap;
            }
        }

        public AmqpMessage AmqpMessage { get; }

        public AmqpResponseStatusCode StatusCode { get; }

        public string TrackingId { get; }

        public AmqpMap Map { get; }

        public static AmqpResponseMessage CreateResponse(AmqpMessage response)
        {
            return new AmqpResponseMessage(response);
        }

        public TValue GetValue<TValue>(MapKey key)
        {
            if (Map == null)
            {
                throw new ArgumentException(AmqpValue.Name);
            }

            var valueObject = Map[key];
            if (valueObject == null)
            {
                throw new ArgumentException(key.ToString());
            }

            if (!(valueObject is TValue))
            {
                throw new ArgumentException(key.ToString());
            }

            return (TValue) Map[key];
        }

        public IEnumerable<TValue> GetListValue<TValue>(MapKey key)
        {
            if (Map == null)
            {
                throw new ArgumentException(AmqpValue.Name);
            }

            var list = (List<object>) Map[key];

            return list.Cast<TValue>();
        }

        public AmqpSymbol GetResponseErrorCondition()
        {
            var condition = AmqpMessage.ApplicationProperties.Map[ManagementConstants.Response.ErrorCondition];

            return condition is AmqpSymbol amqpSymbol ? amqpSymbol : null;
        }

        public Exception ToMessagingContractException()
        {
            return AmqpMessage.ToMessagingContractException(StatusCode);
        }
    }
}