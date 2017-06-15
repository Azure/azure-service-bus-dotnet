// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.ServiceBus.Primitives
{
    /// <summary>
    ///     An interface showing the common functionality between all Service Bus clients.
    /// </summary>
    public interface IClientEntity
    {
        /// <summary>
        ///     Get the client ID.
        /// </summary>
        string ClientId { get; }

        /// <summary>
        ///     Determines whether or not the ClientEntity is closed or being closed.
        /// </summary>
        bool IsClosedOrClosing { get; }

        /// <summary>
        ///     Closes the ClientEntity.
        /// </summary>
        /// <returns>The asynchronous operation.</returns>
        Task CloseAsync();
    }
}