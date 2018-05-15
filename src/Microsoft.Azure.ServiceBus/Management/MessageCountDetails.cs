namespace Microsoft.Azure.ServiceBus.Management
{
    public class MessageCountDetails
    {
        public long ActiveMessageCount { get; set; }

        public long DeadLetterMessageCount { get; set; }

        public long ScheduledMessageCount { get; set; }

        // TODO: Do we return the following two?
        public long TransferMessageCount { get; set; }

        public long TransferDeadLetterMessageCount { get; set; }
    }
}
