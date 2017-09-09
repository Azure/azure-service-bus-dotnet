﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Amqp.Framing
{
    using System.Text;
    using Azure.Amqp;

    sealed class AmqpSqlRuleActionCodec : AmqpRuleActionCodec
    {
        public static readonly string Name = AmqpConstants.Vendor + ":sql-rule-action:list";
        public const ulong Code = 0x0000013700000006;
        const int Fields = 2;

        public AmqpSqlRuleActionCodec() : base(Name, Code) { }

        public string SqlExpression
        {
            get;
            set;
        }

        public int? CompatibilityLevel
        {
            get;
            set;
        }

        protected override int FieldCount => Fields;

        public override string ToString()
        {
            var sb = new StringBuilder("sql-rule-action(");
            var count = 0;
            AddFieldToString(SqlExpression != null, sb, "expression", SqlExpression, ref count);
            AddFieldToString(CompatibilityLevel != null, sb, "level", CompatibilityLevel, ref count);
            sb.Append(')');
            return sb.ToString();
        }

        protected override void OnEncode(ByteBuffer buffer)
        {
            AmqpCodec.EncodeString(SqlExpression, buffer);
            AmqpCodec.EncodeInt(CompatibilityLevel, buffer);
        }

        protected override void OnDecode(ByteBuffer buffer, int count)
        {
            if (count-- > 0)
            {
                SqlExpression = AmqpCodec.DecodeString(buffer);
            }

            if (count > 0)
            {
                CompatibilityLevel = AmqpCodec.DecodeInt(buffer);
            }
        }

        protected override int OnValueSize()
        {
            return AmqpCodec.GetStringEncodeSize(SqlExpression) +
                   AmqpCodec.GetIntEncodeSize(CompatibilityLevel);
        }
    }
}