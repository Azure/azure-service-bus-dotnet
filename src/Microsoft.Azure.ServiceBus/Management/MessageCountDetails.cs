// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Management
{
    public class MessageCountDetails
    {
        public long ActiveMessageCount { get; set; }

        public long DeadLetterMessageCount { get; set; }

        public long ScheduledMessageCount { get; set; }

        public long TransferMessageCount { get; set; }

        public long TransferDeadLetterMessageCount { get; set; }
    }
}
