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
        TimeSpan defaultMessageTimeToLive = TimeSpan.MaxValue;
        TimeSpan autoDeleteOnIdle = TimeSpan.MaxValue;
        TimeSpan duplicateDetectionHistoryTimeWindow = TimeSpan.FromSeconds(30);
        int maxDeliveryCount = 10;
        string forwardTo = null;
        string forwardDeadLetteredMessagesTo = null;
        AuthorizationRules authorizationRules = null;

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

        public long MaxSizeInMB { get; set; } = 1024;

        public bool RequiresDuplicateDetection { get; set; } = false;

        public bool RequiresSession { get; set; } = false;

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

        public bool EnableDeadLetteringOnMessageExpiration { get; set; } = false;

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

        public int MaxDeliveryCount
        {
            get => this.maxDeliveryCount;
            set
            {
                if (value < ManagementClientConstants.MinAllowedMaxDeliveryCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaxDeliveryCount),
                        $"The value must be greater than {ManagementClientConstants.MinAllowedMaxDeliveryCount}");
                }

                this.maxDeliveryCount = value;
            }
        }

        public bool EnableBatchedOperations { get; set; } = true;

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

        public string ForwardTo
        {
            get => this.forwardTo;
            set
            {
                if (value == null)
                {
                    this.forwardTo = value;
                    return;
                }

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
                if (value == null)
                {
                    this.forwardDeadLetteredMessagesTo = value;
                    return;
                }

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

                    var entryList = xDoc.Elements(XName.Get("entry", ManagementClientConstants.AtomNs));
                    foreach (var entry in entryList)
                    {
                        queueList.Add(ParseFromEntryElement(entry));
                    }

                    return queueList;
                }
            }

            throw new MessagingEntityNotFoundException("Queue was not found");
        }

        // TODO: Revisit all properties and ensure they are populated.
        static private QueueDescription ParseFromEntryElement(XElement xEntry)
        {
            try
            {
                var name = xEntry.Element(XName.Get("title", ManagementClientConstants.AtomNs)).Value;
                var qd = new QueueDescription(name);

                var qdXml = xEntry.Element(XName.Get("content", ManagementClientConstants.AtomNs))?
                    .Element(XName.Get("QueueDescription", ManagementClientConstants.SbNs));

                if (qdXml == null)
                {
                    throw new MessagingEntityNotFoundException("Queue was not found");
                }

                foreach (var element in qdXml.Elements())
                {
                    switch (element.Name.LocalName)
                    {
                        case "MaxSizeInMegabytes":
                            qd.MaxSizeInMB = long.Parse(element.Value);
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
                        case "AuthorizationRules":
                            qd.AuthorizationRules = AuthorizationRules.ParseFromXElement(element);
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

        internal XDocument Serialize()
        {
            XDocument doc = new XDocument(
                new XElement(XName.Get("entry", ManagementClientConstants.AtomNs),
                    new XElement(XName.Get("content", ManagementClientConstants.AtomNs),
                        new XAttribute("type", "application/xml"),
                        new XElement(XName.Get("QueueDescription",ManagementClientConstants.SbNs),
                            new XElement(XName.Get("LockDuration", ManagementClientConstants.SbNs), XmlConvert.ToString(this.LockDuration)),
                            new XElement(XName.Get("MaxSizeInMegabytes", ManagementClientConstants.SbNs), XmlConvert.ToString(this.MaxSizeInMB)),
                            new XElement(XName.Get("RequiresDuplicateDetection", ManagementClientConstants.SbNs), XmlConvert.ToString(this.RequiresDuplicateDetection)),
                            new XElement(XName.Get("RequiresSession", ManagementClientConstants.SbNs), XmlConvert.ToString(this.RequiresSession)),
                            this.DefaultMessageTimeToLive != TimeSpan.MaxValue ? new XElement(XName.Get("DefaultMessageTimeToLive", ManagementClientConstants.SbNs), XmlConvert.ToString(this.DefaultMessageTimeToLive)) : null,
                            new XElement(XName.Get("DeadLetteringOnMessageExpiration", ManagementClientConstants.SbNs), XmlConvert.ToString(this.EnableDeadLetteringOnMessageExpiration)),
                            this.RequiresDuplicateDetection && this.DuplicateDetectionHistoryTimeWindow != default ?
                                new XElement(XName.Get("DuplicateDetectionHistoryTimeWindow", ManagementClientConstants.SbNs), XmlConvert.ToString(this.DuplicateDetectionHistoryTimeWindow))
                                : null,
                            new XElement(XName.Get("MaxDeliveryCount", ManagementClientConstants.SbNs), XmlConvert.ToString(this.MaxDeliveryCount)),
                            new XElement(XName.Get("EnableBatchedOperations", ManagementClientConstants.SbNs), XmlConvert.ToString(this.EnableBatchedOperations)),
                            this.authorizationRules != null ? this.AuthorizationRules.Serialize() : null,
                            new XElement(XName.Get("Status", ManagementClientConstants.SbNs), this.Status.ToString()),
                            this.ForwardTo != null ? new XElement(XName.Get("ForwardTo", ManagementClientConstants.SbNs), this.ForwardTo) : null,
                            this.AutoDeleteOnIdle != TimeSpan.MaxValue ? new XElement(XName.Get("AutoDeleteOnIdle", ManagementClientConstants.SbNs), XmlConvert.ToString(this.AutoDeleteOnIdle)) : null,
                            new XElement(XName.Get("EnablePartitioning", ManagementClientConstants.SbNs), XmlConvert.ToString(this.EnablePartitioning)),
                            this.ForwardDeadLetteredMessagesTo != null ? new XElement(XName.Get("ForwardDeadLetteredMessagesTo", ManagementClientConstants.SbNs), this.ForwardDeadLetteredMessagesTo) : null
                        ))
                    ));

            return doc;
        }

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
                && this.MaxSizeInMB == other.MaxSizeInMB
                && this.RequiresDuplicateDetection.Equals(other.RequiresDuplicateDetection)
                && this.RequiresSession.Equals(other.RequiresSession)
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
