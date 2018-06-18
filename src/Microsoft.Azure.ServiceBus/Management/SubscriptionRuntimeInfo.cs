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

        public long MessageCount { get; internal set; }

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

            throw new MessagingEntityNotFoundException("Subscription was not found");
        }

        static internal IList<SubscriptionRuntimeInfo> ParseCollectionFromContent(string topicName, string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "feed")
                {
                    var subscriptionList = new List<SubscriptionRuntimeInfo>();

                    var entryList = xDoc.Elements(XName.Get("entry", ManagementClientConstants.AtomNs));
                    foreach (var entry in entryList)
                    {
                        subscriptionList.Add(ParseFromEntryElement(topicName, entry));
                    }

                    return subscriptionList;
                }
            }

            throw new MessagingEntityNotFoundException("Subscription was not found");
        }

        static private SubscriptionRuntimeInfo ParseFromEntryElement(string topicName, XElement xEntry)
        {
            try
            {
                var name = xEntry.Element(XName.Get("title", ManagementClientConstants.AtomNs)).Value;
                var subscriptionRuntimeInfo = new SubscriptionRuntimeInfo(topicName, name);

                var qdXml = xEntry.Element(XName.Get("content", ManagementClientConstants.AtomNs))?
                    .Element(XName.Get("SubscriptionDescription", ManagementClientConstants.SbNs));

                if (qdXml == null)
                {
                    throw new MessagingEntityNotFoundException("Subscription was not found");
                }

                foreach (var element in qdXml.Elements())
                {
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
                        case "MessageCount":
                            subscriptionRuntimeInfo.MessageCount = long.Parse(element.Value);
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
            catch (Exception ex) when (!(ex is ServiceBusException))
            {
                throw new ServiceBusException(false, ex);
            }
        }
    }
}
