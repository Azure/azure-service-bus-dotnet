// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Filters
{
    sealed class EmptyRuleAction : RuleAction
    {
        internal static readonly EmptyRuleAction Default = new EmptyRuleAction();
    }
}