// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Filters
{
    interface IErrorHandler
    {
        void AddError(string message, string token, int line, int column, int length, int severity);
    }
}