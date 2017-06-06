namespace Microsoft.Azure.ServiceBus
{
    using Microsoft.Azure.ServiceBus.Primitives;

    /// <summary>Action taking place when <see cref="ExceptionReceivedEventArgs"/> is raised for a processed message.</summary>
    public static class ExceptionReceivedEventArgsAction
    {
        /// <summary>Completion operation</summary>
        public const string Complete = "Complete";
        
        /// <summary>Abandon operation</summary>
        public const string Abandon = "Abandon";
        
        /// <summary>User message handler invocation</summary>
        public const string UserCallback = "UserCallback";
        
        /// <summary>Receive operation</summary>
        public const string Receive = "Receive";

        /// <summary>Message lock renewal operation</summary>
        public const string RenewLock = "RenewLock";

        /// <summary>Session start operation</summary>
        public const string AcceptMessageSession = "AcceptMessageSession";

        /// <summary>Session close operation</summary>
        public const string CloseMessageSession = "CloseMessageSession";
    }
}