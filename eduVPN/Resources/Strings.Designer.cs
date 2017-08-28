﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace eduVPN.Resources {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Strings {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Strings() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("eduVPN.Resources.Strings", typeof(Strings).Assembly);
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
        ///   Looks up a localized string similar to eduVPN for Windows.
        /// </summary>
        internal static string CertificateTitle {
            get {
                return ResourceManager.GetString("CertificateTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to API server replied with an error..
        /// </summary>
        internal static string ErrorAPIServerReply {
            get {
                return ResourceManager.GetString("ErrorAPIServerReply", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Client authorization failed..
        /// </summary>
        internal static string ErrorAuthorization {
            get {
                return ResourceManager.GetString("ErrorAuthorization", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Loading of client certificate failed..
        /// </summary>
        internal static string ErrorClientCertificateLoad {
            get {
                return ResourceManager.GetString("ErrorClientCertificateLoad", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Loading of API endpoints failed..
        /// </summary>
        internal static string ErrorEndpointsLoad {
            get {
                return ResourceManager.GetString("ErrorEndpointsLoad", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Loading of the instance source {0} failed..
        /// </summary>
        internal static string ErrorInstanceSourceInfoLoad {
            get {
                return ResourceManager.GetString("ErrorInstanceSourceInfoLoad", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Private key is invalid or of unsupported format..
        /// </summary>
        internal static string ErrorInvalidPrivateKey {
            get {
                return ResourceManager.GetString("ErrorInvalidPrivateKey", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Verifying signature failed. Integrity of {0} is not valid..
        /// </summary>
        internal static string ErrorInvalidSignature {
            get {
                return ResourceManager.GetString("ErrorInvalidSignature", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No access token granted..
        /// </summary>
        internal static string ErrorNullAccessToken {
            get {
                return ResourceManager.GetString("ErrorNullAccessToken", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Loading of profile configuration failed..
        /// </summary>
        internal static string ErrorProfileConfigLoad {
            get {
                return ResourceManager.GetString("ErrorProfileConfigLoad", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Loading of the profile list failed..
        /// </summary>
        internal static string ErrorProfileListLoad {
            get {
                return ResourceManager.GetString("ErrorProfileListLoad", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Saving of profile&apos;s OpenVPN configuration to &quot;{0}&quot; failed..
        /// </summary>
        internal static string ErrorSavingProfileConfiguration {
            get {
                return ResourceManager.GetString("ErrorSavingProfileConfiguration", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Loading of user info failed..
        /// </summary>
        internal static string ErrorUserInfoLoad {
            get {
                return ResourceManager.GetString("ErrorUserInfoLoad", resourceCulture);
            }
        }
    }
}
