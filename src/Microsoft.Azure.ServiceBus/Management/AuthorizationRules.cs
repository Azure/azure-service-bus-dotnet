using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.ServiceBus.Management
{
    public class AuthorizationRules : ICollection<AuthorizationRule>
    {
        private List<AuthorizationRule> innerCollection;

        public AuthorizationRules()
        {
            this.innerCollection = new List<AuthorizationRule>();
        }

        public bool RequiresEncryption { get; set; }

        public int Count => this.innerCollection.Count;

        public bool IsReadOnly => true;

        public void Add(AuthorizationRule item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(AuthorizationRule item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(AuthorizationRule[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<AuthorizationRule> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public bool Remove(AuthorizationRule item)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
