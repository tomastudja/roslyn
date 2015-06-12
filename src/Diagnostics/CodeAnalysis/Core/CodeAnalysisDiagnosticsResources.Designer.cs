﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.CodeAnalysis.Analyzers {
    using System;
    using System.Reflection;
    
    
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
    internal class CodeAnalysisDiagnosticsResources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal CodeAnalysisDiagnosticsResources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.CodeAnalysis.Analyzers.CodeAnalysisDiagnosticsResources", typeof(CodeAnalysisDiagnosticsResources).GetTypeInfo().Assembly);
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
        ///   Looks up a localized string similar to Diagnostic analyzer is marked as supporting only one language, but the analyzer assembly doesn&apos;t seem to refer to any language specific CodeAnalysis assemblies, and so is likely to work for more than one language. Consider adding an additional language argument to DiagnosticAnalyzerAttribute..
        /// </summary>
        internal static string AddLanguageSupportToAnalyzerDescription {
            get {
                return ResourceManager.GetString("AddLanguageSupportToAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Diagnostic analyzer &apos;{0}&apos; may be able to support both C# and Visual Basic. Consider adding an argument to DiagnosticAnalyzerAttribute for &apos;{1}&apos; language support..
        /// </summary>
        internal static string AddLanguageSupportToAnalyzerMessage {
            get {
                return ResourceManager.GetString("AddLanguageSupportToAnalyzerMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Recommend adding language support to diagnostic analyzer..
        /// </summary>
        internal static string AddLanguageSupportToAnalyzerTitle {
            get {
                return ResourceManager.GetString("AddLanguageSupportToAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Apply DiagnosticAnalyzer attribute for &apos;{0}&apos;..
        /// </summary>
        internal static string ApplyDiagnosticAnalyzerAttribute_1 {
            get {
                return ResourceManager.GetString("ApplyDiagnosticAnalyzerAttribute_1", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Apply DiagnosticAnalyzer attribute for both &apos;{0}&apos; and &apos;{1}&apos;..
        /// </summary>
        internal static string ApplyDiagnosticAnalyzerAttribute_2 {
            get {
                return ResourceManager.GetString("ApplyDiagnosticAnalyzerAttribute_2", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A CodeFixProvider that intends to support fix all occurrences must classify the registered code actions into equivalence classes by assigning it an explicit, non-null equivalence key which is unique across all registered code actions by this fixer. This enables the FixAllProvider to fix all diagnostics in the required scope by applying code actions from this fixer that are in the equivalence class of the trigger code action..
        /// </summary>
        internal static string CodeActionNeedsEquivalenceKeyDescription {
            get {
                return ResourceManager.GetString("CodeActionNeedsEquivalenceKeyDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Provide an explicit argument for optional parameter &apos;{0}&apos;, which is non-null and unique across all code actions created by this fixer..
        /// </summary>
        internal static string CreateCodeActionWithEquivalenceKeyMessage {
            get {
                return ResourceManager.GetString("CreateCodeActionWithEquivalenceKeyMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Create code actions should have a unique EquivalenceKey for FixAll occurrences support..
        /// </summary>
        internal static string CreateCodeActionWithEquivalenceKeyTitle {
            get {
                return ResourceManager.GetString("CreateCodeActionWithEquivalenceKeyTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Instance of a diagnostic analyzer might outlive the lifetime of compilation. Hence, storing per-compilation data, such as symbols, into the fields of a diagnostic analyzer might cause stale compilations to stay alive and cause memory leaks.  Instead, you should store this data on a separate type instantiated in a compilation start action, registered using &apos;{0}.{1}&apos; API. An instance of this type will be created per-compilation and it won&apos;t outlive compilation&apos;s lifetime, hence avoiding memory leaks..
        /// </summary>
        internal static string DoNotStorePerCompilationDataOntoFieldsDescription {
            get {
                return ResourceManager.GetString("DoNotStorePerCompilationDataOntoFieldsDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Avoid storing per-compilation data of type &apos;{0}&apos; into the fields of a diagnostic analyzer..
        /// </summary>
        internal static string DoNotStorePerCompilationDataOntoFieldsMessage {
            get {
                return ResourceManager.GetString("DoNotStorePerCompilationDataOntoFieldsMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Avoid storing per-compilation data into the fields of a diagnostic analyzer..
        /// </summary>
        internal static string DoNotStorePerCompilationDataOntoFieldsTitle {
            get {
                return ResourceManager.GetString("DoNotStorePerCompilationDataOntoFieldsTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The author of this interface did not intend to have third party implementations of this interface and reserves the right to change it. Implementing this interface could therefore result in a source or binary compatibility issue with a future version of this interface..
        /// </summary>
        internal static string InternalImplementationOnlyDescription {
            get {
                return ResourceManager.GetString("InternalImplementationOnlyDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Type {0} cannot implement interface {1} because {1} is not available for public implementation..
        /// </summary>
        internal static string InternalImplementationOnlyMessage {
            get {
                return ResourceManager.GetString("InternalImplementationOnlyMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Only internal implementations of this interface are allowed..
        /// </summary>
        internal static string InternalImplementationOnlyTitle {
            get {
                return ResourceManager.GetString("InternalImplementationOnlyTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ReportDiagnostic should only be invoked with supported DiagnosticDescriptors that are returned from DiagnosticAnalyzer.SupportedDiagnostics property. Otherwise, the reported diagnostic will be filtered out by the analysis engine..
        /// </summary>
        internal static string InvalidReportDiagnosticDescription {
            get {
                return ResourceManager.GetString("InvalidReportDiagnosticDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ReportDiagnostic invoked with an unsupported DiagnosticDescriptor &apos;{0}&apos;..
        /// </summary>
        internal static string InvalidReportDiagnosticMessage {
            get {
                return ResourceManager.GetString("InvalidReportDiagnosticMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ReportDiagnostic invoked with an unsupported DiagnosticDescriptor..
        /// </summary>
        internal static string InvalidReportDiagnosticTitle {
            get {
                return ResourceManager.GetString("InvalidReportDiagnosticTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to DiagnosticAnalyzer&apos;s language-specific Register methods, such as RegisterSyntaxNodeAction, RegisterCodeBlockStartAction and RegisterCodeBlockEndAction, expect a language-specific &apos;SyntaxKind&apos; type argument for it&apos;s &apos;{0}&apos; type parameter. Otherwise, the registered analyzer action can never be invoked during analysis..
        /// </summary>
        internal static string InvalidSyntaxKindTypeArgumentDescription {
            get {
                return ResourceManager.GetString("InvalidSyntaxKindTypeArgumentDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Type argument &apos;{0}&apos; for type parameter &apos;{1}&apos; of method &apos;{2}&apos; is not a SyntaxKind enum..
        /// </summary>
        internal static string InvalidSyntaxKindTypeArgumentMessage {
            get {
                return ResourceManager.GetString("InvalidSyntaxKindTypeArgumentMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid type argument for DiagnosticAnalyzer&apos;s Register method..
        /// </summary>
        internal static string InvalidSyntaxKindTypeArgumentTitle {
            get {
                return ResourceManager.GetString("InvalidSyntaxKindTypeArgumentTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Missing &apos;{0}&apos; attribute..
        /// </summary>
        internal static string MissingAttributeMessage {
            get {
                return ResourceManager.GetString("MissingAttributeMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Non-abstract sub-types of DiagnosticAnalyzer should be marked with DiagnosticAnalyzerAttribute(s). The argument to this attribute(s), if any, determine the supported languages for the analyzer. Analyzer types without this attribute will be ignored by the analysis engine..
        /// </summary>
        internal static string MissingDiagnosticAnalyzerAttributeDescription {
            get {
                return ResourceManager.GetString("MissingDiagnosticAnalyzerAttributeDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Missing diagnostic analyzer attribute..
        /// </summary>
        internal static string MissingDiagnosticAnalyzerAttributeTitle {
            get {
                return ResourceManager.GetString("MissingDiagnosticAnalyzerAttributeTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to You must specify at least one syntax/symbol kinds of interest while registering a syntax/symbol analyzer action. Otherwise, the registered action will be dead code and will never be invoked during analysis..
        /// </summary>
        internal static string MissingKindArgumentToRegisterActionDescription {
            get {
                return ResourceManager.GetString("MissingKindArgumentToRegisterActionDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Specify at least one &apos;{0}&apos; of interest while registering a {1} analyzer action..
        /// </summary>
        internal static string MissingKindArgumentToRegisterActionMessage {
            get {
                return ResourceManager.GetString("MissingKindArgumentToRegisterActionMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Missing kind argument while registering an analyzer action..
        /// </summary>
        internal static string MissingKindArgumentToRegisterActionTitle {
            get {
                return ResourceManager.GetString("MissingKindArgumentToRegisterActionTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &apos;{0}&apos; has the default value of &apos;null&apos; for property &apos;{1}&apos;. Either override this property on &apos;{0}&apos; to return a non-null and unique value across all code actions per-fixer or use such an existing code action..
        /// </summary>
        internal static string OverrideCodeActionEquivalenceKeyMessage {
            get {
                return ResourceManager.GetString("OverrideCodeActionEquivalenceKeyMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Use code actions that have a unique EquivalenceKey for FixAll occurrences support..
        /// </summary>
        internal static string OverrideCodeActionEquivalenceKeyTitle {
            get {
                return ResourceManager.GetString("OverrideCodeActionEquivalenceKeyTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &apos;{0}&apos; does not register any analyzer actions. Consider moving actions registered in &apos;{1}&apos; that depend on this start action to &apos;{0}&apos;..
        /// </summary>
        internal static string StartActionWithNoRegisteredActionsMessage {
            get {
                return ResourceManager.GetString("StartActionWithNoRegisteredActionsMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Start action has no registered actions..
        /// </summary>
        internal static string StartActionWithNoRegisteredActionsTitle {
            get {
                return ResourceManager.GetString("StartActionWithNoRegisteredActionsTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &apos;{0}&apos; does not register any analyzer actions, except for a &apos;{1}&apos;. Consider replacing this start/end action pair with a &apos;{2}&apos; or moving actions registered in &apos;{3}&apos; that depend on this start action to &apos;{0}&apos;..
        /// </summary>
        internal static string StartActionWithOnlyEndActionMessage {
            get {
                return ResourceManager.GetString("StartActionWithOnlyEndActionMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Start action has no registered non-end actions..
        /// </summary>
        internal static string StartActionWithOnlyEndActionTitle {
            get {
                return ResourceManager.GetString("StartActionWithOnlyEndActionTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An analyzer start action enables performing stateful analysis over a given code unit, such as a code block, compilation, etc. Careful design is necessary to achieve efficient analyzer execution without memory leaks. Use the following guidelines for writing such analyzers:
        ///1. Define a new scope for the registered start action, possibly with a private nested type for analyzing each code unit.
        ///2. If required, define and initialize state in the start action.
        ///3. Register at least one non-end action that refer [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string StatefulAnalyzerRegisterActionsDescription {
            get {
                return ResourceManager.GetString("StatefulAnalyzerRegisterActionsDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SymbolKind &apos;{0}&apos; is not supported for symbol analyzer actions..
        /// </summary>
        internal static string UnsupportedSymbolKindArgumentToRegisterActionMessage {
            get {
                return ResourceManager.GetString("UnsupportedSymbolKindArgumentToRegisterActionMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unsupported SymbolKind argument while registering a symbol analyzer action..
        /// </summary>
        internal static string UnsupportedSymbolKindArgumentToRegisterActionTitle {
            get {
                return ResourceManager.GetString("UnsupportedSymbolKindArgumentToRegisterActionTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to If your diagnostic analyzer and it&apos;s reported diagnostics need to be localizable, then the supported DiagnosticDescriptors used for constructing the diagnostics must also be localizable. If so, then localizable argument(s) must be provided for parameter &apos;title&apos; (and optionally &apos;description&apos;) to the diagnostic descriptor constructor to ensure that the descriptor is localizable..
        /// </summary>
        internal static string UseLocalizableStringsInDescriptorDescription {
            get {
                return ResourceManager.GetString("UseLocalizableStringsInDescriptorDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Consider providing localizable arguments of type &apos;{0}&apos; to diagnostic descriptor constructor to ensure the descriptor is localizable..
        /// </summary>
        internal static string UseLocalizableStringsInDescriptorMessage {
            get {
                return ResourceManager.GetString("UseLocalizableStringsInDescriptorMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Provide localizable arguments to diagnostic descriptor constructor..
        /// </summary>
        internal static string UseLocalizableStringsInDescriptorTitle {
            get {
                return ResourceManager.GetString("UseLocalizableStringsInDescriptorTitle", resourceCulture);
            }
        }
    }
}
