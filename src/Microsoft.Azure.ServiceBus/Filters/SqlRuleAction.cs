// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Xml.Linq;
    using Microsoft.Azure.ServiceBus.Management;
    using Primitives;

    /// <summary>
    /// Represents set of actions written in SQL language-based syntax that is performed against a <see cref="Message" />.
    /// </summary>
    public sealed class SqlRuleAction : RuleAction
    {
        PropertyDictionary parameters;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlRuleAction" /> class with the specified SQL expression.
        /// </summary>
        /// <param name="sqlExpression">The SQL expression.</param>
        /// <remarks>Max allowed length of sql expression is 1024 chars.</remarks>
        public SqlRuleAction(string sqlExpression)
        {
            if (string.IsNullOrEmpty(sqlExpression))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(sqlExpression));
            }

            if (sqlExpression.Length > Constants.MaximumSqlRuleActionStatementLength)
            {
                throw Fx.Exception.Argument(
                    nameof(sqlExpression),
                    Resources.SqlFilterActionStatmentTooLong.FormatForUser(
                        sqlExpression.Length,
                        Constants.MaximumSqlRuleActionStatementLength));
            }

            this.SqlExpression = sqlExpression;
        }

        /// <summary>
        /// Gets the SQL expression.
        /// </summary>
        /// <value>The SQL expression.</value>
        /// <remarks>Max allowed length of sql expression is 1024 chars.</remarks>
        public string SqlExpression { get; }

        /// <summary>
        /// Sets the value of a rule action.
        /// </summary>
        /// <value>The value of a rule action.</value>
        public IDictionary<string, object> Parameters => this.parameters ?? (this.parameters = new PropertyDictionary());

        /// <summary>
        /// Returns a string representation of <see cref="SqlRuleAction" />.
        /// </summary>
        /// <returns>The string representation of <see cref="SqlRuleAction" />.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "SqlRuleAction: {0}", this.SqlExpression);
        }

        internal new static RuleAction ParseFromXElement(XElement xElement)
        {
            var expression = xElement.Element(XName.Get("SqlExpression", ManagementConstants.SbNs))?.Value;
            if (string.IsNullOrWhiteSpace(expression))
            {
                return null;
            }

            var action = new SqlRuleAction(expression);
            // TODO: populate parameters
            return action;
        }

        // TODO: Parameters
        internal override XElement Serialize()
        {
            XElement action = new XElement(
                XName.Get("Action", ManagementConstants.SbNs),
                new XAttribute(XName.Get("type", ManagementConstants.XmlSchemaNs), nameof(SqlRuleAction)),
                new XElement(XName.Get("SqlExpression", ManagementConstants.SbNs), this.SqlExpression));

            return action;
        }

        //TODO: parameters
        public override bool Equals(RuleAction other)
        {
            if (other is SqlRuleAction sqlAction)
            {
                if (string.Equals(this.SqlExpression, sqlAction.SqlExpression, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}