using System.Runtime.Serialization;

namespace Microsoft.Azure.ServiceBus.Management
{
    public enum EntityStatus
    {
        /// <summary>The status of the messaging entity is active.</summary>
        [EnumMember]
        Active = 0,

        /// <summary>The status of the messaging entity is disabled.</summary>
        [EnumMember]
        Disabled = 1,

        /// <summary>The sending status of the messaging entity is disabled.</summary>
        [EnumMember]
        SendDisabled = 3,

        /// <summary>The receiving status of the messaging entity is disabled.</summary>
        [EnumMember]
        ReceiveDisabled = 4,
    }
}
