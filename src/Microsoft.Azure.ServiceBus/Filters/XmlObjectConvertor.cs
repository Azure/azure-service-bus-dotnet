using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Azure.ServiceBus.Management;

namespace Microsoft.Azure.ServiceBus.Filters
{
    internal class XmlObjectConvertor
    {
        internal static object ParseValueObject(XElement element)
        {
            var prefix = element.GetPrefixOfNamespace(XNamespace.Get(ManagementClientConstants.XmlSchemaNs));
            var type = element.Attribute(XName.Get("type", ManagementClientConstants.XmlSchemaInstanceNs)).Value;
            switch (type.Substring(prefix.Length + 1))
            {
                case "string":
                    return element.Value;
                case "int":
                    return XmlConvert.ToInt32(element.Value);
                case "long":
                    return XmlConvert.ToInt64(element.Value);
                case "boolean":
                    return XmlConvert.ToBoolean(element.Value);
                case "double":
                    return XmlConvert.ToDouble(element.Value);
                case "dateTime":
                    return XmlConvert.ToDateTime(element.Value, XmlDateTimeSerializationMode.Utc);
                default:
                    return null;
            }
        }

        internal static XElement SerializeObject(object value)
        {
            var prefix = "l28";
            string type = prefix + ':';
            if (value is string)
            {
                type += "string";
            }
            else if (value is int)
            {
                type += "int";
            }
            else if (value is long)
            {
                type += "long";
            }
            else if (value is bool)
            {
                type += "boolean";
            }
            else if (value is double)
            {
                type += "double";
            }
            else if (value is DateTime)
            {
                type += "dateTime";
            }
            else
            {
                return null;
            }

            var element = new XElement(XName.Get("Value", ManagementClientConstants.SbNs),
                new XAttribute(XName.Get("type", ManagementClientConstants.XmlSchemaInstanceNs), type),
                new XAttribute(XNamespace.Xmlns + prefix, ManagementClientConstants.XmlSchemaNs),
                value);

            return element;
        }
    }
}
