using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class QueueDescription : IEquatable<QueueDescription>
    {
        public QueueDescription(string path)
        {
            this.Path = path;
        }

        public string Path { get; set; }

        public TimeSpan LockDuration { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Supported values: 1024;2048;3072;4096;5120
        /// </summary>
        public long MaxSizeInMegabytes { get; set; } = 1024;

        public bool RequiresDuplicateDetection { get; set; } = false;

        public bool RequiresSession { get; set; } = false;

        public TimeSpan DefaultMessageTimeToLive { get; set; } = TimeSpan.MaxValue;

        public TimeSpan AutoDeleteOnIdle { get; set; } = TimeSpan.MaxValue;

        public bool EnableDeadLetteringOnMessageExpiration { get; set; } = false;

        public TimeSpan DuplicateDetectionHistoryTimeWindow { get; set; } = TimeSpan.FromSeconds(30);

        public int MaxDeliveryCount { get; set; } = 10;

        public AuthorizationRules AuthorizationRules { get; set; } = null;

        public EntityStatus Status { get; set; } = EntityStatus.Active;

        public string ForwardTo { get; set; } = null;

        public string ForwardDeadLetteredMessagesTo { get; set; } = null;

        public bool EnablePartitioning { get; set; } = false;

        public bool EnableBatchedOperations { get; set; } = false;

        static internal QueueDescription ParseFromContent(string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "entry")
                {
                    return ParseFromEntryElement(xDoc);
                }
            }

            throw new MessagingEntityNotFoundException("Queue was not found");
        }

        static internal IList<QueueDescription> ParseCollectionFromContent(string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "feed")
                {
                    var queueList = new List<QueueDescription>();

                    var entryList = xDoc.Elements(XName.Get("entry", ManagementClient.AtomNs));
                    foreach (var entry in entryList)
                    {
                        queueList.Add(ParseFromEntryElement(entry));
                    }

                    return queueList;
                }
            }

            throw new MessagingEntityNotFoundException("Queue was not found");
        }

        // TODO: Authorization
        // TODO: Revisit all properties and ensure they are populated.
        static private QueueDescription ParseFromEntryElement(XElement xEntry)
        {
            try
            {
                var name = xEntry.Element(XName.Get("title", ManagementClient.AtomNs)).Value;
                var qd = new QueueDescription(name);

                var qdXml = xEntry.Element(XName.Get("content", ManagementClient.AtomNs))
                    .Element(XName.Get("QueueDescription", ManagementClient.SbNs));

                if (qdXml == null)
                {
                    throw new MessagingEntityNotFoundException("Queue was not found");
                }

                foreach (var element in qdXml.Elements())
                {
                    // TODO: Alphabetical ordering
                    switch (element.Name.LocalName)
                    {
                        case "MaxSizeInMegabytes":
                            qd.MaxSizeInMegabytes = long.Parse(element.Value);
                            break;
                        case "RequiresDuplicateDetection":
                            qd.RequiresDuplicateDetection = bool.Parse(element.Value);
                            break;
                        case "RequiresSession":
                            qd.RequiresSession = bool.Parse(element.Value);
                            break;
                        case "DeadLetteringOnMessageExpiration":
                            qd.EnableDeadLetteringOnMessageExpiration = bool.Parse(element.Value);
                            break;
                        case "DuplicateDetectionHistoryTimeWindow":
                            qd.DuplicateDetectionHistoryTimeWindow = XmlConvert.ToTimeSpan(element.Value);
                            break;
                        case "LockDuration":
                            qd.LockDuration = XmlConvert.ToTimeSpan(element.Value);
                            break;
                        case "DefaultMessageTimeToLive":
                            qd.DefaultMessageTimeToLive = XmlConvert.ToTimeSpan(element.Value);
                            break;
                        case "MaxDeliveryCount":
                            qd.MaxDeliveryCount = int.Parse(element.Value);
                            break;
                        case "EnableBatchedOperations":
                            qd.EnableBatchedOperations = bool.Parse(element.Value);
                            break;
                        case "Status":
                            qd.Status = (EntityStatus)Enum.Parse(typeof(EntityStatus), element.Value);
                            break;
                        case "AutoDeleteOnIdle":
                            qd.AutoDeleteOnIdle = XmlConvert.ToTimeSpan(element.Value);
                            break;
                        case "EnablePartitioning":
                            qd.EnablePartitioning = bool.Parse(element.Value);
                            break;
                    }
                }

                return qd;
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
                        new XElement(XName.Get("QueueDescription",ManagementClient.SbNs),
                            new XElement(XName.Get("LockDuration", ManagementClient.SbNs), XmlConvert.ToString(this.LockDuration)),
                            new XElement(XName.Get("MaxSizeInMegabytes", ManagementClient.SbNs), XmlConvert.ToString(this.MaxSizeInMegabytes)),
                            new XElement(XName.Get("RequiresDuplicateDetection", ManagementClient.SbNs), XmlConvert.ToString(this.RequiresDuplicateDetection)),
                            new XElement(XName.Get("RequiresSession", ManagementClient.SbNs), XmlConvert.ToString(this.RequiresSession)),
                            this.DefaultMessageTimeToLive != TimeSpan.MaxValue ? new XElement(XName.Get("DefaultMessageTimeToLive", ManagementClient.SbNs), XmlConvert.ToString(this.DefaultMessageTimeToLive)) : null,
                            this.AutoDeleteOnIdle != TimeSpan.MaxValue ? new XElement(XName.Get("AutoDeleteOnIdle", ManagementClient.SbNs), XmlConvert.ToString(this.AutoDeleteOnIdle)) : null,
                            new XElement(XName.Get("DeadLetteringOnMessageExpiration", ManagementClient.SbNs), XmlConvert.ToString(this.EnableDeadLetteringOnMessageExpiration)),
                            this.RequiresDuplicateDetection && this.DuplicateDetectionHistoryTimeWindow != default ? 
                                new XElement(XName.Get("DuplicateDetectionHistoryTimeWindow", ManagementClient.SbNs), XmlConvert.ToString(this.DuplicateDetectionHistoryTimeWindow)) 
                                : null,
                            new XElement(XName.Get("MaxDeliveryCount", ManagementClient.SbNs), XmlConvert.ToString(this.MaxDeliveryCount)),
                            new XElement(XName.Get("Status", ManagementClient.SbNs), this.Status.ToString()),
                            this.ForwardTo != null ? new XElement(XName.Get("ForwardTo", ManagementClient.SbNs), this.ForwardTo) : null,
                            this.ForwardDeadLetteredMessagesTo != null ? new XElement(XName.Get("ForwardDeadLetteredMessagesTo", ManagementClient.SbNs), this.ForwardDeadLetteredMessagesTo) : null,
                            new XElement(XName.Get("EnablePartitioning", ManagementClient.SbNs), XmlConvert.ToString(this.EnablePartitioning)),
                            new XElement(XName.Get("EnableBatchedOperations", ManagementClient.SbNs), XmlConvert.ToString(this.EnableBatchedOperations))
                        ))
                    ));

            return doc;
        }

        // TODO: Auth rules
        public bool Equals(QueueDescription other)
        {
            if (this.Path.Equals(other.Path, StringComparison.OrdinalIgnoreCase)
                && this.AutoDeleteOnIdle.Equals(other.AutoDeleteOnIdle)
                && this.DefaultMessageTimeToLive.Equals(other.DefaultMessageTimeToLive)
                && this.DuplicateDetectionHistoryTimeWindow.Equals(other.DuplicateDetectionHistoryTimeWindow)
                && this.EnableBatchedOperations == other.EnableBatchedOperations
                && this.EnableDeadLetteringOnMessageExpiration == other.EnableDeadLetteringOnMessageExpiration
                && this.EnablePartitioning == other.EnablePartitioning
                && string.Equals(this.ForwardDeadLetteredMessagesTo, other.ForwardDeadLetteredMessagesTo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(this.ForwardTo, other.ForwardTo, StringComparison.OrdinalIgnoreCase)
                && this.LockDuration.Equals(other.LockDuration)
                && this.MaxDeliveryCount == other.MaxDeliveryCount
                && this.MaxSizeInMegabytes == other.MaxSizeInMegabytes
                && this.RequiresDuplicateDetection.Equals(other.RequiresDuplicateDetection)
                && this.RequiresSession.Equals(other.RequiresSession)
                && this.Status.Equals(other.Status))
            {
                return true;
            }

            return false;
        }
    }
}
