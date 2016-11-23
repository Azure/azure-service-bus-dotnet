using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.Messaging.Primitives
{
    enum DispositionStatus
    {
        Completed = 1, 
        Defered = 2,
        Suspended = 3,
        Abandoned = 4,
        Renewed = 5,
    }
}
