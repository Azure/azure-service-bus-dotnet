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
    using System.Reflection;
    
    
    /// <summary>
    ///    A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        internal Resources() {
        }
        
        /// <summary>
        ///    Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.Azure.ServiceBus.Resources", typeof(Resources).GetTypeInfo().Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///    Overrides the current thread's CurrentUICulture property for all
        ///    resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to No session-id was specified for a session receiver..
        /// </summary>
        public static string AmqpFieldSessionId {
            get {
                return ResourceManager.GetString("AmqpFieldSessionId", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to The received message (delivery-id:{0}, size:{1} bytes) exceeds the limit ({2} bytes) currently allowed on the link..
        /// </summary>
        public static string AmqpMessageSizeExceeded {
            get {
                return ResourceManager.GetString("AmqpMessageSizeExceeded", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to The argument {0} is null or white space..
        /// </summary>
        public static string ArgumentNullOrWhiteSpace {
            get {
                return ResourceManager.GetString("ArgumentNullOrWhiteSpace", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to The argument &apos;{0}&apos; cannot exceed {1} characters..
        /// </summary>
        public static string ArgumentStringTooBig {
            get {
                return ResourceManager.GetString("ArgumentStringTooBig", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to There are no brokeredMessages supplied. Please make sure input messages are not empty..
        /// </summary>
        public static string BrokeredMessageListIsNullOrEmpty {
            get {
                return ResourceManager.GetString("BrokeredMessageListIsNullOrEmpty", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Sending empty {0} is not a valid operation..
        /// </summary>
        public static string CannotSendAnEmptyMessage {
            get {
                return ResourceManager.GetString("CannotSendAnEmptyMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Serialization operation failed due to unsupported type {0}..
        /// </summary>
        public static string FailedToSerializeUnsupportedType {
            get {
                return ResourceManager.GetString("FailedToSerializeUnsupportedType", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to The string has an invalid encoding format..
        /// </summary>
        public static string InvalidEncoding {
            get {
                return ResourceManager.GetString("InvalidEncoding", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Argument {0} must be a non-negative timeout value. The provided value was {1}..
        /// </summary>
        public static string TimeoutMustBeNonNegative {
            get {
                return ResourceManager.GetString("TimeoutMustBeNonNegative", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Argument {0} must be a positive timeout value. The provided value was {1}..
        /// </summary>
        public static string TimeoutMustBePositive {
            get {
                return ResourceManager.GetString("TimeoutMustBePositive", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to The provided token does not specify the &apos;Audience&apos; value..
        /// </summary>
        public static string TokenMissingAudience {
            get {
                return ResourceManager.GetString("TokenMissingAudience", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to The provided token does not specify the &apos;ExpiresOn&apos; value..
        /// </summary>
        public static string TokenMissingExpiresOn {
            get {
                return ResourceManager.GetString("TokenMissingExpiresOn", resourceCulture);
            }
        }
    }
}