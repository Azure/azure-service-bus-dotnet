// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus
{
    /// <summary>
    /// Represents a description of a rule.
    /// </summary>
    public sealed class RuleDescription
    {
        /// <summary>
        /// Gets the name of the default rule on the subscription.
        /// </summary>
        /// <remarks>
        /// Whenever a new subscription is created, a default rule is always added.
        /// The default rule is a <see cref="TrueFilter"/> which will enable all messages in the topic to reach subscription.
        /// </remarks>
        public const string DefaultRuleName = "$Default";

        Filter filter;
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
                throw Fx.Exception.ArgumentNull(nameof(filter));
            }

            Filter = filter;
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
                throw Fx.Exception.ArgumentNull(nameof(filter));
            }

            Filter = filter;
            Name = name;
        }

        /// <summary>
        /// Gets or sets the filter expression used to match messages.
        /// </summary>
        /// <value>The filter expression used to match messages.</value>
        /// <exception cref="System.ArgumentNullException">null (Nothing in Visual Basic) is assigned.</exception>
        public Filter Filter
        {
            get => filter;

            set
            {
                if (value == null)
                {
                    throw Fx.Exception.ArgumentNull(nameof(Filter));
                }

                filter = value;
            }
        }

        /// <summary>
        /// Gets or sets the action to perform if the message satisfies the filtering expression.
        /// </summary>
        /// <value>The action to perform if the message satisfies the filtering expression.</value>
        public RuleAction Action { get; set; }

        /// <summary>
        /// Gets or sets the name of the rule.
        /// </summary>
        /// <value>Returns a <see cref="System.String" /> Representing the name of the rule.</value>
        /// <remarks>Max allowed length of rule name is 50 chars.</remarks>
        public string Name
        {
            get => name;

            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(Name));
                }

                name = value;
            }
        }

        internal void ValidateDescriptionName()
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(name));
            }

            if (name.Length > Constants.RuleNameMaximumLength)
            {
                throw Fx.Exception.ArgumentOutOfRange(
                    nameof(name),
                    name,
                    Resources.EntityNameLengthExceedsLimit.FormatForUser(name, Constants.RuleNameMaximumLength));
            }

            if (name.Contains(Constants.PathDelimiter) || name.Contains(@"\"))
            {
                throw Fx.Exception.Argument(
                    nameof(name),
                    Resources.InvalidCharacterInEntityName.FormatForUser(Constants.PathDelimiter, name));
            }

            string[] uriSchemeKeys = { "@", "?", "#" };
            foreach (var uriSchemeKey in uriSchemeKeys)
            {
                if (name.Contains(uriSchemeKey))
                {
                    throw Fx.Exception.Argument(
                        nameof(name),
                        Resources.CharacterReservedForUriScheme.FormatForUser(nameof(name), uriSchemeKey));
                }
            }
        }
    }
}