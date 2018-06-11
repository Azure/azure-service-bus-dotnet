// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using Microsoft.Azure.ServiceBus.Management;

namespace Microsoft.Azure.ServiceBus
{
    /// <summary>
    /// Matches none the messages arriving to be selected for the subscription.
    /// </summary>
    public sealed class FalseFilter : SqlFilter
    {
        internal static readonly FalseFilter Default = new FalseFilter();

        /// <summary>
        /// Initializes a new instance of the <see cref="FalseFilter" /> class.
        /// </summary>
        public FalseFilter()
            : base("1=0")
        {
        }

        /// <summary>
        /// Converts the current instance to its string representation.
        /// </summary>
        /// <returns>A string representation of the current instance.</returns>
        public override string ToString()
        {
            return "FalseFilter";
        }

        internal override XElement Serialize()
        {
            XElement filter = new XElement(
                XName.Get("Filter", ManagementConstants.SbNs),
                new XAttribute(XName.Get("type", ManagementConstants.XmlSchemaNs), nameof(FalseFilter)),
                new XElement(XName.Get("SqlExpression", ManagementConstants.SbNs), this.SqlExpression));

            return filter;
        }

        public override bool Equals(Filter other)
        {
            return other is FalseFilter;
        }
    }
}