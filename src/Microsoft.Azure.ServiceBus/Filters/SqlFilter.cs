// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Filters
{
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Represents a filter which is a composition of an expression and an action that is executed in the pub/sub pipeline.
    /// </summary>
    public class SqlFilter : Filter
    {
        PropertyDictionary parameters;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlFilter" /> class using the specified SQL expression.
        /// </summary>
        /// <param name="sqlExpression">The SQL expression.</param>
        public SqlFilter(string sqlExpression)
            : this(sqlExpression, Constants.FilterConstants.DefaultCompatibilityLevel)
        {
        }

        SqlFilter(string sqlExpression, int compatibilityLevel)
        {
            // Add the same checks in Validate() method. Constructor is not invoked during deserialization.
            if (string.IsNullOrEmpty(sqlExpression))
            {
                throw Fx.Exception.ArgumentNull("sqlExpression");
            }

            if (sqlExpression.Length > Constants.FilterConstants.MaximumSqlFilterStatementLength)
            {
                throw Fx.Exception.Argument(
                    "sqlExpression",
                    Resources.SqlFilterStatmentTooLong.FormatForUser(
                        sqlExpression.Length,
                        Constants.FilterConstants.MaximumSqlFilterStatementLength));
            }

            this.SqlExpression = sqlExpression;
            this.CompatibilityLevel = compatibilityLevel;
        }

        /// <summary>
        /// Gets the SQL expression.
        /// </summary>
        /// <value>The SQL expression.</value>
        public string SqlExpression { get; private set; }

        /// <summary>
        /// This property is reserved for future use. An integer value showing the compatibility level, currently hard-coded to 20.
        /// </summary>
        /// <value>The compatibility level.</value>
        /// <remarks>This property is reserved for future use.</remarks>
        public int CompatibilityLevel { get; private set; }

        /// <summary>
        /// Sets the value of a filter expression.
        /// </summary>
        /// <value>The value of a filter expression.</value>
        public IDictionary<string, object> Parameters => this.parameters ?? (this.parameters = new PropertyDictionary());

        /// <summary>
        /// Returns a string representation of <see cref="SqlFilter" />.
        /// </summary>
        /// <returns>The string representation of <see cref="SqlFilter" />.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "SqlFilter: {0}", this.SqlExpression);
        }
    }
}