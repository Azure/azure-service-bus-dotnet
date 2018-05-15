using System.Collections.Generic;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class AuthorizationRules : List<AuthorizationRule>
    {
        public bool RequiresEncryption { get; set; }
    }
}
