﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.18444
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace TSO_LoginServer {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "12.0.0.0")]
    internal sealed partial class GlobalSettings : global::System.Configuration.ApplicationSettingsBase {
        
        private static GlobalSettings defaultInstance = ((GlobalSettings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new GlobalSettings())));
        
        public static GlobalSettings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool CreateAccountsOnLogin {
            get {
                return ((bool)(this["CreateAccountsOnLogin"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("127.0.0.1")]
        public string ListeningIP {
            get {
                return ((string)(this["ListeningIP"]));
            }
            set {
                this["ListeningIP"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("2106")]
        public string ListeningPort {
            get {
                return ((string)(this["ListeningPort"]));
            }
            set {
                this["ListeningPort"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0.1.24.0")]
        public string ClientVersion {
            get {
                return ((string)(this["ClientVersion"]));
            }
            set {
                this["ClientVersion"] = value;
            }
        }
    }
}
