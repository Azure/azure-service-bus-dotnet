namespace Microsoft.Azure.ServiceBus.Management
{
    public class QueueRuntimeDescription
    {
        public long SizeInBytes { get; set; }

        public MessageCountDetails MessageCountDetails { get; set; }

        public EntityAvailabilityStatus AvailabilityStatus { get; set; }
    }
}
