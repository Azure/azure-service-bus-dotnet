namespace Microsoft.Azure.ServiceBus
{
    using System.Xml.Linq;
    using Microsoft.Azure.ServiceBus.Management;

    internal static class FilterExtensions
    {
        public static Filter ParseFromXElement(XElement xElement)
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

        public static XElement Serialize(this Filter filter)
        {
            switch (filter)
            {
                case SqlFilter sqlFilter:
                    return sqlFilter.Serialize();

                case CorrelationFilter correlationFilter:
                    return correlationFilter.Serialize();

                default:
                    return null;
            }
        }
    }
}