﻿namespace Microsoft.Azure.ServiceBus.Amqp.Framing
{
    using Microsoft.Azure.Amqp;

    sealed class AmqpTrueFilterCodec : AmqpFilterCodec
    {
        public static readonly string Name = AmqpConstants.Vendor + ":true-filter:list";
        public static readonly ulong Code = 0x000001370000007;

        public AmqpTrueFilterCodec() : base((string) Name, (ulong) Code) { }

        public override string ToString()
        {
            return "true()";
        }
    }
}