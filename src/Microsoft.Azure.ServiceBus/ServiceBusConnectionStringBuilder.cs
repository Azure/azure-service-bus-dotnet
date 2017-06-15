// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus
{
    /// <summary>
    ///     Used to generate Service Bus connection strings.
    /// </summary>
    public class ServiceBusConnectionStringBuilder
    {
        const char KeyValueSeparator = '=';
        const char KeyValuePairDelimiter = ';';
        static readonly string EndpointScheme = "amqps";
        static readonly string EndpointFormat = EndpointScheme + "://{0}.servicebus.windows.net";
        static readonly string EndpointConfigName = "Endpoint";
        static readonly string SharedAccessKeyNameConfigName = "SharedAccessKeyName";
        static readonly string SharedAccessKeyConfigName = "SharedAccessKey";
        static readonly string EntityPathConfigName = "EntityPath";

        /// <summary>
        ///     Instatiates a new <see cref="ServiceBusConnectionStringBuilder" />.
        /// </summary>
        /// <param name="connectionString">Connection string for namespace or the entity.</param>
        public ServiceBusConnectionStringBuilder(string connectionString)
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
                ParseConnectionString(connectionString);
        }

        /// <summary>
        ///     Instantiates a new <see cref="ServiceBusConnectionStringBuilder" />.
        /// </summary>
        /// <param name="namespaceName">Namespace name.</param>
        /// <param name="entityPath">Path to the entity.</param>
        /// <param name="sharedAccessKeyName">Shared access key name.</param>
        /// <param name="sharedAccessKey">Shared access key.</param>
        public ServiceBusConnectionStringBuilder(string namespaceName, string entityPath, string sharedAccessKeyName, string sharedAccessKey)
        {
            if (string.IsNullOrWhiteSpace(namespaceName))
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(namespaceName));
            if (string.IsNullOrWhiteSpace(sharedAccessKeyName) || string.IsNullOrWhiteSpace(sharedAccessKey))
                throw Fx.Exception.ArgumentNullOrWhiteSpace(string.IsNullOrWhiteSpace(sharedAccessKeyName) ? nameof(sharedAccessKeyName) : nameof(sharedAccessKey));

            if (namespaceName.Contains("."))
                Endpoint = new Uri(EndpointScheme + "://" + namespaceName);
            else
                Endpoint = new Uri(EndpointFormat.FormatInvariant(namespaceName));

            EntityPath = entityPath;
            SasKeyName = sharedAccessKeyName;
            SasKey = sharedAccessKey;
        }

        /// <summary>
        ///     Gets or sets the Service Bus endpoint.
        /// </summary>
        public Uri Endpoint { get; set; }

        /// <summary>
        ///     Get the entity path value from the connection string
        /// </summary>
        public string EntityPath { get; set; }

        /// <summary>
        ///     Get the shared access policy owner name from the connection string
        /// </summary>
        public string SasKeyName { get; set; }

        /// <summary>
        ///     Get the shared access policy key value from the connection string
        /// </summary>
        /// <value>Shared Access Signature key</value>
        public string SasKey { get; set; }

        /// <summary>
        ///     Returns an interoperable connection string that can be used to connect to ServiceBus Namespace
        /// </summary>
        /// <returns>Namespace connection string</returns>
        public string GetNamespaceConnectionString()
        {
            var connectionStringBuilder = new StringBuilder();
            if (Endpoint != null)
                connectionStringBuilder.Append($"{EndpointConfigName}{KeyValueSeparator}{Endpoint}{KeyValuePairDelimiter}");

            if (!string.IsNullOrWhiteSpace(SasKeyName))
                connectionStringBuilder.Append($"{SharedAccessKeyNameConfigName}{KeyValueSeparator}{SasKeyName}{KeyValuePairDelimiter}");

            if (!string.IsNullOrWhiteSpace(SasKey))
                connectionStringBuilder.Append($"{SharedAccessKeyConfigName}{KeyValueSeparator}{SasKey}");

            return connectionStringBuilder.ToString();
        }

        /// <summary>
        ///     Returns an interoperable connection string that can be used to connect to the given ServiceBus Entity
        /// </summary>
        /// <returns>Entity connection string</returns>
        public string GetEntityConnectionString()
        {
            if (string.IsNullOrWhiteSpace(EntityPath))
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(EntityPath));

            return $"{GetNamespaceConnectionString()}{KeyValuePairDelimiter}{EntityPathConfigName}{KeyValueSeparator}{EntityPath}{KeyValuePairDelimiter}";
        }

        /// <summary>
        ///     Returns an interoperable connection string that can be used to connect to ServiceBus Namespace
        /// </summary>
        /// <returns>The connection string</returns>
        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(EntityPath))
                return GetNamespaceConnectionString();

            return GetEntityConnectionString();
        }

        void ParseConnectionString(string connectionString)
        {
            // First split based on ';'
            var keyValuePairs = connectionString.Split(new[] {KeyValuePairDelimiter}, StringSplitOptions.RemoveEmptyEntries);
            foreach (var keyValuePair in keyValuePairs)
            {
                // Now split based on the _first_ '='
                var keyAndValue = keyValuePair.Split(new[] {KeyValueSeparator}, 2);
                var key = keyAndValue[0];
                if (keyAndValue.Length != 2)
                    throw Fx.Exception.Argument(nameof(connectionString), $"Value for the connection string parameter name '{key}' was not found.");

                var value = keyAndValue[1];
                if (key.Equals(EndpointConfigName, StringComparison.OrdinalIgnoreCase))
                    Endpoint = new Uri(value);
                else if (key.Equals(SharedAccessKeyNameConfigName, StringComparison.OrdinalIgnoreCase))
                    SasKeyName = value;
                else if (key.Equals(EntityPathConfigName, StringComparison.OrdinalIgnoreCase))
                    EntityPath = value;
                else if (key.Equals(SharedAccessKeyConfigName, StringComparison.OrdinalIgnoreCase))
                    SasKey = value;
                else
                    throw Fx.Exception.Argument(nameof(connectionString), $"Illegal connection string parameter name '{key}'");
            }
        }
    }
}