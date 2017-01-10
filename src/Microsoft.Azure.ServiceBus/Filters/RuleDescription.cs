// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Filters
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents a description of a rule.
    /// </summary>
    public sealed class RuleDescription
    {
        /// <summary>
        /// The default name used in creating default rule when adding subscriptions
        /// to a topic. The name is "$Default".
        /// </summary>
        public const string DefaultRuleName = "$Default";
        Filter filter;
        RuleAction action;
        string name;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleDescription" /> class with default values.
        /// </summary>
        public RuleDescription()
            : this(TrueFilter.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleDescription" /> class with the specified name.
        /// </summary>
        /// <param name="name">The name of the rule.</param>
        public RuleDescription(string name)
            : this(name, TrueFilter.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleDescription" /> class with the specified filter expression.
        /// </summary>
        /// <param name="filter">The filter expression used to match messages.</param>
        public RuleDescription(Filter filter)
        {
            if (filter == null)
            {
                throw Fx.Exception.ArgumentNull("Filter");
            }

            this.Filter = filter;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleDescription" /> class with the specified name and filter expression.
        /// </summary>
        /// <param name="name">The name of the rule.</param>
        /// <param name="filter">The filter expression used to match messages.</param>
        public RuleDescription(string name, Filter filter)
        {
            if (filter == null)
            {
                throw Fx.Exception.ArgumentNull("Filter");
            }

            this.Filter = filter;
            this.Name = name;
        }

        /// <summary>
        /// Gets or sets the filter expression used to match messages.
        /// </summary>
        /// <value>The filter expression used to match messages.</value>
        /// <exception cref="System.ArgumentNullException">null (Nothing in Visual Basic) is assigned.</exception>
        public Filter Filter
        {
            get
            {
                return this.filter;
            }

            set
            {
                if (value == null)
                {
                    throw Fx.Exception.ArgumentNull(nameof(value));
                }

                this.filter = value;
            }
        }

        /// <summary>
        /// Gets or sets the action to perform if the message satisfies the filtering expression.
        /// </summary>
        /// <value>The action to perform if the message satisfies the filtering expression.</value>
        public RuleAction Action
        {
            get
            {
                return this.action;
            }

            set
            {
                this.action = value;
            }
        }

        /// <summary>
        /// Gets or sets the name of the rule.
        /// </summary>
        /// <value>Returns a <see cref="System.String" /> Representing the name of the rule.</value>
        public string Name
        {
            get
            {
                return this.name;
            }

            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(this.Name));
                }

                this.name = value;
            }
        }

        internal string Tag
        {
            get;
            set;
        }

        internal void ValidateDescriptionName()
        {
            if (string.IsNullOrWhiteSpace(this.name))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace("description.Name");
            }

            if (this.name.Length > Constants.RuleNameMaximumLength)
            {
                throw Fx.Exception.ArgumentOutOfRange("description.Name", this.name, Resources.EntityNameLengthExceedsLimit.FormatForUser(this.name, Constants.RuleNameMaximumLength));
            }

            if (this.name.Contains(Constants.PathDelimiter) || this.name.Contains(@"\"))
            {
                throw Fx.Exception.Argument("description.Name", Resources.InvalidCharacterInEntityName.FormatForUser(Constants.PathDelimiter, this.name));
            }

            string[] uriSchemeKeys = { "@", "?", "#" };
            foreach (var uriSchemeKey in uriSchemeKeys)
            {
                if (this.name.Contains(uriSchemeKey))
                {
                    throw Fx.Exception.Argument(
                        "description.Name", Resources.CharacterReservedForUriScheme.FormatForUser("description.Name", uriSchemeKey));
                }
            }
        }
    }
}