using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class TopicDescription : IEquatable<TopicDescription>
    {
        public TopicDescription(string path)
        {
            this.Path = path;
        }

        public TimeSpan DefaultMessageTimeToLive { get; set; }

        public TimeSpan AutoDeleteOnIdle { get; set; }

        public long MaxSizeInMegabytes { get; set; }

        public bool RequiresDuplicateDetection { get; set; }

        public TimeSpan DuplicateDetectionHistoryTimeWindow { get; set; }

        public string Path { get; set; }

        public AuthorizationRules Authorization { get; set; }

        public EntityStatus Status { get; set; }

        public bool EnablePartitioning { get; set; }

        public bool SupportOrdering { get; set; }

        public bool EnableBatchedOperations { get; set; }

        static internal TopicDescription ParseFromContent(string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "entry")
                {
                    return ParseFromEntryElement(xDoc);
                }
            }

            // TODO: Log
            throw new MessagingEntityNotFoundException("Topic was not found");
        }

        static internal IList<TopicDescription> ParseCollectionFromContent(string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "feed")
                {
                    var topicList = new List<TopicDescription>();

                    var entryList = xDoc.Elements(XName.Get("entry", ManagementClient.AtomNs));
                    foreach (var entry in entryList)
                    {
                        topicList.Add(ParseFromEntryElement(entry));
                    }

                    return topicList;
                }
            }

            throw new MessagingEntityNotFoundException("Topic was not found");
        }

        // TODO: Authorization
        static private TopicDescription ParseFromEntryElement(XElement xEntry)
        {
            try
            {
                var name = xEntry.Element(XName.Get("title", ManagementClient.AtomNs)).Value;
                var topicDesc = new TopicDescription(name);

                var qdXml = xEntry.Element(XName.Get("content", ManagementClient.AtomNs))
                    .Element(XName.Get("TopicDescription", ManagementClient.SbNs));

                if (qdXml == null)
                {
                    throw new MessagingEntityNotFoundException("Topic was not found");
                }

                foreach (var element in qdXml.Elements())
                {
                    switch (element.Name.LocalName)
                    {
                        case "MaxSizeInMegabytes":
                            topicDesc.MaxSizeInMegabytes = long.Parse(element.Value);
                            break;
                        case "RequiresDuplicateDetection":
                            topicDesc.RequiresDuplicateDetection = bool.Parse(element.Value);
                            break;
                        case "DuplicateDetectionHistoryTimeWindow":
                            topicDesc.DuplicateDetectionHistoryTimeWindow = XmlConvert.ToTimeSpan(element.Value);
                            break;
                        case "DefaultMessageTimeToLive":
                            topicDesc.DefaultMessageTimeToLive = XmlConvert.ToTimeSpan(element.Value);
                            break;
                        case "EnableBatchedOperations":
                            topicDesc.EnableBatchedOperations = bool.Parse(element.Value);
                            break;
                        case "Status":
                            topicDesc.Status = (EntityStatus)Enum.Parse(typeof(EntityStatus), element.Value);
                            break;
                        case "AutoDeleteOnIdle":
                            topicDesc.AutoDeleteOnIdle = XmlConvert.ToTimeSpan(element.Value);
                            break;
                        case "EnablePartitioning":
                            topicDesc.EnablePartitioning = bool.Parse(element.Value);
                            break;
                        case "SupportOrdering":
                            topicDesc.SupportOrdering = bool.Parse(element.Value);
                            break;
                    }
                }

                return topicDesc;
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
                        new XElement(XName.Get("TopicDescription", ManagementClient.SbNs),
                            new XElement(XName.Get("MaxSizeInMegabytes", ManagementClient.SbNs), XmlConvert.ToString(this.MaxSizeInMegabytes)),
                            new XElement(XName.Get("RequiresDuplicateDetection", ManagementClient.SbNs), XmlConvert.ToString(this.RequiresDuplicateDetection)),
                            this.DefaultMessageTimeToLive != TimeSpan.MaxValue ? new XElement(XName.Get("DefaultMessageTimeToLive", ManagementClient.SbNs), XmlConvert.ToString(this.DefaultMessageTimeToLive)) : null,
                            this.AutoDeleteOnIdle != TimeSpan.MaxValue ? new XElement(XName.Get("AutoDeleteOnIdle", ManagementClient.SbNs), XmlConvert.ToString(this.AutoDeleteOnIdle)) : null,
                            this.RequiresDuplicateDetection && this.DuplicateDetectionHistoryTimeWindow != default ?
                                new XElement(XName.Get("DuplicateDetectionHistoryTimeWindow", ManagementClient.SbNs), XmlConvert.ToString(this.DuplicateDetectionHistoryTimeWindow))
                                : null,
                            new XElement(XName.Get("Status", ManagementClient.SbNs), this.Status.ToString()),
                            new XElement(XName.Get("EnablePartitioning", ManagementClient.SbNs), XmlConvert.ToString(this.EnablePartitioning)),
                            new XElement(XName.Get("EnableBatchedOperations", ManagementClient.SbNs), XmlConvert.ToString(this.EnableBatchedOperations)),
                            new XElement(XName.Get("SupportOrdering", ManagementClient.SbNs), XmlConvert.ToString(this.SupportOrdering))
                        ))
                    ));

            return doc;
        }

        public bool Equals(TopicDescription other)
        {
            if (this.Path.Equals(other.Path, StringComparison.OrdinalIgnoreCase)
                && this.AutoDeleteOnIdle.Equals(other.AutoDeleteOnIdle)
                && this.DefaultMessageTimeToLive.Equals(other.DefaultMessageTimeToLive)
                && this.DuplicateDetectionHistoryTimeWindow.Equals(other.DuplicateDetectionHistoryTimeWindow)
                && this.EnableBatchedOperations == other.EnableBatchedOperations
                && this.EnablePartitioning == other.EnablePartitioning
                && this.MaxSizeInMegabytes == other.MaxSizeInMegabytes
                && this.RequiresDuplicateDetection.Equals(other.RequiresDuplicateDetection)
                && this.Status.Equals(other.Status))
            {
                return true;
            }

            return false;
        }
    }
}
