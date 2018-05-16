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

        /// <summary>Resuming the previous status of the messaging entity.</summary>
        [EnumMember]
        Restoring = 2,

        /// <summary>The sending status of the messaging entity is disabled.</summary>
        [EnumMember]
        SendDisabled = 3,

        /// <summary>The receiving status of the messaging entity is disabled.</summary>
        [EnumMember]
        ReceiveDisabled = 4,

        /// <summary>Indicates that the resource is still being created. Any creation attempt on the same resource path will result in a 
        /// <see cref="ServiceBusException" /> exception (HttpCode.Conflict 409).</summary> 
        [EnumMember]
        Creating = 5,
        
        /// <summary>Indicates that the system is still attempting cleanup of the entity. Any additional deletion call will be allowed (the system will be notified). Any additional creation call on the same resource path will result in a 
        /// <see cref="ServiceBusException" /> exception (HttpCode.Conflict 409).</summary> 
        [EnumMember]
        Deleting = 6,

        /// <summary>The messaging entity is being renamed.</summary>
        [EnumMember]
        Renaming = 7,

        /// <summary>The status of the messaging entity is unknown.</summary>
        [EnumMember]
        Unknown = 99
    }
}
