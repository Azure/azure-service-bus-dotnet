using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.ServiceBus.Primitives
{
    using System.Text;

    public class ServiceBusConnectionStringBuilder
    {
        public static readonly string EndpointScheme = "amqps";
        public static readonly string EndpointFormat = EndpointScheme + "://{0}.servicebus.windows.net";
        static readonly string EndpointConfigName = "Endpoint";
        static readonly string SharedAccessKeyNameConfigName = "SharedAccessKeyName";
        static readonly string SharedAccessKeyConfigName = "SharedAccessKey";
        static readonly string EntityPathConfigName = "EntityPath";
        const char KeyValueSeparator = '=';
        const char KeyValuePairDelimiter = ';';

        public Uri Endpoint { get; set; }

        /// <summary>
        /// Get the entity path value from the connection string
        /// </summary>
        public string EntityPath { get; set; }

        /// <summary>
        /// Get the shared access policy owner name from the connection string
        /// </summary>
        public string SasKeyName { get; set; }

        /// <summary>
        /// Get the shared access policy key value from the connection string
        /// </summary>
        /// <value>Shared Access Signature key</value>
        public string SasKey { get; set; }

        public ServiceBusConnectionStringBuilder(string connectionString)
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                this.ParseConnectionString(connectionString);
            }
        }

        public string GetConnectionString(string namespaceName, string entityPath, string sharedAccessKeyName, string sharedAccessKey)
        {
            if (string.IsNullOrWhiteSpace(namespaceName))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(namespaceName));
            }
            if (string.IsNullOrWhiteSpace(sharedAccessKeyName) || string.IsNullOrWhiteSpace(sharedAccessKey))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(string.IsNullOrWhiteSpace(sharedAccessKeyName) ? nameof(sharedAccessKeyName) : nameof(sharedAccessKey));
            }

            if (namespaceName.Contains("."))
            {
                // It appears to be a fully qualified host name, use it.
                this.Endpoint = new Uri(EndpointScheme + "://" + namespaceName);
            }
            else
            {
                this.Endpoint = new Uri(EndpointFormat.FormatInvariant(namespaceName));
            }

            this.EntityPath = entityPath;
            this.SasKeyName = sharedAccessKeyName;
            this.SasKey = sharedAccessKey;

            return this.ToString();
        }

        void ParseConnectionString(string connectionString)
        {
            // First split based on ';'
            string[] keyValuePairs = connectionString.Split(new[] {KeyValuePairDelimiter}, StringSplitOptions.RemoveEmptyEntries);
            foreach (var keyValuePair in keyValuePairs)
            {
                // Now split based on the _first_ '='
                string[] keyAndValue = keyValuePair.Split(new[] {KeyValueSeparator}, 2);
                string key = keyAndValue[0];
                if (keyAndValue.Length != 2)
                {
                    throw Fx.Exception.Argument(nameof(connectionString), $"Value for the connection string parameter name '{key}' was not found.");
                }

                string value = keyAndValue[1];
                if (key.Equals(EndpointConfigName, StringComparison.OrdinalIgnoreCase))
                {
                    this.Endpoint = new Uri(value);
                }
                else if (key.Equals(SharedAccessKeyNameConfigName, StringComparison.OrdinalIgnoreCase))
                {
                    this.SasKeyName = value;
                }
                else if (key.Equals(EntityPathConfigName, StringComparison.OrdinalIgnoreCase))
                {
                    this.EntityPath = value;
                }
                else if (key.Equals(SharedAccessKeyConfigName, StringComparison.OrdinalIgnoreCase))
                {
                    this.SasKey = value;
                }
                else
                {
                    throw Fx.Exception.Argument(nameof(connectionString), $"Illegal connection string parameter name '{key}'");
                }
            }
        }

        /// <summary>
        /// Returns an interoperable connection string that can be used to connect to ServiceBus Namespace
        /// </summary>
        /// <returns>the connection string</returns>
        public override string ToString()
        {
            var connectionStringBuilder = new StringBuilder();
            if (this.Endpoint != null)
            {
                connectionStringBuilder.Append($"{EndpointConfigName}{KeyValueSeparator}{this.Endpoint}{KeyValuePairDelimiter}");
            }

            if (!string.IsNullOrWhiteSpace(this.EntityPath))
            {
                connectionStringBuilder.Append($"{EntityPathConfigName}{KeyValueSeparator}{this.EntityPath}{KeyValuePairDelimiter}");
            }

            if (!string.IsNullOrWhiteSpace(this.SasKeyName))
            {
                connectionStringBuilder.Append($"{SharedAccessKeyNameConfigName}{KeyValueSeparator}{this.SasKeyName}{KeyValuePairDelimiter}");
            }

            if (!string.IsNullOrWhiteSpace(this.SasKey))
            {
                connectionStringBuilder.Append($"{SharedAccessKeyConfigName}{KeyValueSeparator}{this.SasKey}");
            }

            return connectionStringBuilder.ToString();
        }
    }
}
