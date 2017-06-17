// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Amqp;

namespace Microsoft.Azure.ServiceBus.Primitives
{
    /// <summary>
    ///     Provides an adapter from TokenProvider to ICbsTokenProvider for AMQP CBS usage.
    /// </summary>
    internal sealed class TokenProviderAdapter : ICbsTokenProvider
    {
        readonly TimeSpan operationTimeout;
        readonly TokenProvider tokenProvider;

        public TokenProviderAdapter(TokenProvider tokenProvider, TimeSpan operationTimeout)
        {
            Fx.Assert(tokenProvider != null, "tokenProvider cannot be null");
            this.tokenProvider = tokenProvider;
            this.operationTimeout = operationTimeout;
        }

        public async Task<CbsToken> GetTokenAsync(Uri namespaceAddress, string appliesTo, string[] requiredClaims)
        {
            var claim = requiredClaims?.FirstOrDefault();
            var token = await tokenProvider.GetTokenAsync(appliesTo, claim, operationTimeout).ConfigureAwait(false);
            return new CbsToken(token.TokenValue, CbsConstants.ServiceBusSasTokenType, token.ExpiresAtUtc);
        }
    }
}