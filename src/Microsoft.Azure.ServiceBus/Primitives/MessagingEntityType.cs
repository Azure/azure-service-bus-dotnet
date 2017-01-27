﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    public enum MessagingEntityType
    {
        Queue = 0,
        Topic = 1,
        Subscriber = 2,
        Filter = 3,
        Namespace = 4,
        Unknown = 0x7FFFFFFE,
    }
}