namespace Microsoft.Azure.ServiceBus.Management
{
    using System;
    using System.Collections.Generic;
    using System.Xml.Linq;

    internal static class TopicRuntimeInfoExtensions
    {
        public static TopicRuntimeInfo ParseFromContent(string xml)
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

        // TODO: is this used?
        static IList<TopicRuntimeInfo> ParseCollectionFromContent(string xml)
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

        static TopicRuntimeInfo ParseFromEntryElement(XElement xEntry)
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