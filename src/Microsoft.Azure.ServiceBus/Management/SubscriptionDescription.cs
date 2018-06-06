using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class SubscriptionDescription : IEquatable<SubscriptionDescription>
    {
        public SubscriptionDescription(string topicPath, string subscriptionName)
        {
            this.TopicPath = topicPath;
            this.SubscriptionName = subscriptionName;
        }

        public TimeSpan LockDuration { get; set; }

        public bool RequiresSession { get; set; }

        public TimeSpan DefaultMessageTimeToLive { get; set; }

        public TimeSpan AutoDeleteOnIdle { get; set; }

        public bool EnableDeadLetteringOnMessageExpiration { get; set; }

        public bool EnableDeadLetteringOnFilterEvaluationExceptions { get; set; }

        public string TopicPath { get; set; }

        public string SubscriptionName { get; set; }

        public int MaxDeliveryCount { get; set; }

        public EntityStatus Status { get; set; }

        public string ForwardTo { get; set; }

        public string ForwardDeadLetteredMessagesTo { get; set; }

        static internal SubscriptionDescription ParseFromContent(string topicName, string xml)
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

        static internal IList<SubscriptionDescription> ParseCollectionFromContent(string topicName, string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "feed")
                {
                    var subscriptionList = new List<SubscriptionDescription>();

                    var entryList = xDoc.Elements(XName.Get("entry", ManagementClient.AtomNs));
                    foreach (var entry in entryList)
                    {
                        subscriptionList.Add(ParseFromEntryElement(topicName, entry));
                    }

                    return subscriptionList;
                }
            }

            throw new MessagingEntityNotFoundException("Subscription was not found");
        }

        // TODO: Authorization and messagecounts
        static private SubscriptionDescription ParseFromEntryElement(string topicName, XElement xEntry)
        {
            try
            {
                var name = xEntry.Element(XName.Get("title", ManagementClient.AtomNs)).Value;
                var subscriptionDesc = new SubscriptionDescription(topicName, name);

                var qdXml = xEntry.Element(XName.Get("content", ManagementClient.AtomNs))
                    .Element(XName.Get("SubscriptionDescription", ManagementClient.SbNs));

                if (qdXml == null)
                {
                    throw new MessagingEntityNotFoundException("Subscription was not found");
                }

                foreach (var element in qdXml.Elements())
                {
                    // TODO: Alphabetical ordering
                    switch (element.Name.LocalName)
                    {
                        case "RequiresSession":
                            subscriptionDesc.RequiresSession = bool.Parse(element.Value);
                            break;
                        case "DeadLetteringOnMessageExpiration":
                            subscriptionDesc.EnableDeadLetteringOnMessageExpiration = bool.Parse(element.Value);
                            break;
                        case "DeadLetteringOnFilterEvaluationExceptions":
                            subscriptionDesc.EnableDeadLetteringOnFilterEvaluationExceptions = bool.Parse(element.Value);
                            break;
                        case "LockDuration":
                            subscriptionDesc.LockDuration = XmlConvert.ToTimeSpan(element.Value);
                            break;
                        case "DefaultMessageTimeToLive":
                            subscriptionDesc.DefaultMessageTimeToLive = XmlConvert.ToTimeSpan(element.Value);
                            break;
                        case "MaxDeliveryCount":
                            subscriptionDesc.MaxDeliveryCount = int.Parse(element.Value);
                            break;
                        case "Status":
                            subscriptionDesc.Status = (EntityStatus)Enum.Parse(typeof(EntityStatus), element.Value);
                            break;
                        case "AutoDeleteOnIdle":
                            subscriptionDesc.AutoDeleteOnIdle = XmlConvert.ToTimeSpan(element.Value);
                            break;
                    }
                }

                return subscriptionDesc;
            }
            catch (Exception ex)
            {
                throw new ServiceBusException(false, ex);
            }
        }

        // TODO: Authorization rules
        internal XDocument Serialize()
        {
            XDocument doc = new XDocument(
                new XElement(XName.Get("entry", ManagementClient.AtomNs),
                    new XElement(XName.Get("content", ManagementClient.AtomNs),
                        new XAttribute("type", "application/xml"),
                        new XElement(XName.Get("SubscriptionDescription", ManagementClient.SbNs),
                            new XElement(XName.Get("LockDuration", ManagementClient.SbNs), XmlConvert.ToString(this.LockDuration)),
                            new XElement(XName.Get("RequiresSession", ManagementClient.SbNs), XmlConvert.ToString(this.RequiresSession)),
                            this.DefaultMessageTimeToLive != TimeSpan.MaxValue ? new XElement(XName.Get("DefaultMessageTimeToLive", ManagementClient.SbNs), XmlConvert.ToString(this.DefaultMessageTimeToLive)) : null,
                            this.AutoDeleteOnIdle != TimeSpan.MaxValue ? new XElement(XName.Get("AutoDeleteOnIdle", ManagementClient.SbNs), XmlConvert.ToString(this.AutoDeleteOnIdle)) : null,
                            new XElement(XName.Get("DeadLetteringOnMessageExpiration", ManagementClient.SbNs), XmlConvert.ToString(this.EnableDeadLetteringOnMessageExpiration)),
                            new XElement(XName.Get("DeadLetteringOnFilterEvaluationExceptions", ManagementClient.SbNs), XmlConvert.ToString(this.EnableDeadLetteringOnFilterEvaluationExceptions)),
                            new XElement(XName.Get("MaxDeliveryCount", ManagementClient.SbNs), XmlConvert.ToString(this.MaxDeliveryCount)),
                            new XElement(XName.Get("Status", ManagementClient.SbNs), this.Status.ToString()),
                            this.ForwardTo != null ? new XElement(XName.Get("ForwardTo", ManagementClient.SbNs), this.ForwardTo) : null,
                            this.ForwardDeadLetteredMessagesTo != null ? new XElement(XName.Get("ForwardDeadLetteredMessagesTo", ManagementClient.SbNs), this.ForwardDeadLetteredMessagesTo) : null
                        ))
                    ));

            return doc;
        }

        public bool Equals(SubscriptionDescription other)
        {
            if (this.SubscriptionName.Equals(other.SubscriptionName, StringComparison.OrdinalIgnoreCase)
                && this.TopicPath.Equals(other.TopicPath, StringComparison.OrdinalIgnoreCase)
                && this.AutoDeleteOnIdle.Equals(other.AutoDeleteOnIdle)
                && this.DefaultMessageTimeToLive.Equals(other.DefaultMessageTimeToLive)
                && this.EnableDeadLetteringOnMessageExpiration == other.EnableDeadLetteringOnMessageExpiration
                && this.EnableDeadLetteringOnFilterEvaluationExceptions == other.EnableDeadLetteringOnFilterEvaluationExceptions
                && this.ForwardDeadLetteredMessagesTo.Equals(other.ForwardDeadLetteredMessagesTo, StringComparison.OrdinalIgnoreCase)
                && this.ForwardTo.Equals(other.ForwardTo, StringComparison.OrdinalIgnoreCase)
                && this.LockDuration.Equals(other.LockDuration)
                && this.MaxDeliveryCount == other.MaxDeliveryCount
                && this.RequiresSession.Equals(other.RequiresSession)
                && this.Status.Equals(other.Status))
            {
                return true;
            }

            return false;
        }
    }
}
