// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.ServiceBus.Primitives
{
    /// <summary>
    ///     Contract for all client entities with Open-Close/Abort state m/c
    ///     main-purpose: closeAll related entities
    /// </summary>
    public abstract class ClientEntity : IClientEntity
    {
        static int nextId;
        readonly object syncLock;

        /// <summary></summary>
        /// <param name="clientId"></param>
        /// <param name="retryPolicy"></param>
        protected ClientEntity(string clientId, RetryPolicy retryPolicy)
        {
            if (retryPolicy == null)
            {
                throw new ArgumentNullException(nameof(retryPolicy));
            }

            ClientId = clientId;
            RetryPolicy = retryPolicy;
            syncLock = new object();
        }

        /// <summary>
        ///     Gets the <see cref="RetryPolicy.RetryPolicy" /> for the ClientEntity.
        /// </summary>
        public RetryPolicy RetryPolicy { get; }

        /// <summary>
        ///     Gets or sets the state of closing.
        /// </summary>
        public bool IsClosedOrClosing { get; set; }

        /// <summary>
        ///     Gets the client ID.
        /// </summary>
        public string ClientId { get; }

        /// <summary>
        ///     Closes the ClientEntity.
        /// </summary>
        /// <returns>The asynchronous operation</returns>
        public async Task CloseAsync()
        {
            var callClose = false;
            lock (syncLock)
            {
                if (!IsClosedOrClosing)
                {
                    IsClosedOrClosing = true;
                    callClose = true;
                }
            }

            if (callClose)
            {
                await OnClosingAsync().ConfigureAwait(false);
            }
        }

        /// <summary></summary>
        /// <returns></returns>
        protected abstract Task OnClosingAsync();

        /// <summary></summary>
        /// <returns></returns>
        protected static long GetNextId()
        {
            return Interlocked.Increment(ref nextId);
        }
    }
}