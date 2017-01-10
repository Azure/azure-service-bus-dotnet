// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Filters
{
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Represents set of actions written in SQL language-based syntax that is performed against a <see cref="BrokeredMessage" />.
    /// </summary>
    public sealed class SqlRuleAction : RuleAction
    {
        PropertyDictionary parameters;

        /// <summary>Initializes a new instance of the
        /// <see cref="SqlRuleAction" /> class with the specified SQL expression.</summary>
        /// <param name="sqlExpression">The SQL expression.</param>
        public SqlRuleAction(string sqlExpression)
            : this(sqlExpression, Constants.FilterConstants.DefaultCompatibilityLevel)
        {
        }

        /// <summary>Initializes a new instance of the
        /// <see cref="SqlRuleAction" /> class with the specified SQL expression and compatibility level.</summary>
        /// <param name="sqlExpression">The SQL expression.</param>
        /// <param name="compatibilityLevel">Reserved for future use. An integer value showing compatibility level. Currently hard-coded to 20.</param>
        public SqlRuleAction(string sqlExpression, int compatibilityLevel)
        {
            // Add the same checks in Validate() method. Constructor is not invoked during deserialization.
            if (string.IsNullOrEmpty(sqlExpression))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace("sqlExpression");
            }

            if (sqlExpression.Length > Constants.FilterConstants.MaximumSqlRuleActionStatementLength)
            {
                throw Fx.Exception.Argument(
                    "sqlExpression",
                    Resources.SqlFilterActionStatmentTooLong.FormatForUser(
                        sqlExpression.Length,
                        Constants.FilterConstants.MaximumSqlRuleActionStatementLength));
            }

            this.SqlExpression = sqlExpression;
            this.CompatibilityLevel = compatibilityLevel;
        }

        /// <summary>Gets the SQL expression.</summary>
        /// <value>The SQL expression.</value>
        public string SqlExpression { get; private set; }

        /// <summary>This property is reserved for future use. An integer value showing the compatibility level, currently hard-coded to 20.</summary>
        /// <value>An integer value showing the compatibility level</value>
        /// <remarks>This property is reserved for future use.</remarks>
        public int CompatibilityLevel { get; private set; }

        /// <summary>Sets the value of a rule action.</summary>
        /// <value>The value of a rule action.</value>
        public IDictionary<string, object> Parameters => this.parameters ?? (this.parameters = new PropertyDictionary());

        /// <summary>Returns a string representation of <see cref="SqlRuleAction" />.</summary>
        /// <returns>The string representation of <see cref="SqlRuleAction" />.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "SqlRuleAction: {0}", this.SqlExpression);
        }
    }
}