using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.ServiceBus.Management
{
    internal class ManagementConstants
    {
        public const int QueueNameMaximumLength = 260;
        public const int TopicNameMaximumLength = 260;
        public const int SubscriptionNameMaximumLength = 50;
        public const int RuleNameMaximumLength = 50;

        internal const string AtomNs = "http://www.w3.org/2005/Atom";
        internal const string SbNs = "http://schemas.microsoft.com/netservices/2010/10/servicebus/connect";
        internal const string XmlSchemaNs = "http://www.w3.org/2001/XMLSchema-instance";
        internal const string AtomContentType = "application/atom+xml";
        internal const string apiVersionQuery = "api-version=" + ApiVersion;
        internal const string ApiVersion = "2017-04";
    }
}
