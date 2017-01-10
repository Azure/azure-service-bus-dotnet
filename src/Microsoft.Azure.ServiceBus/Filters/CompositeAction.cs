// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Filters
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /* public */ class CompositeAction : RuleAction, IEnumerable<RuleAction>
    {
        [DataMember(Name = "Actions", EmitDefaultValue = false, IsRequired = true, Order = 0x10001)]
        readonly List<RuleAction> actions;

        public CompositeAction()
        {
            this.actions = new List<RuleAction>();
        }

        public CompositeAction(IEnumerable<RuleAction> actions)
        {
            this.actions = new List<RuleAction>(actions);
        }

        public void Add(RuleAction action)
        {
            this.actions.Add(action);
        }

        public IEnumerator<RuleAction> GetEnumerator()
        {
            return this.actions.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.actions.GetEnumerator();
        }
    }
}