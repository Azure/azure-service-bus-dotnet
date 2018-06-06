using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class SubscriptionRuntimeInfo
    {
        public SubscriptionRuntimeInfo(string topicName, string subscriptionName)
        {
            this.TopicPath = topicName;
            this.SubscriptionName = subscriptionName;
        }

        public string TopicPath { get; set; }

        public string SubscriptionName { get; set; }

        public MessageCountDetails MessageCountDetails { get; set; }

        public DateTime AccessedAt { get; internal set; }

        public DateTime CreatedAt { get; internal set; }

        public DateTime UpdatedAt { get; internal set; }

        static internal SubscriptionRuntimeInfo ParseFromContent(string topicName, string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "entry")
                {
                    return ParseFromEntryElement(topicName, xDoc);
                }
            }

            // TODO error handling
            throw new NotImplementedException(xml);
        }

        static internal IList<SubscriptionRuntimeInfo> ParseCollectionFromContent(string topicName, string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "feed")
                {
                    var subscriptionList = new List<SubscriptionRuntimeInfo>();

                    var entryList = xDoc.Elements(XName.Get("entry", ManagementConstants.AtomNs));
                    foreach (var entry in entryList)
                    {
                        subscriptionList.Add(ParseFromEntryElement(topicName, entry));
                    }

                    return subscriptionList;
                }
            }

            throw new NotImplementedException(xml);
        }

        static private SubscriptionRuntimeInfo ParseFromEntryElement(string topicName, XElement xEntry)
        {
            try
            {
                var name = xEntry.Element(XName.Get("title", ManagementConstants.AtomNs)).Value;
                var subscriptionRuntimeInfo = new SubscriptionRuntimeInfo(topicName, name);

                var qdXml = xEntry.Element(XName.Get("content", ManagementConstants.AtomNs))
                    .Element(XName.Get("SubscriptionDescription", ManagementConstants.SbNs));

                foreach (var element in qdXml.Elements())
                {
                    // TODO: Alphabetical ordering
                    switch (element.Name.LocalName)
                    {
                        case "AccessedAt":
                            subscriptionRuntimeInfo.AccessedAt = DateTime.Parse(element.Value);
                            break;
                        case "CreatedAt":
                            subscriptionRuntimeInfo.CreatedAt = DateTime.Parse(element.Value);
                            break;
                        case "UpdatedAt":
                            subscriptionRuntimeInfo.UpdatedAt = DateTime.Parse(element.Value);
                            break;
                        case "CountDetails":
                            subscriptionRuntimeInfo.MessageCountDetails = new MessageCountDetails();
                            foreach (var countElement in element.Elements())
                            {
                                switch (countElement.Name.LocalName)
                                {
                                    case "ActiveMessageCount":
                                        subscriptionRuntimeInfo.MessageCountDetails.ActiveMessageCount = long.Parse(countElement.Value);
                                        break;
                                    case "DeadLetterMessageCount":
                                        subscriptionRuntimeInfo.MessageCountDetails.DeadLetterMessageCount = long.Parse(countElement.Value);
                                        break;
                                    case "ScheduledMessageCount":
                                        subscriptionRuntimeInfo.MessageCountDetails.ScheduledMessageCount = long.Parse(countElement.Value);
                                        break;
                                    case "TransferMessageCount":
                                        subscriptionRuntimeInfo.MessageCountDetails.TransferMessageCount = long.Parse(countElement.Value);
                                        break;
                                    case "TransferDeadLetterMessageCount":
                                        subscriptionRuntimeInfo.MessageCountDetails.TransferDeadLetterMessageCount = long.Parse(countElement.Value);
                                        break;
                                }
                            }
                            break;
                    }
                }

                return subscriptionRuntimeInfo;
            }
            catch (Exception ex)
            {
                throw new ServiceBusException(false, ex);
            }
        }
    }
}
