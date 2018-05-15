using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Microsoft.Azure.ServiceBus.Management
{
    public enum EntityAvailabilityStatus
    {
        /// <summary>The entity is unknown.</summary>
        [EnumMember]
        Unknown = 0,
        
        /// <summary>The entity is available.</summary>
        [EnumMember]
        Available = 1,
        
        /// <summary>The entity is limited.</summary>
        [EnumMember]
        Limited = 2,
        
        /// <summary>The entity is being restored.</summary>
        [EnumMember]
        Restoring = 3,
        
        /// <summary>The entity is being renamed.</summary>
        [EnumMember]
        Renaming = 4
    }
}
