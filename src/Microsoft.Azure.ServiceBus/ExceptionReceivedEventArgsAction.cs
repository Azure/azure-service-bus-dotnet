namespace Microsoft.Azure.ServiceBus
{
    static class ExceptionReceivedEventArgsAction
    {
        public const string Complete = "Complete";
        public const string Abandon = "Abandon";
        public const string UserCallback = "UserCallback";
        public const string Receive = "Receive";
        public const string RenewLock = "RenewLock";
        public const string AcceptMessageSession = "AcceptMessageSession";
        public const string CloseMessageSession = "CloseMessageSession";
    }
}