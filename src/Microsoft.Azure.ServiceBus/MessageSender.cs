// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    abstract class MessageSender : ClientEntity
    {
        readonly TimeSpan operationTimeout;

        protected MessageSender(TimeSpan operationTimeout)
            : base(nameof(MessageSender) + StringUtility.GetRandomString())
        {
            this.operationTimeout = operationTimeout;
        }

        internal TimeSpan OperationTimeout
        {
            get { return this.operationTimeout; }
        }

        public Task SendAsync(IEnumerable<BrokeredMessage> brokeredMessages)
        {
            MessageSender.ValidateMessages(brokeredMessages);
            return this.OnSendAsync(brokeredMessages);
        }

        protected abstract Task OnSendAsync(IEnumerable<BrokeredMessage> brokeredMessages);

        static void ValidateMessages(IEnumerable<BrokeredMessage> brokeredMessages)
        {
            if (brokeredMessages == null || !brokeredMessages.Any())
            {
                throw Fx.Exception.ArgumentNull("brokeredMessages");
            }

            if (brokeredMessages.Any(brokeredMessage => brokeredMessage.IsLockTokenSet))
            {
                throw Fx.Exception.Argument(nameof(brokeredMessages), "Cannot Send ReceivedMessages");
            }
        }
    }
}
