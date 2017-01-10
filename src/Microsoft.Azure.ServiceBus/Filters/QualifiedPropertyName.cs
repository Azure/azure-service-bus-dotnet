// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
/*
namespace Microsoft.Azure.ServiceBus.Filters
{
    using System;
    using System.Globalization;

    struct QualifiedPropertyName : IEquatable<QualifiedPropertyName>, IComparable<QualifiedPropertyName>
    {
        readonly PropertyScope scope;

        readonly string name;

        public QualifiedPropertyName(PropertyScope scope, string name)
        {
            this.scope = scope;
            this.name = name;
        }

        public PropertyScope Scope => this.scope;

        public string Name => this.name;

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}", this.scope, this.name);
        }

        public bool Equals(QualifiedPropertyName other)
        {
            return
                this.scope == other.scope &&
                StringComparer.OrdinalIgnoreCase.Equals(this.name, other.name);
        }

        public int CompareTo(QualifiedPropertyName other)
        {
            int result = ((int)this.scope).CompareTo((int)other.scope);
            if (result == 0)
            {
                result = StringComparer.OrdinalIgnoreCase.Compare(this.name, other.name);
            }

            return result;
        }

        public override bool Equals(object obj)
        {
            if (obj is QualifiedPropertyName)
            {
                return this.Equals((QualifiedPropertyName)obj);
            }

            return false;
        }

        public override int GetHashCode()
        {
            int scopeHash = ((int)this.scope).GetHashCode();
            int nameHash = StringComparer.OrdinalIgnoreCase.GetHashCode(this.name);

            return HashCode.CombineHashCodes(scopeHash, nameHash);
        }
    }
}
*/