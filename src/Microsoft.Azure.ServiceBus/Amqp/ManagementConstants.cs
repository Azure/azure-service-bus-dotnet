//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Messaging.Amqp
{
    using System;
    using Microsoft.Azure.Amqp.Encoding;

    static class ManagementConstants
    {
        public const string Microsoft = "com.microsoft";

        public static class Request
        {
            public const string Operation = "operation";
        }

        public static class Response
        {
            public const string StatusCode = "statusCode";
            public const string StatusDescription = "statusDescription";
            public const string ErrorCondition = "errorCondition";
        }

        public static class Operations
        {
            public const string RenewLockOperation = Microsoft + ":renew-lock";
            public const string ReceiveBySequenceNumberOperation = Microsoft + ":receive-by-sequence-number";
            public const string UpdateDispositionOperation = Microsoft + ":update-disposition";
        }

        public static class Properties
        {
            public static readonly MapKey ServerTimeout = new MapKey(Microsoft + ":server-timeout");
            public static readonly MapKey TrackingId = new MapKey(Microsoft + ":tracking-id");

            public static readonly MapKey LockToken = new MapKey("lock-token");
            public static readonly MapKey LockTokens = new MapKey("lock-tokens");
            public static readonly MapKey SequenceNumbers = new MapKey("sequence-numbers");
            public static readonly MapKey Expirations = new MapKey("expirations");
            public static readonly MapKey Expiration = new MapKey("expiration");
            public static readonly MapKey LockedUntilUtc = new MapKey("locked-until-utc");
            public static readonly MapKey ReceiverSettleMode = new MapKey("receiver-settle-mode");
            public static readonly MapKey Message = new MapKey("message");
            public static readonly MapKey Messages = new MapKey("messages");
            public static readonly MapKey DispositionStatus = new MapKey("disposition-status");
        }
    }
}
