// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IMessageReceiver
    {
        string Path { get; }

        ReceiveMode ReceiveMode { get; }

        int PrefetchCount { get; set; }

        long LastPeekedSequenceNumber { get; }

        Task<BrokeredMessage> ReceiveAsync();

        Task<IList<BrokeredMessage>> ReceiveAsync(int maxMessageCount);

        Task<IList<BrokeredMessage>> ReceiveBySequenceNumberAsync(IEnumerable<long> sequenceNumbers);

        Task CompleteAsync(IEnumerable<Guid> lockTokens);

        Task AbandonAsync(Guid lockToken);

        Task DeferAsync(Guid lockToken);

        Task DeadLetterAsync(Guid lockToken);

        Task<DateTime> RenewLockAsync(Guid lockToken);

        /// <summary>
        /// Asynchronously reads the next message without changing the state of the receiver or the message source.
        /// </summary>
        /// <returns>The asynchronous operation that returns the <see cref="Microsoft.Azure.ServiceBus.BrokeredMessage" /> that represents the next message to be read.</returns>
        Task<BrokeredMessage> PeekAsync();

        /// <summary>
        /// Asynchronously reads the next batch of message without changing the state of the receiver or the message source.
        /// </summary>
        /// <param name="maxMessageCount">The number of messages.</param>
        /// <returns>The asynchronous operation that returns a list of <see cref="Microsoft.Azure.ServiceBus.BrokeredMessage" /> to be read.</returns>
        Task<IList<BrokeredMessage>> PeekAsync(int maxMessageCount);

        /// <summary>
        /// Asynchronously reads the next message without changing the state of the receiver or the message source.
        /// </summary>
        /// <param name="fromSequenceNumber">The sequence number from where to read the message.</param>
        /// <returns>The asynchronous operation that returns the <see cref="Microsoft.Azure.ServiceBus.BrokeredMessage" /> that represents the next message to be read.</returns>
        Task<BrokeredMessage> PeekBySequenceNumberAsync(long fromSequenceNumber);

        /// <summary>Peeks a batch of messages.</summary>
        /// <param name="fromSequenceNumber">The starting point from which to browse a batch of messages.</param>
        /// <param name="messageCount">The number of messages.</param>
        /// <returns>A batch of messages peeked.</returns>
        Task<IList<BrokeredMessage>> PeekBySequenceNumberAsync(long fromSequenceNumber, int messageCount);
    }
}