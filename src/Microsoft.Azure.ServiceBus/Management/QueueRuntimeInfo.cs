using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class QueueRuntimeInfo
    {
        public string Path { get; set; }

        public long MessageCount { get; internal set; }

        public MessageCountDetails MessageCountDetails { get; internal set; }

        public long SizeInBytes { get; internal set; }

        public DateTime CreatedAt { get; internal set; }

        public DateTime UpdatedAt { get; internal set; }

        public DateTime AccessedAt { get; internal set; }

        static internal QueueRuntimeInfo ParseFromContent(string xml)
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

        static internal IList<QueueRuntimeInfo> ParseCollectionFromContent(string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "feed")
                {
                    var queueList = new List<QueueRuntimeInfo>();

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

        static private QueueRuntimeInfo ParseFromEntryElement(XElement xEntry)
        {
            try
            {
                var name = xEntry.Element(XName.Get("title", ManagementClientConstants.AtomNs)).Value;
                var qRuntime = new QueueRuntimeInfo()
                {
                    Path = name
                };

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
                        case "AccessedAt":
                            qRuntime.AccessedAt = DateTime.Parse(element.Value);
                            break;
                        case "CreatedAt":
                            qRuntime.CreatedAt = DateTime.Parse(element.Value);
                            break;
                        case "MessageCount":
                            qRuntime.MessageCount = long.Parse(element.Value);
                            break;
                        case "SizeInBytes":
                            qRuntime.SizeInBytes = long.Parse(element.Value);
                            break;
                        case "UpdatedAt":
                            qRuntime.UpdatedAt = DateTime.Parse(element.Value);
                            break;
                        case "CountDetails":
                            qRuntime.MessageCountDetails = new MessageCountDetails();
                            foreach (var countElement in element.Elements())
                            {
                                switch (countElement.Name.LocalName)
                                {
                                    case "ActiveMessageCount":
                                        qRuntime.MessageCountDetails.ActiveMessageCount = long.Parse(countElement.Value);
                                        break;
                                    case "DeadLetterMessageCount":
                                        qRuntime.MessageCountDetails.DeadLetterMessageCount = long.Parse(countElement.Value);
                                        break;
                                    case "ScheduledMessageCount":
                                        qRuntime.MessageCountDetails.ScheduledMessageCount = long.Parse(countElement.Value);
                                        break;
                                    case "TransferMessageCount":
                                        qRuntime.MessageCountDetails.TransferMessageCount = long.Parse(countElement.Value);
                                        break;
                                    case "TransferDeadLetterMessageCount":
                                        qRuntime.MessageCountDetails.TransferDeadLetterMessageCount = long.Parse(countElement.Value);
                                        break;
                                }
                            }
                            break;
                    }
                }

                return qRuntime;
            }
            catch (Exception ex) when (!(ex is ServiceBusException))
            {
                throw new ServiceBusException(false, ex);
            }
        }
    }
}
