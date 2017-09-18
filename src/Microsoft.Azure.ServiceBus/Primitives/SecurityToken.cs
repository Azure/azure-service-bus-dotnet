// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Primitives
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;

    /// <summary>
    /// Provides information about a security token such as audience, expiry time, and the string token value.
    /// </summary>
    internal class SecurityToken
    {
        // per Simple Web Token draft specification
        private const string TokenAudience = "Audience";
        private const string TokenExpiresOn = "ExpiresOn";
        private const string TokenIssuer = "Issuer";
        private const string TokenDigest256 = "HMACSHA256";

        const string InternalExpiresOnFieldName = "ExpiresOn";
        const string InternalAudienceFieldName = TokenAudience;
        const string InternalKeyValueSeparator = "=";
        const string InternalPairSeparator = "&";
        static readonly Func<string, string> Decoder = WebUtility.UrlDecode;
        static readonly DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        readonly string token;
        readonly DateTime expiresAtUtc;
        readonly string audience;

        /// <summary>
        /// Creates a new instance of the <see cref="SecurityToken"/> class.
        /// </summary>
        /// <param name="expiresAtUtc">The expiration time</param>
        public SecurityToken(string tokenString, DateTime expiresAtUtc, string audience)
        {
            Guard.AgainstNullAndEmpty(nameof(tokenString), tokenString);
            Guard.AgainstNullAndEmpty(nameof(audience), audience);

            this.token = tokenString;
            this.expiresAtUtc = expiresAtUtc;
            this.audience = audience;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SecurityToken"/> class.
        /// </summary>
        /// <param name="expiresAtUtc">The expiration time</param>
        public SecurityToken(string tokenString, DateTime expiresAtUtc)
        {
            Guard.AgainstNullAndEmpty(nameof(tokenString), tokenString);

            this.token = tokenString;
            this.expiresAtUtc = expiresAtUtc;
            this.audience = this.GetAudienceFromToken(tokenString);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SecurityToken"/> class.
        /// </summary>
        public SecurityToken(string tokenString)
        {
            Guard.AgainstNullAndEmpty(nameof(tokenString), tokenString);

            this.token = tokenString;
            this.GetExpirationDateAndAudienceFromToken(tokenString, out this.expiresAtUtc, out this.audience);
        }

        /// <summary>
        /// Gets the audience of this token.
        /// </summary>
        public string Audience => this.audience;

        /// <summary>
        /// Gets the expiration time of this token.
        /// </summary>
        public DateTime ExpiresAtUtc => this.expiresAtUtc;

        /// <summary>
        /// Gets the actual token.
        /// </summary>
        public object TokenValue => this.token;

        protected virtual string ExpiresOnFieldName => InternalExpiresOnFieldName;

        protected virtual string AudienceFieldName => InternalAudienceFieldName;

        protected virtual string KeyValueSeparator => InternalKeyValueSeparator;

        protected virtual string PairSeparator => InternalPairSeparator;

        static IDictionary<string, string> Decode(string encodedString, Func<string, string> keyDecoder, Func<string, string> valueDecoder, string keyValueSeparator, string pairSeparator)
        {
            var dictionary = new Dictionary<string, string>();
            IEnumerable<string> valueEncodedPairs = encodedString.Split(new[] { pairSeparator }, StringSplitOptions.None);
            foreach (var valueEncodedPairAsString in valueEncodedPairs)
            {
                var pair = valueEncodedPairAsString.Split(new[] { keyValueSeparator }, StringSplitOptions.None);
                if (pair.Length != 2)
                {
                    throw new FormatException("The string has an invalid encoding format.");
                }

                dictionary.Add(keyDecoder(pair[0]), valueDecoder(pair[1]));
            }

            return dictionary;
        }

        string GetAudienceFromToken(string token)
        {
            var decodedToken = Decode(token, Decoder, Decoder, this.KeyValueSeparator, this.PairSeparator);
            if (!decodedToken.TryGetValue(this.AudienceFieldName, out var audience))
            {
                throw new FormatException(TokenMissingAudience);
            }

            return audience;
        }

        private const string TokenMissingAudience = "The provided token does not specify the 'Audience' value.";

        void GetExpirationDateAndAudienceFromToken(string token, out DateTime expiresOn, out string audience)
        {
            IDictionary<string, string> decodedToken = Decode(token, Decoder, Decoder, this.KeyValueSeparator, this.PairSeparator);
            if (!decodedToken.TryGetValue(this.ExpiresOnFieldName, out var expiresIn))
            {
                throw new FormatException("The provided token does not specify the 'ExpiresOn' value.");
            }

            if (!decodedToken.TryGetValue(this.AudienceFieldName, out audience))
            {
                throw new FormatException(TokenMissingAudience);
            }

            expiresOn = (EpochTime + TimeSpan.FromSeconds(double.Parse(expiresIn, CultureInfo.InvariantCulture)));
        }
    }
}