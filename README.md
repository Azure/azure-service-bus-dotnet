﻿<p align="center">
  <img src="service-bus.png" alt="Microsoft Azure Relay" width="100"/>
</p>

# Microsoft Azure Service Bus Client for .NET

**Please be aware that this library is currently in active development, and is not intended for production**

This is the next generation Service Bus .NET client library that focuses on Queues & Topics. If you are looking for Event Hubs and Relay clients, follow the below links:
* [Event Hubs](https://github.com/azure/azure-event-hubs-dotnet)
* [Relay](https://github.com/azure/azure-relay-dotnet)
 
For information on the current set of implemented features and features to come, see our [Road map](#road-map).

Azure Service Bus Messaging is an asynchronous messaging cloud platform that enables you to send messages between decoupled systems. Microsoft offers this feature as a service, which means that you do not need to host any of your own hardware in order to use it.

Refer to [docs.microsoft.com](https://azure.microsoft.com/services/service-bus/) to learn more about Service Bus.

This library is built using .NET Standard 1.3. For more information on what platforms are supported see [.NET Platforms Support](https://docs.microsoft.com/en-us/dotnet/articles/standard/library#net-platforms-support).

### Getting Started

To get started sending messages to Service Bus refer to [Get started sending to Service Bus queues](./samples/SendSample/readme.md).

To get started receiving messages with Service Bus refer to [Get started receiving from Service Bus queues](./samples/ReceiveSample/readme.md).  

### Running the unit tests 

In order to run the unit tests, you will need to do the following:

1. Deploy the ARM template located at [/templates/azuredeploy.json](/templates/azuredeploy.json), which will provision the required entities for the unit tests, or click the following button:

    <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fazure-service-bus-dotnet%2Fmaster%2Ftemplates%2Fazuredeploy.json" target="_blank">
        <img src="http://azuredeploy.net/deploybutton.png"/>
    </a>

1. Add an Environment Variable named `azure-service-bus-dotnet/connectionstring` and set the value as the connection string of the newly created namespace. **Please note that if you are using Visual Studio, you must restart Visual Studio in order to use new Environment Variables.**

## How to provide feedback

See our [Contribution Guidelines](./.github/CONTRIBUTING.md).

## Road map

- [x] Sprint 1: **Complete**

  All runtime operations for queues (not topics / subscriptions)
    * Send
    * Receive/Peeklock (without receive by sequence number)
    * Abandon
    * Deadletter
    * Defer
  
- [x] Sprint 2: **Complete**
  * RenewLock (Request/Response)
  * Batch operation  - Explicit batching only
  * Runtime operation only
  * Linux testing setup/investigation

- [x] Sprint 3: **Complete**
  * Add topic/subscription support
  * Session support
    * Accept session
    * Session Receive/ReceiveBatch
	
- [x] Sprint 4: **Complete**
  * Retry policy
  * Receive by sequence number

- [ ] Sprint 5: Early 2017
  * Add major error conditions (ex. preventing all operations that are not supported, for Ex Transaction scenarios, etc)
  * Request/Response features:
      * Add/Remove Rule
      * Browse messages and sessions
  * Scheduled messages specific API (Scheduling of messages can be done today through the Queue/Topic client, but this item is to add specific API's for scheduled messages)
  * OnMessage/OnSession handlers
