// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus.Filters
{
    /// <summary>
    ///     Represents the correlation filter expression.
    /// </summary>
    /// <remarks>
    ///     The CorrelationFilter provides an efficient shortcut for declarations of filters that deal only with correlation
    ///     equality.
    ///     In this case the cost of the lexigraphical analysis of the expression can be avoided.
    ///     Not only will correlation filters be optimized at declaration time, but they will also be optimized at runtime.
    ///     Correlation filter matching can be reduced to a hashtable lookup, which aggregates the complexity of the set of
    ///     defined correlation filters to O(1).
    /// </remarks>
    public sealed class CorrelationFilter : Filter
    {
        PropertyDictionary properties;

        /// <summary>
        ///     Initializes a new instance of the <see cref="CorrelationFilter" /> class with default values.
        /// </summary>
        public CorrelationFilter()
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="CorrelationFilter" /> class with the specified correlation identifier.
        /// </summary>
        /// <param name="correlationId">The identifier for the correlation.</param>
        /// <exception cref="System.ArgumentException">Thrown when the <paramref name="correlationId" /> is null or empty.</exception>
        public CorrelationFilter(string correlationId)
            : this()
        {
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(correlationId));
            }

            CorrelationId = correlationId;
        }

        /// <summary>
        ///     Identifier of the correlation.
        /// </summary>
        /// <value>The identifier of the correlation.</value>
        public string CorrelationId { get; set; }

        /// <summary>
        ///     Identifier of the message.
        /// </summary>
        /// <value>The identifier of the message.</value>
        public string MessageId { get; set; }

        /// <summary>
        ///     Address to send to.
        /// </summary>
        /// <value>The address to send to.</value>
        public string To { get; set; }

        /// <summary>
        ///     Address of the queue to reply to.
        /// </summary>
        /// <value>The address of the queue to reply to.</value>
        public string ReplyTo { get; set; }

        /// <summary>
        ///     Application specific label.
        /// </summary>
        /// <value>The application specific label.</value>
        public string Label { get; set; }

        /// <summary>
        ///     Session identifier.
        /// </summary>
        /// <value>The session identifier.</value>
        public string SessionId { get; set; }

        /// <summary>
        ///     Session identifier to reply to.
        /// </summary>
        /// <value>The session identifier to reply to.</value>
        public string ReplyToSessionId { get; set; }

        /// <summary>
        ///     Content type of the message.
        /// </summary>
        /// <value>The content type of the message.</value>
        public string ContentType { get; set; }

        /// <summary>
        ///     Application specific properties of the message.
        /// </summary>
        /// <value>The application specific properties of the message.</value>
        public IDictionary<string, object> Properties => properties ?? (properties = new PropertyDictionary());

        /// <summary>
        ///     Converts the value of the current instance to its equivalent string representation.
        /// </summary>
        /// <returns>A string representation of the current instance.</returns>
        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append("CorrelationFilter: ");

            var firstExpression = true;

            AppendPropertyExpression(ref firstExpression, builder, "sys.CorrelationId", CorrelationId);
            AppendPropertyExpression(ref firstExpression, builder, "sys.MessageId", MessageId);
            AppendPropertyExpression(ref firstExpression, builder, "sys.To", To);
            AppendPropertyExpression(ref firstExpression, builder, "sys.ReplyTo", ReplyTo);
            AppendPropertyExpression(ref firstExpression, builder, "sys.Label", Label);
            AppendPropertyExpression(ref firstExpression, builder, "sys.SessionId", SessionId);
            AppendPropertyExpression(ref firstExpression, builder, "sys.ReplyToSessionId", ReplyToSessionId);
            AppendPropertyExpression(ref firstExpression, builder, "sys.ContentType", ContentType);

            foreach (var pair in Properties)
            {
                var propertyName = pair.Key;
                var propertyValue = pair.Value;

                AppendPropertyExpression(ref firstExpression, builder, propertyName, propertyValue);
            }

            return builder.ToString();
        }

        void AppendPropertyExpression(ref bool firstExpression, StringBuilder builder, string propertyName, object value)
        {
            if (value != null)
            {
                if (firstExpression)
                {
                    firstExpression = false;
                }
                else
                {
                    builder.Append(" AND ");
                }

                builder.AppendFormat("{0} = '{1}'", propertyName, value);
            }
        }
    }
}