using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class QueueDescription
    {
        public QueueDescription(string path)
        {
            this.Path = path;
        }

        public string Path { get; set; }

        public TimeSpan LockDuration { get; set; }

        public long MaxSizeInMegabytes { get; set; }

        public bool RequiresDuplicateDetection { get; set; }

        public bool RequiresSession { get; set; }

        public TimeSpan DefaultMessageTimeToLive { get; set; }

        public TimeSpan AutoDeleteOnIdle { get; set; }

        public bool EnableDeadLetteringOnMessageExpiration { get; set; }

        public TimeSpan DuplicateDetectionHistoryTimeWindow { get; set; }

        public int MaxDeliveryCount { get; set; }

        public AuthorizationRules AuthorizationRules { get; set; }

        public EntityStatus Status { get; set; }

        public string ForwardTo { get; set; }

        public string ForwardDeadLetteredMessagesTo { get; set; }

        public bool EnablePartitioning { get; set; }

        public bool EnableBatchedOperations { get; set; }

        public QueueRuntimeInfo QueueRuntimeInfo { get; internal set; }

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

            throw new NotImplementedException(xml);
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

            throw new NotImplementedException(xml);
        }

        // TODO: Authorization and messagecounts
        static private QueueDescription ParseFromEntryElement(XElement xEntry)
        {
            try
            {
                var name = xEntry.Element(XName.Get("title", ManagementClient.AtomNs)).Value;
                var qd = new QueueDescription(name);

                var qdXml = xEntry.Element(XName.Get("content", ManagementClient.AtomNs))
                    .Element(XName.Get("QueueDescription", ManagementClient.SbNs));

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
                        case "SizeInBytes": 
                            if (qd.QueueRuntimeInfo == null)
                            {
                                qd.QueueRuntimeInfo = new QueueRuntimeInfo();
                            }
                            qd.QueueRuntimeInfo.SizeInBytes = long.Parse(element.Value);
                            break;
                        case "MessageCount":
                            if (qd.QueueRuntimeInfo == null)
                            {
                                qd.QueueRuntimeInfo = new QueueRuntimeInfo();
                            }
                            qd.QueueRuntimeInfo.MessageCount = long.Parse(element.Value);
                            break;
                        case "Status":
                            qd.Status = (EntityStatus)Enum.Parse(typeof(EntityStatus), element.Value);
                            break;
                        case "CreatedAt":
                            if (qd.QueueRuntimeInfo == null)
                            {
                                qd.QueueRuntimeInfo = new QueueRuntimeInfo();
                            }
                            qd.QueueRuntimeInfo.CreatedAt = DateTime.Parse(element.Value);
                            break;
                        case "UpdatedAt":
                            if (qd.QueueRuntimeInfo == null)
                            {
                                qd.QueueRuntimeInfo = new QueueRuntimeInfo();
                            }
                            qd.QueueRuntimeInfo.UpdatedAt = DateTime.Parse(element.Value);
                            break;
                        case "AccessedAt":
                            if (qd.QueueRuntimeInfo == null)
                            {
                                qd.QueueRuntimeInfo = new QueueRuntimeInfo();
                            }
                            qd.QueueRuntimeInfo.AccessedAt = DateTime.Parse(element.Value);
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
                throw new ServiceBusException(true, ex);
            }
        }

        // TODO: Authorization rules
        internal XDocument Serialize()
        {
            //XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance"),

            XDocument doc = new XDocument(
                new XElement(XName.Get("entry", ManagementClient.AtomNs),
                    new XElement(XName.Get("content", ManagementClient.AtomNs),
                        new XAttribute("type", "application/xml"),
                        new XElement(XName.Get("QueueDescription",ManagementClient.SbNs),
                            new XElement(XName.Get("LockDuration", ManagementClient.SbNs), XmlConvert.ToString(this.LockDuration)),
                            new XElement(XName.Get("MaxSizeInMegabytes", ManagementClient.SbNs), XmlConvert.ToString(this.MaxSizeInMegabytes)),
                            new XElement(XName.Get("RequiresDuplicateDetection", ManagementClient.SbNs), XmlConvert.ToString(this.RequiresDuplicateDetection)),
                            new XElement(XName.Get("RequiresSession", ManagementClient.SbNs), XmlConvert.ToString(this.RequiresSession)),
                            new XElement(XName.Get("DefaultMessageTimeToLive", ManagementClient.SbNs), XmlConvert.ToString(this.DefaultMessageTimeToLive)),
                            new XElement(XName.Get("AutoDeleteOnIdle", ManagementClient.SbNs), XmlConvert.ToString(this.AutoDeleteOnIdle)),
                            new XElement(XName.Get("EnableDeadLetteringOnMessageExpiration", ManagementClient.SbNs), XmlConvert.ToString(this.EnableDeadLetteringOnMessageExpiration)),
                            new XElement(XName.Get("DuplicateDetectionHistoryTimeWindow", ManagementClient.SbNs), XmlConvert.ToString(this.DuplicateDetectionHistoryTimeWindow)),
                            new XElement(XName.Get("MaxDeliveryCount", ManagementClient.SbNs), XmlConvert.ToString(this.MaxDeliveryCount)),
                            new XElement(XName.Get("Status", ManagementClient.SbNs), this.Status.ToString()),
                            new XElement(XName.Get("ForwardTo", ManagementClient.SbNs), this.ForwardTo),
                            new XElement(XName.Get("ForwardDeadLetteredMessagesTo", ManagementClient.SbNs), this.ForwardDeadLetteredMessagesTo),
                            new XElement(XName.Get("EnablePartitioning", ManagementClient.SbNs), XmlConvert.ToString(this.EnablePartitioning)),
                            new XElement(XName.Get("EnableBatchedOperations", ManagementClient.SbNs), XmlConvert.ToString(this.EnableBatchedOperations))
                        ))
                    ));

            return doc;
        }
    }
}
