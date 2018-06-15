﻿using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class TopicDescription : IEquatable<TopicDescription>
    {
        string path;
        TimeSpan defaultMessageTimeToLive = TimeSpan.MaxValue;
        TimeSpan autoDeleteOnIdle = TimeSpan.MaxValue;
        TimeSpan duplicateDetectionHistoryTimeWindow = TimeSpan.FromSeconds(30);
        AuthorizationRules authorizationRules = null;

        public TopicDescription(string path)
        {
            this.Path = path;
        }

        public TimeSpan DefaultMessageTimeToLive
        {
            get => this.defaultMessageTimeToLive;
            set
            {
                if (value < ManagementClientConstants.MinimumAllowedTimeToLive || value > ManagementClientConstants.MaximumAllowedTimeToLive)
                {
                    throw new ArgumentOutOfRangeException(nameof(DefaultMessageTimeToLive),
                        $"The value must be between {ManagementClientConstants.MinimumAllowedTimeToLive} and {ManagementClientConstants.MaximumAllowedTimeToLive}");
                }

                this.defaultMessageTimeToLive = value;
            }
        }

        public TimeSpan AutoDeleteOnIdle
        {
            get => this.autoDeleteOnIdle;
            set
            {
                if (value < ManagementClientConstants.MinimumAllowedAutoDeleteOnIdle)
                {
                    throw new ArgumentOutOfRangeException(nameof(AutoDeleteOnIdle),
                        $"The value must be greater than {ManagementClientConstants.MinimumAllowedAutoDeleteOnIdle}");
                }

                this.autoDeleteOnIdle = value;
            }
        }

        public long MaxSizeInMB { get; set; } = 1024;

        public bool RequiresDuplicateDetection { get; set; } = false;

        public TimeSpan DuplicateDetectionHistoryTimeWindow
        {
            get => this.duplicateDetectionHistoryTimeWindow;
            set
            {
                if (value < ManagementClientConstants.MinimumDuplicateDetectionHistoryTimeWindow || value > ManagementClientConstants.MaximumDuplicateDetectionHistoryTimeWindow)
                {
                    throw new ArgumentOutOfRangeException(nameof(DuplicateDetectionHistoryTimeWindow),
                        $"The value must be between {ManagementClientConstants.MinimumDuplicateDetectionHistoryTimeWindow} and {ManagementClientConstants.MaximumDuplicateDetectionHistoryTimeWindow}");
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

        public AuthorizationRules AuthorizationRules
        {
            get
            {
                if (this.authorizationRules == null)
                {
                    this.authorizationRules = new AuthorizationRules();
                }

                return this.authorizationRules;
            }
            internal set
            {
                this.authorizationRules = value;
            }
        }

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

        static private TopicDescription ParseFromEntryElement(XElement xEntry)
        {
            try
            {
                var name = xEntry.Element(XName.Get("title", ManagementClientConstants.AtomNs)).Value;
                var topicDesc = new TopicDescription(name);

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
                        case "MaxSizeInMegabytes":
                            topicDesc.MaxSizeInMB = long.Parse(element.Value);
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
                        case "AuthorizationRules":
                            topicDesc.AuthorizationRules = AuthorizationRules.ParseFromXElement(element);
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

        internal XDocument Serialize()
        {
            XDocument doc = new XDocument(
                new XElement(XName.Get("entry", ManagementClientConstants.AtomNs),
                    new XElement(XName.Get("content", ManagementClientConstants.AtomNs),
                        new XAttribute("type", "application/xml"),
                        new XElement(XName.Get("TopicDescription", ManagementClientConstants.SbNs),
                            new XElement(XName.Get("MaxSizeInMegabytes", ManagementClientConstants.SbNs), XmlConvert.ToString(this.MaxSizeInMB)),
                            new XElement(XName.Get("RequiresDuplicateDetection", ManagementClientConstants.SbNs), XmlConvert.ToString(this.RequiresDuplicateDetection)),
                            this.DefaultMessageTimeToLive != TimeSpan.MaxValue ? new XElement(XName.Get("DefaultMessageTimeToLive", ManagementClientConstants.SbNs), XmlConvert.ToString(this.DefaultMessageTimeToLive)) : null,
                            this.AutoDeleteOnIdle != TimeSpan.MaxValue ? new XElement(XName.Get("AutoDeleteOnIdle", ManagementClientConstants.SbNs), XmlConvert.ToString(this.AutoDeleteOnIdle)) : null,
                            this.RequiresDuplicateDetection && this.DuplicateDetectionHistoryTimeWindow != default ?
                                new XElement(XName.Get("DuplicateDetectionHistoryTimeWindow", ManagementClientConstants.SbNs), XmlConvert.ToString(this.DuplicateDetectionHistoryTimeWindow))
                                : null,
                            this.authorizationRules != null ? this.AuthorizationRules.Serialize() : null,
                            new XElement(XName.Get("Status", ManagementClientConstants.SbNs), this.Status.ToString()),
                            new XElement(XName.Get("EnablePartitioning", ManagementClientConstants.SbNs), XmlConvert.ToString(this.EnablePartitioning)),
                            new XElement(XName.Get("EnableBatchedOperations", ManagementClientConstants.SbNs), XmlConvert.ToString(this.EnableBatchedOperations)),
                            new XElement(XName.Get("SupportOrdering", ManagementClientConstants.SbNs), XmlConvert.ToString(this.SupportOrdering))
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
                && this.MaxSizeInMB == other.MaxSizeInMB
                && this.RequiresDuplicateDetection.Equals(other.RequiresDuplicateDetection)
                && this.Status.Equals(other.Status)
                && (this.authorizationRules != null && other.authorizationRules != null
                    || this.authorizationRules == null && other.authorizationRules == null)
                && this.authorizationRules != null && this.AuthorizationRules.Equals(other.AuthorizationRules))
            {
                return true;
            }

            return false;
        }
    }
}
