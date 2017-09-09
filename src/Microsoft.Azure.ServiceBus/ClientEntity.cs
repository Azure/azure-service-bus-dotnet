﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Core;

    /// <summary>
    /// Contract for all client entities with Open-Close/Abort state m/c
    /// main-purpose: closeAll related entities
    /// </summary>
    public abstract class ClientEntity : IClientEntity
    {
        static int nextId;
        readonly string clientTypeName;
        readonly object syncLock;

        protected ClientEntity(string clientTypeName, string postfix, RetryPolicy retryPolicy)
        {
            this.clientTypeName = clientTypeName;
            ClientId = GenerateClientId(clientTypeName, postfix);
            RetryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
            syncLock = new object();
        }

        /// <summary>
        /// Returns true if the client is closed or closing.
        /// </summary>
        public bool IsClosedOrClosing
        {
            get;
            internal set;
        }

        /// <summary>
        /// Duration after which individual operations will timeout.
        /// </summary>
        public abstract TimeSpan OperationTimeout { get; set; }

        /// <summary>
        /// Gets the ID to identify this client. This can be used to correlate logs and exceptions.
        /// </summary>
        /// <remarks>Every new client has a unique ID (in that process).</remarks>
        public string ClientId { get; private set; }

        /// <summary>
        /// Gets the <see cref="ServiceBus.RetryPolicy"/> defined on the client.
        /// </summary>
        public RetryPolicy RetryPolicy { get; }

        /// <summary>
        /// Closes the Client. Closes the connections opened by it.
        /// </summary>
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

        /// <summary>
        /// Gets a list of currently registered plugins for this client.
        /// </summary>
        public abstract IList<ServiceBusPlugin> RegisteredPlugins { get; }

        /// <summary>
        /// Registers a <see cref="ServiceBusPlugin"/> to be used with this client.
        /// </summary>
        /// <param name="serviceBusPlugin">The <see cref="ServiceBusPlugin"/> to register.</param>
        public abstract void RegisterPlugin(ServiceBusPlugin serviceBusPlugin);

        /// <summary>
        /// Unregisters a <see cref="ServiceBusPlugin"/>.
        /// </summary>
        /// <param name="serviceBusPluginName">The name <see cref="ServiceBusPlugin.Name"/> to be unregistered</param>
        public abstract void UnregisterPlugin(string serviceBusPluginName);

        protected abstract Task OnClosingAsync();

        protected static long GetNextId()
        {
            return Interlocked.Increment(ref nextId);
        }

        /// <summary>
        /// Generates a new client id that can be used to identify a specific client in logs and error messages.
        /// </summary>
        /// <param name="clientTypeName">The type of the client.</param>
        /// <param name="postfix">Information that can be appended by the client.</param>
        protected static string GenerateClientId(string clientTypeName, string postfix = "")
        {
            return $"{clientTypeName}{GetNextId()}{postfix}";
        }

        /// <summary>
        /// Throw an OperationCanceledException if the object is Closing.
        /// </summary>
        protected virtual void ThrowIfClosed()
        {
            if (IsClosedOrClosing)
            {
                throw new ObjectDisposedException($"{clientTypeName} with Id '{ClientId}' has already been closed. Please create a new {clientTypeName}.");
            }
        }

        /// <summary>
        /// Updates the client id.
        /// </summary>
        internal void UpdateClientId(string newClientId)
        {
            MessagingEventSource.Log.UpdateClientId(ClientId, newClientId);
            ClientId = newClientId;
        }
    }
}