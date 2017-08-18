<p align="center">
  <img src="service-bus.png" alt="Microsoft Azure Relay" width="100"/>
</p>

# Microsoft Azure Service Bus Client for .NET


|Build/Package|Status|
|------|-------------|
|master|[![Build status](https://ci.appveyor.com/api/projects/status/o4kaqt06h62d0ugp/branch/master?svg=true)](https://ci.appveyor.com/project/vinaysurya/azure-service-bus-dotnet/branch/master) [![codecov](https://codecov.io/gh/Azure/azure-service-bus-dotnet/branch/master/graph/badge.svg)](https://codecov.io/gh/Azure/azure-service-bus-dotnet)|
|dev|[![Build status](https://ci.appveyor.com/api/projects/status/o4kaqt06h62d0ugp/branch/dev?svg=true)](https://ci.appveyor.com/project/vinaysurya/azure-service-bus-dotnet/branch/dev) [![codecov](https://codecov.io/gh/Azure/azure-service-bus-dotnet/branch/dev/graph/badge.svg)](https://codecov.io/gh/Azure/azure-service-bus-dotnet)|
|Microsoft.Azure.ServiceBus|[![NuGet Version and Downloads count](https://buildstats.info/nuget/Microsoft.Azure.ServiceBus?includePreReleases=true)](https://www.nuget.org/packages/Microsoft.Azure.ServiceBus/)|

This is the next generation Service Bus .NET client library that focuses on queues & topics. If you are looking for Event Hubs and Relay clients, follow the below links:
* [Event Hubs](https://github.com/azure/azure-event-hubs-dotnet)
* [Relay](https://github.com/azure/azure-relay-dotnet)

Azure Service Bus is an asynchronous messaging cloud platform that enables you to send messages between decoupled systems. Microsoft offers this feature as a service, which means that you do not need to host any of your own hardware in order to use it.

Refer to the [online documentation](https://azure.microsoft.com/services/service-bus/) to learn more about Service Bus.

This library is built using .NET Standard 1.3. For more information on what platforms are supported see [.NET Platforms Support](https://docs.microsoft.com/en-us/dotnet/articles/standard/library#net-platforms-support).

## How to provide feedback

See our [Contribution Guidelines](./.github/CONTRIBUTING.md).

## How to get support

See our [Support Guidelines](./.github/SUPPORT.md)

## FAQ

### Where can I find examples that use this library?

[https://github.com/Azure/azure-service-bus/tree/master/samples](https://github.com/Azure/azure-service-bus/tree/master/samples)

### How do I run the unit tests? 

In order to run the unit tests, you will need to do the following:

1. Deploy the Azure Resource Manager template located at [/build/azuredeploy.json](/build/azuredeploy.json) by clicking the following button:

    <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fazure-service-bus-dotnet%2Fmaster%2Fbuild%2Fazuredeploy.json" target="_blank">
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
