// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus.Filters;

namespace Microsoft.Azure.ServiceBus.Core
{
    internal interface IInnerSubscriptionClient
    {
        MessageReceiver InnerReceiver { get; }

        int PrefetchCount { get; set; }

        Task OnAddRuleAsync(RuleDescription description);

        Task OnRemoveRuleAsync(string ruleName);

        Task CloseAsync();
    }
}