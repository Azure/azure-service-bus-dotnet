// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml.Linq;
using Microsoft.Azure.ServiceBus.Management;

namespace Microsoft.Azure.ServiceBus
{
    /// <summary>
    /// Describes a filter expression that is evaluated against a Message.
    /// </summary>
    /// <remarks>
    /// Filter is an abstract class with the following concrete implementations:
    /// <list type="bullet">
    /// <item><b>SqlFilter</b> that represents a filter using SQL syntax. </item>
    /// <item><b>CorrelationFilter</b> that provides an optimization for correlation equality expressions.</item>
    /// </list>
    /// </remarks>
    /// <seealso cref="SqlFilter"/>
    /// <seealso cref="TrueFilter"/>
    /// <seealso cref="CorrelationFilter "/>
    /// <seealso cref="FalseFilter"/>
    public abstract class Filter : IEquatable<Filter>
    {
        internal Filter()
        {
            // This is intentionally left blank. This constructor exists
            // only to prevent external assemblies inheriting from it.
        }

        internal static Filter ParseFromXElement(XElement xElement)
        {
            var attribute = xElement.Attribute(XName.Get("type", ManagementClientConstants.XmlSchemaNs));
            if (attribute == null)
            {
                return null;
            }

            switch (attribute.Value)
            {
                case "SqlFilter":
                    return SqlFilterExtensions.ParseFromXElement(xElement);
                case "CorrelationFilter":
                    return CorrelationFilterExtensions.ParseFromXElement(xElement);
                case "TrueFilter":
                    return new TrueFilter();
                case "FalseFilter":
                    return new FalseFilter();
                default:
                    return null;
            }
        }

        internal XElement Serialize()
        {
            switch (this)
            {
                case SqlFilter sqlFilter:
                    return SqlFilterExtensions.Serialize(sqlFilter);

                case CorrelationFilter correlationFilter:
                    return CorrelationFilterExtensions.Serialize(correlationFilter);

                default:
                    return null;
            }
        }

        public abstract bool Equals(Filter other);
    }
}