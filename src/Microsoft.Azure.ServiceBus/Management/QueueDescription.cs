using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class QueueDescription : IEquatable<QueueDescription>
    {
        string path;
        TimeSpan lockDuration = TimeSpan.FromSeconds(60);
        long maxSizeInGB = 1;
        TimeSpan defaultMessageTimeToLive = TimeSpan.MaxValue;
        TimeSpan autoDeleteOnIdle = TimeSpan.MaxValue;
        TimeSpan duplicateDetectionHistoryTimeWindow = TimeSpan.FromSeconds(30);
        int maxDeliveryCount = 10;
        string forwardTo = null;
        string forwardDeadLetteredMessagesTo = null;

        public QueueDescription(string path)
        {
            this.Path = path;
        }

        public string Path
        {
            get => this.path;
            set
            {
                ManagementClient.CheckValidQueueName(value, nameof(Path));
                this.path = value;
            }
        }

        public TimeSpan LockDuration
        {
            get => this.lockDuration;
            set
            {
                TimeoutHelper.ThrowIfNonPositiveArgument(value, nameof(LockDuration));
                this.lockDuration = value;
            }
        }

        /// <summary>
        /// Allowed values: 1-5
        /// </summary>
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

        public bool RequiresSession { get; set; } = false;

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

        public bool EnableDeadLetteringOnMessageExpiration { get; set; } = false;

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

        public int MaxDeliveryCount
        {
            get => this.maxDeliveryCount;
            set
            {
                if (value < ManagementConstants.MinAllowedMaxDeliveryCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaxDeliveryCount),
                        $"The value must be greater than {ManagementConstants.MinAllowedMaxDeliveryCount}");
                }

                this.maxDeliveryCount = value;
            }
        }

        public bool EnableBatchedOperations { get; set; } = true;

        public AuthorizationRules AuthorizationRules { get; internal set; } = null;

        public EntityStatus Status { get; set; } = EntityStatus.Active;

        public string ForwardTo
        {
            get => this.forwardTo;
            set
            {
                ManagementClient.CheckValidQueueName(value, nameof(ForwardTo));
                if (this.path.Equals(value, StringComparison.CurrentCultureIgnoreCase))
                {
                    throw new InvalidOperationException("Entity cannot have auto-forwarding policy to itself");
                }

                this.forwardTo = value;
            }
        }

        public string ForwardDeadLetteredMessagesTo
        {
            get => this.forwardDeadLetteredMessagesTo;
            set
            {
                ManagementClient.CheckValidQueueName(value, nameof(ForwardDeadLetteredMessagesTo));
                if (this.path.Equals(value, StringComparison.CurrentCultureIgnoreCase))
                {
                    throw new InvalidOperationException("Entity cannot have auto-forwarding policy to itself");
                }

                this.forwardDeadLetteredMessagesTo = value;
            }
        }

        public bool EnablePartitioning { get; set; } = false;

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

                    var entryList = xDoc.Elements(XName.Get("entry", ManagementConstants.AtomNs));
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
                var name = xEntry.Element(XName.Get("title", ManagementConstants.AtomNs)).Value;
                var qd = new QueueDescription(name);

                var qdXml = xEntry.Element(XName.Get("content", ManagementConstants.AtomNs))?
                    .Element(XName.Get("QueueDescription", ManagementConstants.SbNs));

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
                            qd.MaxSizeInGB = long.Parse(element.Value) / 1024;
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
                        case "ForwardTo":
                            if (!string.IsNullOrWhiteSpace(element.Value))
                            {
                                qd.ForwardTo = element.Value;
                            }
                            break;
                        case "ForwardDeadLetteredMessagesTo":
                            if (!string.IsNullOrWhiteSpace(element.Value))
                            {
                                qd.ForwardDeadLetteredMessagesTo = element.Value;
                            }
                            break;
                    }
                }

                return qd;
            }
            catch (Exception ex) when (!(ex is ServiceBusException))
            {
                throw new ServiceBusException(false, ex);
            }
        }

        internal void NormalizeDescription(string baseAddress)
        {
            if (!string.IsNullOrWhiteSpace(this.ForwardTo))
            {
                this.ForwardTo = NormalizeForwardToAddress(this.ForwardTo, baseAddress);
            }

            if (!string.IsNullOrWhiteSpace(this.ForwardDeadLetteredMessagesTo))
            {
                this.ForwardDeadLetteredMessagesTo = NormalizeForwardToAddress(this.ForwardDeadLetteredMessagesTo, baseAddress);
            }
        }

        private static string NormalizeForwardToAddress(string forwardTo, string baseAddress)
        {
            Uri forwardToUri;
            if (!Uri.TryCreate(forwardTo, UriKind.Absolute, out forwardToUri))
            {
                if (!baseAddress.EndsWith("/", StringComparison.Ordinal))
                {
                    baseAddress += "/";
                }

                forwardToUri = new Uri(new Uri(baseAddress), forwardTo);
            }

            return forwardToUri.AbsoluteUri;
        }

        // TODO: Authorization rules
        internal XDocument Serialize()
        {
            XDocument doc = new XDocument(
                new XElement(XName.Get("entry", ManagementConstants.AtomNs),
                    new XElement(XName.Get("content", ManagementConstants.AtomNs),
                        new XAttribute("type", "application/xml"),
                        new XElement(XName.Get("QueueDescription",ManagementConstants.SbNs),
                            new XElement(XName.Get("LockDuration", ManagementConstants.SbNs), XmlConvert.ToString(this.LockDuration)),
                            new XElement(XName.Get("MaxSizeInMegabytes", ManagementConstants.SbNs), XmlConvert.ToString(this.MaxSizeInGB * 1024)),
                            new XElement(XName.Get("RequiresDuplicateDetection", ManagementConstants.SbNs), XmlConvert.ToString(this.RequiresDuplicateDetection)),
                            new XElement(XName.Get("RequiresSession", ManagementConstants.SbNs), XmlConvert.ToString(this.RequiresSession)),
                            this.DefaultMessageTimeToLive != TimeSpan.MaxValue ? new XElement(XName.Get("DefaultMessageTimeToLive", ManagementConstants.SbNs), XmlConvert.ToString(this.DefaultMessageTimeToLive)) : null,
                            this.AutoDeleteOnIdle != TimeSpan.MaxValue ? new XElement(XName.Get("AutoDeleteOnIdle", ManagementConstants.SbNs), XmlConvert.ToString(this.AutoDeleteOnIdle)) : null,
                            new XElement(XName.Get("DeadLetteringOnMessageExpiration", ManagementConstants.SbNs), XmlConvert.ToString(this.EnableDeadLetteringOnMessageExpiration)),
                            this.RequiresDuplicateDetection && this.DuplicateDetectionHistoryTimeWindow != default ? 
                                new XElement(XName.Get("DuplicateDetectionHistoryTimeWindow", ManagementConstants.SbNs), XmlConvert.ToString(this.DuplicateDetectionHistoryTimeWindow)) 
                                : null,
                            new XElement(XName.Get("MaxDeliveryCount", ManagementConstants.SbNs), XmlConvert.ToString(this.MaxDeliveryCount)),
                            new XElement(XName.Get("Status", ManagementConstants.SbNs), this.Status.ToString()),
                            this.ForwardTo != null ? new XElement(XName.Get("ForwardTo", ManagementConstants.SbNs), this.ForwardTo) : null,
                            this.ForwardDeadLetteredMessagesTo != null ? new XElement(XName.Get("ForwardDeadLetteredMessagesTo", ManagementConstants.SbNs), this.ForwardDeadLetteredMessagesTo) : null,
                            new XElement(XName.Get("EnablePartitioning", ManagementConstants.SbNs), XmlConvert.ToString(this.EnablePartitioning)),
                            new XElement(XName.Get("EnableBatchedOperations", ManagementConstants.SbNs), XmlConvert.ToString(this.EnableBatchedOperations))
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
                && this.MaxSizeInGB == other.MaxSizeInGB
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
