namespace Microsoft.Azure.ServiceBus
{
    using System.Xml.Linq;
    using Microsoft.Azure.ServiceBus.Management;

    internal static class SqlFilterExtensions
    {
        public static Filter ParseFromXElement(XElement xElement)
        {
            var expression = xElement.Element(XName.Get("SqlExpression", ManagementClientConstants.SbNs))?.Value;
            if (string.IsNullOrWhiteSpace(expression))
            {
                return null;
            }

            var filter = new SqlFilter(expression);
            // TODO: populate parameters
            return filter;
        }

        // TODO: Populate params
        public static XElement Serialize(this SqlFilter filter)
        {
            return new XElement(
                XName.Get("Filter", ManagementClientConstants.SbNs),
                new XAttribute(XName.Get("type", ManagementClientConstants.XmlSchemaNs), nameof(SqlFilter)),
                new XElement(XName.Get("SqlExpression", ManagementClientConstants.SbNs), filter.SqlExpression));
        }
    }
}