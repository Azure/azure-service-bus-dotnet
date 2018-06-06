using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class TopicRuntimeInfo
    {
        public TopicRuntimeInfo(string name)
        {
            this.Path = name;
        }

        public string Path { get; set; }

        public DateTime AccessedAt { get; internal set; }

        public DateTime CreatedAt { get; internal set; }

        public DateTime UpdatedAt { get; internal set; }

        public long SizeInBytes { get; internal set; }

        public int SubscriptionCount { get; internal set; }

        static internal TopicRuntimeInfo ParseFromContent(string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "entry")
                {
                    return ParseFromEntryElement(xDoc);
                }
            }

            // TODO error handling
            throw new NotImplementedException(xml);
        }

        static internal IList<TopicRuntimeInfo> ParseCollectionFromContent(string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "feed")
                {
                    var topicList = new List<TopicRuntimeInfo>();

                    var entryList = xDoc.Elements(XName.Get("entry", ManagementConstants.AtomNs));
                    foreach (var entry in entryList)
                    {
                        topicList.Add(ParseFromEntryElement(entry));
                    }

                    return topicList;
                }
            }

            throw new NotImplementedException(xml);
        }

        static private TopicRuntimeInfo ParseFromEntryElement(XElement xEntry)
        {
            try
            {
                var name = xEntry.Element(XName.Get("title", ManagementConstants.AtomNs)).Value;
                var topicRuntimeInfo = new TopicRuntimeInfo(name);

                var qdXml = xEntry.Element(XName.Get("content", ManagementConstants.AtomNs))
                    .Element(XName.Get("TopicDescription", ManagementConstants.SbNs));

                foreach (var element in qdXml.Elements())
                {
                    switch (element.Name.LocalName)
                    {
                        case "AccessedAt":
                            topicRuntimeInfo.AccessedAt = DateTime.Parse(element.Value);
                            break;
                        case "CreatedAt":
                            topicRuntimeInfo.CreatedAt = DateTime.Parse(element.Value);
                            break;
                        case "SizeInBytes":
                            topicRuntimeInfo.SizeInBytes = long.Parse(element.Value);
                            break;
                        case "SubscriptionCount":
                            topicRuntimeInfo.SubscriptionCount = int.Parse(element.Value);
                            break;
                        case "UpdatedAt":
                            topicRuntimeInfo.UpdatedAt = DateTime.Parse(element.Value);
                            break;
                    }
                }

                return topicRuntimeInfo;
            }
            catch (Exception ex)
            {
                throw new ServiceBusException(false, ex);
            }
        }
    }
}
