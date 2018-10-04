namespace Microsoft.Azure.ServiceBus
{
    using Microsoft.Azure.ServiceBus.Primitives;

    internal static class MessageExtensions
    {
        public static void VerifyMessageIsNotPreviouslyReceived(this Message message)
        {
            if (message.SystemProperties.IsLockTokenSet)
            {
                throw Fx.Exception.Argument(nameof(message), "Cannot send a message that was already received.");
            }
        }
    }
}