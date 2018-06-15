using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class AuthorizationRules : List<AuthorizationRule>, IEquatable<AuthorizationRules>
    {
        private bool RequiresEncryption => this.Count > 0;

        internal XElement Serialize()
        {
            var rules = new XElement(
                XName.Get("AuthorizationRules", ManagementClientConstants.SbNs),
                this.Select(rule => rule.Serialize()));

            return rules;
        }

        internal static AuthorizationRules ParseFromXElement(XElement xElement)
        {
            var rules = new AuthorizationRules();
            var xRules = xElement.Elements(XName.Get("AuthorizationRule", ManagementClientConstants.SbNs));
            rules.AddRange(xRules.Select(rule => AuthorizationRule.ParseFromXElement(rule)));
            return rules;
        }

        public bool Equals(AuthorizationRules other)
        {
            if (other == null || this.Count != other.Count)
            {
                return false;
            }

            var cnt = new Dictionary<string, AuthorizationRule>();
            foreach (AuthorizationRule rule in this)
            {
                cnt[rule.KeyName] = rule;
            }

            foreach (AuthorizationRule otherRule in other)
            {
                if (!cnt.TryGetValue(otherRule.KeyName, out var rule) || !rule.Equals(otherRule))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
