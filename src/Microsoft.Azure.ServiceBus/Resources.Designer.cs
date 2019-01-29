﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.Azure.ServiceBus {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.Azure.ServiceBus.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to retreive session ID from broker. Please retry..
        /// </summary>
        internal static string AmqpFieldSessionId {
            get {
                return ResourceManager.GetString("AmqpFieldSessionId", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The received message (delivery-id:{0}, size:{1} bytes) exceeds the limit ({2} bytes) currently allowed on the link..
        /// </summary>
        internal static string AmqpMessageSizeExceeded {
            get {
                return ResourceManager.GetString("AmqpMessageSizeExceeded", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The value of the argument {0} must be positive..
        /// </summary>
        internal static string ArgumentMustBePositive {
            get {
                return ResourceManager.GetString("ArgumentMustBePositive", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The argument {0} is null or white space..
        /// </summary>
        internal static string ArgumentNullOrWhiteSpace {
            get {
                return ResourceManager.GetString("ArgumentNullOrWhiteSpace", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The argument &apos;{0}&apos; cannot exceed {1} characters..
        /// </summary>
        internal static string ArgumentStringTooBig {
            get {
                return ResourceManager.GetString("ArgumentStringTooBig", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to There are no brokeredMessages supplied. Please make sure input messages are not empty..
        /// </summary>
        internal static string BrokeredMessageListIsNullOrEmpty {
            get {
                return ResourceManager.GetString("BrokeredMessageListIsNullOrEmpty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Sending empty {0} is not a valid operation..
        /// </summary>
        internal static string CannotSendAnEmptyMessage {
            get {
                return ResourceManager.GetString("CannotSendAnEmptyMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &apos;{0}&apos; contains character &apos;{1}&apos; which is not allowed because it is reserved in the Uri scheme..
        /// </summary>
        internal static string CharacterReservedForUriScheme {
            get {
                return ResourceManager.GetString("CharacterReservedForUriScheme", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to This request has been blocked because the entity or namespace is being throttled. Please retry the operation, and if condition continues, please slow down your rate of request..
        /// </summary>
        internal static string DefaultServerBusyException {
            get {
                return ResourceManager.GetString("DefaultServerBusyException", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The entity path/name &apos;{0}&apos; exceeds the &apos;{1}&apos; character limit..
        /// </summary>
        internal static string EntityNameLengthExceedsLimit {
            get {
                return ResourceManager.GetString("EntityNameLengthExceedsLimit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The minimum back off period &apos;{0}&apos; cannot exceed the maximum back off period of &apos;{1}&apos;..
        /// </summary>
        internal static string ExponentialRetryBackoffRange {
            get {
                return ResourceManager.GetString("ExponentialRetryBackoffRange", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Serialization operation failed due to unsupported type {0}..
        /// </summary>
        internal static string FailedToSerializeUnsupportedType {
            get {
                return ResourceManager.GetString("FailedToSerializeUnsupportedType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0} is not a supported user property type..
        /// </summary>
        internal static string InvalidAmqpMessageProperty {
            get {
                return ResourceManager.GetString("InvalidAmqpMessageProperty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The entity name or path contains an invalid character &apos;{0}&apos;. The supplied value is &apos;{1}&apos;..
        /// </summary>
        internal static string InvalidCharacterInEntityName {
            get {
                return ResourceManager.GetString("InvalidCharacterInEntityName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The string has an invalid encoding format..
        /// </summary>
        internal static string InvalidEncoding {
            get {
                return ResourceManager.GetString("InvalidEncoding", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to List of lock tokens cannot be empty.
        /// </summary>
        internal static string ListOfLockTokensCannotBeEmpty {
            get {
                return ResourceManager.GetString("ListOfLockTokensCannotBeEmpty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The specified value &apos;{0}&apos; is invalid. &quot;maxConcurrentCalls&quot; must be greater than zero..
        /// </summary>
        internal static string MaxConcurrentCallsMustBeGreaterThanZero {
            get {
                return ResourceManager.GetString("MaxConcurrentCallsMustBeGreaterThanZero", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The specified value &apos;{0}&apos; is invalid. &quot;MaxMessageCount&quot; on MessageBatchHandlerOptions must be greater than zero..
        /// </summary>
        internal static string MaxMessageCountMustBeGreaterThanZero {
            get {
                return ResourceManager.GetString("MaxMessageCountMustBeGreaterThanZero", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A message handler has already been registered..
        /// </summary>
        internal static string MessageHandlerAlreadyRegistered {
            get {
                return ResourceManager.GetString("MessageHandlerAlreadyRegistered", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The lock supplied is invalid. Either the lock expired, or the message has already been removed from the queue, or was received by a different receiver instance..
        /// </summary>
        internal static string MessageLockLost {
            get {
                return ResourceManager.GetString("MessageLockLost", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &apos;{0}&apos; is not a supported type..
        /// </summary>
        internal static string NotSupportedPropertyType {
            get {
                return ResourceManager.GetString("NotSupportedPropertyType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to This operation is only supported for a message receiver in &apos;PeekLock&apos; receive mode..
        /// </summary>
        internal static string PeekLockModeRequired {
            get {
                return ResourceManager.GetString("PeekLockModeRequired", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The {0} plugin has already been registered..
        /// </summary>
        internal static string PluginAlreadyRegistered {
            get {
                return ResourceManager.GetString("PluginAlreadyRegistered", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Provided rule filter {0} is not supported. Supported values are: {1}, {2}.
        /// </summary>
        internal static string RuleFilterNotSupported {
            get {
                return ResourceManager.GetString("RuleFilterNotSupported", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to retreive session filter from broker. Please retry..
        /// </summary>
        internal static string SessionFilterMissing {
            get {
                return ResourceManager.GetString("SessionFilterMissing", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A session handler has already been registered..
        /// </summary>
        internal static string SessionHandlerAlreadyRegistered {
            get {
                return ResourceManager.GetString("SessionHandlerAlreadyRegistered", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The session lock has expired on the MessageSession. Accept a new MessageSession..
        /// </summary>
        internal static string SessionLockExpiredOnMessageSession {
            get {
                return ResourceManager.GetString("SessionLockExpiredOnMessageSession", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The length of the filter action statement is {0}, which exceeds the maximum length of {1}..
        /// </summary>
        internal static string SqlFilterActionStatmentTooLong {
            get {
                return ResourceManager.GetString("SqlFilterActionStatmentTooLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The length of the filter statement is {0}, which exceeds the maximum length of {1}.
        /// </summary>
        internal static string SqlFilterStatmentTooLong {
            get {
                return ResourceManager.GetString("SqlFilterStatmentTooLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Argument {0} must be a non-negative timeout value. The provided value was {1}..
        /// </summary>
        internal static string TimeoutMustBeNonNegative {
            get {
                return ResourceManager.GetString("TimeoutMustBeNonNegative", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Argument {0} must be a positive timeout value. The provided value was {1}..
        /// </summary>
        internal static string TimeoutMustBePositive {
            get {
                return ResourceManager.GetString("TimeoutMustBePositive", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Argument {0} must be a positive non-zero timeout value. The provided value was {1}..
        /// </summary>
        internal static string TimeoutMustBePositiveNonZero {
            get {
                return ResourceManager.GetString("TimeoutMustBePositiveNonZero", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The provided token does not specify the &apos;Audience&apos; value..
        /// </summary>
        internal static string TokenMissingAudience {
            get {
                return ResourceManager.GetString("TokenMissingAudience", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The provided token does not specify the &apos;ExpiresOn&apos; value..
        /// </summary>
        internal static string TokenMissingExpiresOn {
            get {
                return ResourceManager.GetString("TokenMissingExpiresOn", resourceCulture);
            }
        }
    }
}
