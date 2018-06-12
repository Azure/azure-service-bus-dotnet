using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class TopicDescription : IEquatable<TopicDescription>
    {
        string path;
        long maxSizeInGB = 1;
        TimeSpan defaultMessageTimeToLive = TimeSpan.MaxValue;
        TimeSpan autoDeleteOnIdle = TimeSpan.MaxValue;
        TimeSpan duplicateDetectionHistoryTimeWindow = TimeSpan.FromSeconds(30);

        public TopicDescription(string path)
        {
            this.Path = path;
        }

        public TimeSpan DefaultMessageTimeToLive
        {
            get => this.defaultMessageTimeToLive;
            set
            {
                if (value < ManagementConstants.MinimumAllowedTimeToLive || value > ManagementConstants.MaximumAllowedTimeToLive)
                {
                    throw new ArgumentOutOfRangeException(nameof(DefaultMessageTimeToLive),
                        $"The value must be between {ManagementConstants.MinimumAllowedTimeToLive} and {ManagementConstants.MaximumAllowedTimeToLive}");
                }

                this.defaultMessageTimeToLive = value;
            }
        }

        public TimeSpan AutoDeleteOnIdle
        {
            get => this.autoDeleteOnIdle;
            set
            {
                if (value < ManagementConstants.MinimumAllowedAutoDeleteOnIdle)
                {
                    throw new ArgumentOutOfRangeException(nameof(AutoDeleteOnIdle),
                        $"The value must be greater than {ManagementConstants.MinimumAllowedAutoDeleteOnIdle}");
                }

                this.autoDeleteOnIdle = value;
            }
        }

        public long MaxSizeInGB
        {
            get => this.maxSizeInGB;
            set
            {
                //if (value < ManagementConstants.MinAllowedMaxEntitySizeInGB || value > ManagementConstants.MaxAllowedMaxEntitySizeInGB)
                //{
                //    throw new ArgumentOutOfRangeException(nameof(MaxSizeInGB),
                //        $"The value must be between {ManagementConstants.MinAllowedMaxEntitySizeInGB} and {ManagementConstants.MaxAllowedMaxEntitySizeInGB}");
                //}

                this.maxSizeInGB = value;
            }
        }

        public bool RequiresDuplicateDetection { get; set; } = false;

        public TimeSpan DuplicateDetectionHistoryTimeWindow
        {
            get => this.duplicateDetectionHistoryTimeWindow;
            set
            {
                if (value < ManagementConstants.MinimumDuplicateDetectionHistoryTimeWindow || value > ManagementConstants.MaximumDuplicateDetectionHistoryTimeWindow)
                {
                    throw new ArgumentOutOfRangeException(nameof(DuplicateDetectionHistoryTimeWindow),
                        $"The value must be between {ManagementConstants.MinimumDuplicateDetectionHistoryTimeWindow} and {ManagementConstants.MaximumDuplicateDetectionHistoryTimeWindow}");
                }

                this.duplicateDetectionHistoryTimeWindow = value;
            }
        }

        public string Path
        {
            get => this.path;
            set
            {
                ManagementClient.CheckValidTopicName(value, nameof(Path));
                this.path = value;
            }
        }

        public AuthorizationRules Authorization { get; set; }

        public EntityStatus Status { get; set; } = EntityStatus.Active;

        public bool EnablePartitioning { get; set; } = false;

        public bool SupportOrdering { get; set; } = false;

        public bool EnableBatchedOperations { get; set; } = true;

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

                    var entryList = xDoc.Elements(XName.Get("entry", ManagementConstants.AtomNs));
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
                var name = xEntry.Element(XName.Get("title", ManagementConstants.AtomNs)).Value;
                var topicDesc = new TopicDescription(name);

                var qdXml = xEntry.Element(XName.Get("content", ManagementConstants.AtomNs))?
                    .Element(XName.Get("TopicDescription", ManagementConstants.SbNs));

                if (qdXml == null)
                {
                    throw new MessagingEntityNotFoundException("Topic was not found");
                }

                foreach (var element in qdXml.Elements())
                {
                    switch (element.Name.LocalName)
                    {
                        case "MaxSizeInMegabytes":
                            topicDesc.MaxSizeInGB = long.Parse(element.Value) / 1024;
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
            catch (Exception ex) when (!(ex is ServiceBusException))
            {
                throw new ServiceBusException(false, ex);
            }
        }

        // TODO: Authorization rules
        internal XDocument Serialize()
        {
            XDocument doc = new XDocument(
                new XElement(XName.Get("entry", ManagementConstants.AtomNs),
                    new XElement(XName.Get("content", ManagementConstants.AtomNs),
                        new XAttribute("type", "application/xml"),
                        new XElement(XName.Get("TopicDescription", ManagementConstants.SbNs),
                            new XElement(XName.Get("MaxSizeInMegabytes", ManagementConstants.SbNs), XmlConvert.ToString(this.MaxSizeInGB * 1024)),
                            new XElement(XName.Get("RequiresDuplicateDetection", ManagementConstants.SbNs), XmlConvert.ToString(this.RequiresDuplicateDetection)),
                            this.DefaultMessageTimeToLive != TimeSpan.MaxValue ? new XElement(XName.Get("DefaultMessageTimeToLive", ManagementConstants.SbNs), XmlConvert.ToString(this.DefaultMessageTimeToLive)) : null,
                            this.AutoDeleteOnIdle != TimeSpan.MaxValue ? new XElement(XName.Get("AutoDeleteOnIdle", ManagementConstants.SbNs), XmlConvert.ToString(this.AutoDeleteOnIdle)) : null,
                            this.RequiresDuplicateDetection && this.DuplicateDetectionHistoryTimeWindow != default ?
                                new XElement(XName.Get("DuplicateDetectionHistoryTimeWindow", ManagementConstants.SbNs), XmlConvert.ToString(this.DuplicateDetectionHistoryTimeWindow))
                                : null,
                            new XElement(XName.Get("Status", ManagementConstants.SbNs), this.Status.ToString()),
                            new XElement(XName.Get("EnablePartitioning", ManagementConstants.SbNs), XmlConvert.ToString(this.EnablePartitioning)),
                            new XElement(XName.Get("EnableBatchedOperations", ManagementConstants.SbNs), XmlConvert.ToString(this.EnableBatchedOperations)),
                            new XElement(XName.Get("SupportOrdering", ManagementConstants.SbNs), XmlConvert.ToString(this.SupportOrdering))
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
                && this.MaxSizeInGB == other.MaxSizeInGB
                && this.RequiresDuplicateDetection.Equals(other.RequiresDuplicateDetection)
                && this.Status.Equals(other.Status))
            {
                return true;
            }

            return false;
        }
    }
}
