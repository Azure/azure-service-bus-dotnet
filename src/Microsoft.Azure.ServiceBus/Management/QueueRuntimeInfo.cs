// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Management
{
    using System;
    using System.Collections.Generic;
    using System.Xml.Linq;
    public class QueueRuntimeInfo
    {
        public QueueRuntimeInfo(string path)
        {
            this.Path = path;
        }

        public string Path { get; internal set; }

        public long MessageCount { get; internal set; }

        public MessageCountDetails MessageCountDetails { get; internal set; }

        public long SizeInBytes { get; internal set; }

        public DateTime CreatedAt { get; internal set; }

        public DateTime UpdatedAt { get; internal set; }

        public DateTime AccessedAt { get; internal set; }
    }
}
