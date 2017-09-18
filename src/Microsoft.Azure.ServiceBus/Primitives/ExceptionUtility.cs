// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Primitives
{
    using System;

    class ExceptionUtility
    {
        public Exception AsError(Exception exception)
        {
            return exception;
        }
    }
}