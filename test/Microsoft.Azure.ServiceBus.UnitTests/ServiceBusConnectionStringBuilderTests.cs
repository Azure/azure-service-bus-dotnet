// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.UnitTests
{
    using System;
    using Xunit;

    public class ServiceBusConnectionStringBuilderTests
    {
        [Fact]
        void ConnectionStringBuilderShouldTakeCareOfWhitespace()
        {
            var csBuilder = new ServiceBusConnectionStringBuilder
            {
                Endpoint = " amqps://contoso.servicebus.windows.net ",
                EntityPath = " myQ ",
                SasKeyName = " keyname ",
                SasKey = " key "
            };

            Assert.Equal("Endpoint=amqps://contoso.servicebus.windows.net;SharedAccessKeyName=keyname;SharedAccessKey=key;EntityPath=myQ", csBuilder.ToString());
        }

        [Fact]
        void ConnectionStringBuilderEndpointShouldFormatUri()
        {
            var csBuilder = new ServiceBusConnectionStringBuilder();

            csBuilder.Endpoint = "ns1.servicebus.windows.net";
            Assert.Equal("amqps://ns1.servicebus.windows.net", csBuilder.Endpoint);

            csBuilder.Endpoint = " ns2.servicebus.windows.net ";
            Assert.Equal("amqps://ns2.servicebus.windows.net", csBuilder.Endpoint);

            csBuilder.Endpoint = "amqps://ns3.servicebus.windows.net";
            Assert.Equal("amqps://ns3.servicebus.windows.net", csBuilder.Endpoint);

            csBuilder.Endpoint = "https://ns4.servicebus.windows.net:3990";
            Assert.Equal("amqps://ns4.servicebus.windows.net", csBuilder.Endpoint);

            csBuilder.Endpoint = "ns5.servicebus.windows.net/";
            Assert.Equal("amqps://ns5.servicebus.windows.net", csBuilder.Endpoint);
        }

        [Fact]
        void ConnectionStringBuilderShouldTakeCareOfSlash()
        {
            var csBuilder = new ServiceBusConnectionStringBuilder
            {
                Endpoint = "contoso.servicebus.windows.net/",
                EntityPath = " myQ ",
                SasKeyName = " keyname "
            };

            Assert.Equal("Endpoint=amqps://contoso.servicebus.windows.net;SharedAccessKeyName=keyname;EntityPath=myQ", csBuilder.ToString());
        }

        [Fact]
        void ConnectionStringBuilderShouldTrimTrailingSemicolon()
        {
            var csBuilder = new ServiceBusConnectionStringBuilder
            {
                Endpoint = " amqps://contoso.servicebus.windows.net",
                SasKeyName = " keyname "
            };

            Assert.Equal("Endpoint=amqps://contoso.servicebus.windows.net;SharedAccessKeyName=keyname", csBuilder.ToString());

            csBuilder.SasKeyName = "";
            Assert.Equal("Endpoint=amqps://contoso.servicebus.windows.net", csBuilder.ToString());

            csBuilder.EntityPath = "myQ";
            Assert.Equal("Endpoint=amqps://contoso.servicebus.windows.net;EntityPath=myQ", csBuilder.ToString());
        }

        [Fact]
        void ConnectionStringBuilderShouldThrowForInvalidEndpoint()
        {
            var csBuilder = new ServiceBusConnectionStringBuilder();
            Assert.Throws<ArgumentException>(() => csBuilder.Endpoint = "ns1");
            Assert.Throws<UriFormatException>(() => csBuilder.Endpoint = "ns2 .ns3");
        }

        [Fact]
        void ConnectionStringBuilderShouldNotFailWhileParsingUnknownProperties()
        {
            string connectionString = "Endpoint=amqp://hello.servicebus.windows.net;SecretMessage=h=llo;EntityPath=myQ;";
            var csBuilder = new ServiceBusConnectionStringBuilder(connectionString);
            Assert.Equal("amqps://hello.servicebus.windows.net", csBuilder.Endpoint);
            Assert.Equal("myQ", csBuilder.EntityPath);
            Assert.Equal(1, csBuilder.ConnectionStringProperties.Count);
            Assert.True(csBuilder.ConnectionStringProperties.ContainsKey("secretmessage"));
            Assert.Equal("h=llo", csBuilder.ConnectionStringProperties["secretmessage"]);
        }
    }
}
