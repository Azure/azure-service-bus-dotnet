namespace Microsoft.Azure.ServiceBus.UnitTests.API
{
    using System.Runtime.CompilerServices;
    using ApprovalTests;
    using ApprovalTests.Reporters;
    using Xunit;

    public class ApiApprovals
    {
        [Fact]
        public void ApproveAzureServiceBus()
        {
            var assembly = typeof(Message).Assembly;
            var publicApi = PublicApiGenerator.ApiGenerator.GeneratePublicApi(assembly, whitelistedNamespacePrefixes: new[] { "Microsoft.Azure.ServiceBus." });
            Approvals.Verify(publicApi);
        }
    }
}