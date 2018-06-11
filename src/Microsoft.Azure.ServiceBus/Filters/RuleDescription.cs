// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Azure.ServiceBus.Primitives;

namespace Microsoft.Azure.ServiceBus
{
    /// <summary>
    /// Represents a description of a rule.
    /// </summary>
    public sealed class RuleDescription : IEquatable<RuleDescription>
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
            : this(RuleDescription.DefaultRuleName, TrueFilter.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleDescription" /> class with the specified name.
        /// </summary>
        public RuleDescription(string name)
            : this(name, TrueFilter.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleDescription" /> class with the specified filter expression.
        /// </summary>
        /// <param name="filter">The filter expression used to match messages.</param>
        [Obsolete("This constructor will be removed in next version, please use RuleDescription(string, Filter) instead.")]
        public RuleDescription(Filter filter)
        {
            if (filter == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(filter));
            }

            this.Filter = filter;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleDescription" /> class with the specified name and filter expression.
        /// </summary>
        /// <param name="filter">The filter expression used to match messages.</param>
        public RuleDescription(string name, Filter filter)
        {
            if (filter == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(filter));
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
            get => this.filter;

            set
            {
                if (value == null)
                {
                    throw Fx.Exception.ArgumentNull(nameof(this.Filter));
                }

                this.filter = value;
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
            get => this.name;

            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(this.Name));
                }

                this.name = value;
            }
        }

        // TODO: Implement for AMQP
        internal DateTime CreatedAt
        {
            get; set;
        }

        internal void ValidateDescriptionName()
        {
            if (string.IsNullOrWhiteSpace(this.name))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(this.name));
            }

            if (this.name.Length > Constants.RuleNameMaximumLength)
            {
                throw Fx.Exception.ArgumentOutOfRange(
                    nameof(this.name),
                    this.name,
                    Resources.EntityNameLengthExceedsLimit.FormatForUser(this.name, Constants.RuleNameMaximumLength));
            }

            if (this.name.Contains(Constants.PathDelimiter) || this.name.Contains(@"\"))
            {
                throw Fx.Exception.Argument(
                    nameof(this.name),
                    Resources.InvalidCharacterInEntityName.FormatForUser(Constants.PathDelimiter, this.name));
            }

            string[] uriSchemeKeys = { "@", "?", "#" };
            foreach (var uriSchemeKey in uriSchemeKeys)
            {
                if (this.name.Contains(uriSchemeKey))
                {
                    throw Fx.Exception.Argument(
                        nameof(this.name),
                        Resources.CharacterReservedForUriScheme.FormatForUser(nameof(this.name), uriSchemeKey));
                }
            }
        }

        static internal RuleDescription ParseFromContent(string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "entry")
                {
                    return ParseFromEntryElement(xDoc);
                }
            }

            throw new MessagingEntityNotFoundException("Rule was not found");
        }

        static internal IList<RuleDescription> ParseCollectionFromContent(string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "feed")
                {
                    var rules = new List<RuleDescription>();

                    var entryList = xDoc.Elements(XName.Get("entry", ManagementConstants.AtomNs));
                    foreach (var entry in entryList)
                    {
                        rules.Add(ParseFromEntryElement(entry));
                    }

                    return rules;
                }
            }

            throw new MessagingEntityNotFoundException("Rule was not found");
        }

        static private RuleDescription ParseFromEntryElement(XElement xEntry)
        {
            try
            {
                var name = xEntry.Element(XName.Get("title", ManagementConstants.AtomNs)).Value;
                var ruleDescription = new RuleDescription();

                var rdXml = xEntry.Element(XName.Get("content", ManagementConstants.AtomNs))?
                    .Element(XName.Get("RuleDescription", ManagementConstants.SbNs));

                if (rdXml == null)
                {
                    throw new MessagingEntityNotFoundException("Rule was not found");
                }

                foreach (var element in rdXml.Elements())
                {
                    switch (element.Name.LocalName)
                    {
                        case "Name":
                            ruleDescription.Name = element.Value;
                            break;
                        case "Filter":
                            ruleDescription.Filter = Filter.ParseFromXElement(element);
                            break;
                        case "Action":
                            ruleDescription.Action = RuleAction.ParseFromXElement(element);
                            break;
                        case "CreatedAt":
                            ruleDescription.CreatedAt = DateTime.Parse(element.Value);
                            break;
                    }
                }

                return ruleDescription;
            }
            catch (Exception ex) when (!(ex is ServiceBusException))
            {
                throw new ServiceBusException(false, ex);
            }
        }

        internal XDocument Serialize()
        {
            XDocument doc = new XDocument(
                   new XElement(XName.Get("entry", ManagementConstants.AtomNs),
                       new XElement(XName.Get("content", ManagementConstants.AtomNs),
                           new XAttribute("type", "application/xml"),
                           new XElement(
                                XName.Get("RuleDescription", ManagementConstants.SbNs),
                                this.Filter?.Serialize(),
                                this.Action?.Serialize(),
                                new XElement(XName.Get("Name", ManagementConstants.SbNs), this.Name)))));

            return doc;
        }

        public bool Equals(RuleDescription other)
        {
            if (string.Equals(this.Name, other.Name, StringComparison.OrdinalIgnoreCase)
                && (this.Filter == null || this.Filter.Equals(other.Filter))
                && (this.Action == null || this.Action.Equals(other.Action)))
            {
                return true;
            }

            return false;
        }
    }
}