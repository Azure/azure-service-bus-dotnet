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

        public MessageCountDetails MessageCountDetails { get; internal set; }

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

            throw new MessagingEntityNotFoundException("Topic was not found");
        }

        static internal IList<TopicRuntimeInfo> ParseCollectionFromContent(string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "feed")
                {
                    var topicList = new List<TopicRuntimeInfo>();

                    var entryList = xDoc.Elements(XName.Get("entry", ManagementClientConstants.AtomNs));
                    foreach (var entry in entryList)
                    {
                        topicList.Add(ParseFromEntryElement(entry));
                    }

                    return topicList;
                }
            }

            throw new MessagingEntityNotFoundException("Topic was not found");
        }

        static private TopicRuntimeInfo ParseFromEntryElement(XElement xEntry)
        {
            try
            {
                var name = xEntry.Element(XName.Get("title", ManagementClientConstants.AtomNs)).Value;
                var topicRuntimeInfo = new TopicRuntimeInfo(name);

                var qdXml = xEntry.Element(XName.Get("content", ManagementClientConstants.AtomNs))?
                    .Element(XName.Get("TopicDescription", ManagementClientConstants.SbNs));

                if (qdXml == null)
                {
                    throw new MessagingEntityNotFoundException("Topic was not found");
                }

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
                        case "CountDetails":
                            topicRuntimeInfo.MessageCountDetails = new MessageCountDetails();
                            foreach (var countElement in element.Elements())
                            {
                                switch (countElement.Name.LocalName)
                                {
                                    case "ActiveMessageCount":
                                        topicRuntimeInfo.MessageCountDetails.ActiveMessageCount = long.Parse(countElement.Value);
                                        break;
                                    case "DeadLetterMessageCount":
                                        topicRuntimeInfo.MessageCountDetails.DeadLetterMessageCount = long.Parse(countElement.Value);
                                        break;
                                    case "ScheduledMessageCount":
                                        topicRuntimeInfo.MessageCountDetails.ScheduledMessageCount = long.Parse(countElement.Value);
                                        break;
                                    case "TransferMessageCount":
                                        topicRuntimeInfo.MessageCountDetails.TransferMessageCount = long.Parse(countElement.Value);
                                        break;
                                    case "TransferDeadLetterMessageCount":
                                        topicRuntimeInfo.MessageCountDetails.TransferDeadLetterMessageCount = long.Parse(countElement.Value);
                                        break;
                                }
                            }
                            break;
                    }
                }

                return topicRuntimeInfo;
            }
            catch (Exception ex) when (!(ex is ServiceBusException))
            {
                throw new ServiceBusException(false, ex);
            }
        }
    }
}
