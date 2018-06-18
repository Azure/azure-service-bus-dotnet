namespace Microsoft.Azure.ServiceBus
{
    using System.Xml.Linq;
    using Microsoft.Azure.ServiceBus.Management;

    internal static class RuleActionExtensions
    {
        internal static RuleAction ParseFromXElement(XElement xElement)
        {
            var attribute = xElement.Attribute(XName.Get("type", ManagementClientConstants.XmlSchemaNs));
            if (attribute == null)
            {
                return null;
            }

            switch (attribute.Value)
            {
                case "SqlRuleAction":
                    return ParseFromXElementSqlRuleAction(xElement);

                // anything else, including "EmptyRuleAction", should return null
                default:
                    return null;
            }
        }


        static RuleAction ParseFromXElementSqlRuleAction(XElement xElement)
        {
            var expression = xElement.Element(XName.Get("SqlExpression", ManagementClientConstants.SbNs))?.Value;
            if (string.IsNullOrWhiteSpace(expression))
            {
                return null;
            }

            var action = new SqlRuleAction(expression);

            // TODO: populate parameters
            return action;
        }

        // TODO: Parameters
        public static XElement Serialize(this RuleAction action)
        {
            if (action is SqlRuleAction sqlRuleAction)
            {
                return new XElement(
                        XName.Get("Action", ManagementClientConstants.SbNs),
                        new XAttribute(XName.Get("type", ManagementClientConstants.XmlSchemaNs), nameof(SqlRuleAction)),
                        new XElement(XName.Get("SqlExpression", ManagementClientConstants.SbNs), sqlRuleAction.SqlExpression));
            }

            return null;
        }
    }
}