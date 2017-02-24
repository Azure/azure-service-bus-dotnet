<p align="center">
  <img src="service-bus.png" alt="Microsoft Azure Relay" width="100"/>
</p>

# Microsoft Azure Service Bus Client for .NET

**Please be aware that this library is currently in active development, and is not intended for production**

|Build/Package|Status|
|------|-------------|
|master|[![Build status](https://ci.appveyor.com/api/projects/status/anpaipqto58ka5lk/branch/master?svg=true)](https://ci.appveyor.com/project/jtaubensee/azure-service-bus-dotnet/branch/master)|
|dev|[![Build status](https://ci.appveyor.com/api/projects/status/anpaipqto58ka5lk/branch/master?svg=true)](https://ci.appveyor.com/project/jtaubensee/azure-service-bus-dotnet/branch/dev)|

This is the next generation Service Bus .NET client library that focuses on queues & topics. If you are looking for Event Hubs and Relay clients, follow the below links:
* [Event Hubs](https://github.com/azure/azure-event-hubs-dotnet)
* [Relay](https://github.com/azure/azure-relay-dotnet)
 
For information on the current set of implemented features and features to come, see our [Road map](#road-map).

Azure Service Bus Messaging is an asynchronous messaging cloud platform that enables you to send messages between decoupled systems. Microsoft offers this feature as a service, which means that you do not need to host any of your own hardware in order to use it.

Refer to the [online documentation](https://azure.microsoft.com/services/service-bus/) to learn more about Service Bus.

This library is built using .NET Standard 1.3. For more information on what platforms are supported see [.NET Platforms Support](https://docs.microsoft.com/en-us/dotnet/articles/standard/library#net-platforms-support).

## How to provide feedback

See our [Contribution Guidelines](./.github/CONTRIBUTING.md).

## FAQ

### Where can I find examples that use this library?

To get started *sending* messages to Service Bus refer to [Get started sending to Service Bus queues](./samples/SendSample/readme.md).

To get started *receiving* messages with Service Bus refer to [Get started receiving from Service Bus queues](./samples/ReceiveSample/readme.md).  

### How do I run the unit tests? 

In order to run the unit tests, you will need to do the following:

1. Deploy the Azure Resource Manager template located at [/templates/azuredeploy.json](/templates/azuredeploy.json) by clicking the following button:

    <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fazure-service-bus-dotnet%2Fmaster%2Ftemplates%2Fazuredeploy.json" target="_blank">
        <img src="http://azuredeploy.net/deploybutton.png"/>
    </a>

    *Running the above template will provision a standard Service Bus namespace along with the required entities to successfully run the unit tests.*

1. Add an Environment Variable named `azure-service-bus-dotnet/connectionstring` and set the value as the connection string of the newly created namespace. **Please note that if you are using Visual Studio, you must restart Visual Studio in order to use new Environment Variables.**

Once you have completed the above, you can run `dotnet test` from the `/test/Microsoft.Azure.ServiceBus.UnitTests` directory.

### Can I manage Service Bus entities with this library?

The standard way to manage Azure resources is by using [Azure Resource Manager](https://docs.microsoft.com/en-us/azure/azure-resource-manager/resource-group-overview). In order to use functionality that previously existed in the .NET Framework Service Bus client library, you will need to use the `Microsoft.Azure.Management.ServiceBus` library. This will enable use cases that dynamically create/read/update/delete resources. The following links will provide more information on the new library and how to use it.

* GitHub repo - [https://github.com/Azure/azure-sdk-for-net/tree/AutoRest/src/ResourceManagement/ServiceBus](https://github.com/Azure/azure-sdk-for-net/tree/AutoRest/src/ResourceManagement/ServiceBus)
* NuGet package - [https://www.nuget.org/packages/Microsoft.Azure.Management.ServiceBus/](https://www.nuget.org/packages/Microsoft.Azure.Management.ServiceBus/)
* Sample - [https://github.com/Azure-Samples/service-bus-dotnet-management](https://github.com/Azure-Samples/service-bus-dotnet-management)

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

- [x] Sprint 5: **Complete**
  * Exception handling and some missing error scenarios
  * Add/Remove Rule
  * Browse messages and sessions
  * Scheduled messages specific API (Scheduling of messages can be done today through the queue/topic client, but this item is to add specific API's for scheduled messages)
  * EventSource logging
  * Overload to Receive/AcceptMessageSession APIs that accepts ServerWaitTimeout

- [ ] Sprint 6: February 2017
  * Interfaces for easier testing
  * "EntityFactory" (name to be determined) - Object used to create Queue/Topic/Subscption clients
  * OnMessage/OnSession handlers
  * PartitionedEntity API - Batch receive to/complete from a specific partition
  * NuGet package
  * Dedicated AppVeyor account
  * API documentation
  * General clean up (i.e. moving strings to resources file)
  * Additional unit/stress tests
    * Prefetch
