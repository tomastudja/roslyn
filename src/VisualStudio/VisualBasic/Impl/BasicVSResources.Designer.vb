﻿'------------------------------------------------------------------------------
' <auto-generated>
'     This code was generated by a tool.
'     Runtime Version:4.0.30319.42000
'
'     Changes to this file may cause incorrect behavior and will be lost if
'     the code is regenerated.
' </auto-generated>
'------------------------------------------------------------------------------

Option Strict On
Option Explicit On

Imports System

Namespace Microsoft.VisualStudio.LanguageServices.VisualBasic
    
    'This class was auto-generated by the StronglyTypedResourceBuilder
    'class via a tool like ResGen or Visual Studio.
    'To add or remove a member, edit your .ResX file then rerun ResGen
    'with the /str option, or rebuild your VS project.
    '''<summary>
    '''  A strongly-typed resource class, for looking up localized strings, etc.
    '''</summary>
    <Global.System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0"),  _
     Global.System.Diagnostics.DebuggerNonUserCodeAttribute(),  _
     Global.System.Runtime.CompilerServices.CompilerGeneratedAttribute()>  _
    Friend Class BasicVSResources
        
        Private Shared resourceMan As Global.System.Resources.ResourceManager
        
        Private Shared resourceCulture As Global.System.Globalization.CultureInfo
        
        <Global.System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")>  _
        Friend Sub New()
            MyBase.New
        End Sub
        
        '''<summary>
        '''  Returns the cached ResourceManager instance used by this class.
        '''</summary>
        <Global.System.ComponentModel.EditorBrowsableAttribute(Global.System.ComponentModel.EditorBrowsableState.Advanced)>  _
        Friend Shared ReadOnly Property ResourceManager() As Global.System.Resources.ResourceManager
            Get
                If Object.ReferenceEquals(resourceMan, Nothing) Then
                    Dim temp As Global.System.Resources.ResourceManager = New Global.System.Resources.ResourceManager("BasicVSResources", GetType(BasicVSResources).Assembly)
                    resourceMan = temp
                End If
                Return resourceMan
            End Get
        End Property
        
        '''<summary>
        '''  Overrides the current thread's CurrentUICulture property for all
        '''  resource lookups using this strongly typed resource class.
        '''</summary>
        <Global.System.ComponentModel.EditorBrowsableAttribute(Global.System.ComponentModel.EditorBrowsableState.Advanced)>  _
        Friend Shared Property Culture() As Global.System.Globalization.CultureInfo
            Get
                Return resourceCulture
            End Get
            Set
                resourceCulture = value
            End Set
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Insert Snippet.
        '''</summary>
        Friend Shared ReadOnly Property InsertSnippet() As String
            Get
                Return ResourceManager.GetString("InsertSnippet", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to IntelliSense.
        '''</summary>
        Friend Shared ReadOnly Property Intellisense() As String
            Get
                Return ResourceManager.GetString("Intellisense", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Microsoft Visual Basic.
        '''</summary>
        Friend Shared ReadOnly Property MicrosoftVisualBasic() As String
            Get
                Return ResourceManager.GetString("MicrosoftVisualBasic", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to _Move local declaration to the extracted method if it is not used elsewhere.
        '''</summary>
        Friend Shared ReadOnly Property Option_AllowMovingDeclaration() As String
            Get
                Return ResourceManager.GetString("Option_AllowMovingDeclaration", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Automatic _insertion of Interface and MustOverride members.
        '''</summary>
        Friend Shared ReadOnly Property Option_AutomaticInsertionOfInterfaceAndMustOverrideMembers() As String
            Get
                Return ResourceManager.GetString("Option_AutomaticInsertionOfInterfaceAndMustOverrideMembers", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Enable full solution _analysis.
        '''</summary>
        Friend Shared ReadOnly Property Option_ClosedFileDiagnostics() As String
            Get
                Return ResourceManager.GetString("Option_ClosedFileDiagnostics", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to _Show procedure line separators.
        '''</summary>
        Friend Shared ReadOnly Property Option_DisplayLineSeparators() As String
            Get
                Return ResourceManager.GetString("Option_DisplayLineSeparators", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to _Don&apos;t put ByRef on custom structure.
        '''</summary>
        Friend Shared ReadOnly Property Option_DontPutOutOrRefOnStruct() As String
            Get
                Return ResourceManager.GetString("Option_DontPutOutOrRefOnStruct", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Editor Help.
        '''</summary>
        Friend Shared ReadOnly Property Option_EditorHelp() As String
            Get
                Return ResourceManager.GetString("Option_EditorHelp", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to A_utomatic insertion of end constructs.
        '''</summary>
        Friend Shared ReadOnly Property Option_EnableEndConstruct() As String
            Get
                Return ResourceManager.GetString("Option_EnableEndConstruct", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Highlight related _keywords under cursor.
        '''</summary>
        Friend Shared ReadOnly Property Option_EnableHighlightKeywords() As String
            Get
                Return ResourceManager.GetString("Option_EnableHighlightKeywords", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to _Highlight references to symbol under cursor.
        '''</summary>
        Friend Shared ReadOnly Property Option_EnableHighlightReferences() As String
            Get
                Return ResourceManager.GetString("Option_EnableHighlightReferences", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to _Pretty listing (reformatting) of code.
        '''</summary>
        Friend Shared ReadOnly Property Option_EnableLineCommit() As String
            Get
                Return ResourceManager.GetString("Option_EnableLineCommit", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to _Enter outlining mode when files open.
        '''</summary>
        Friend Shared ReadOnly Property Option_EnableOutlining() As String
            Get
                Return ResourceManager.GetString("Option_EnableOutlining", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Extract Method.
        '''</summary>
        Friend Shared ReadOnly Property Option_ExtractMethod() As String
            Get
                Return ResourceManager.GetString("Option_ExtractMethod", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to _Generate XML documentation comments for &apos;&apos;&apos;.
        '''</summary>
        Friend Shared ReadOnly Property Option_GenerateXmlDocCommentsForTripleApostrophes() As String
            Get
                Return ResourceManager.GetString("Option_GenerateXmlDocCommentsForTripleApostrophes", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Go to Definition.
        '''</summary>
        Friend Shared ReadOnly Property Option_GoToDefinition() As String
            Get
                Return ResourceManager.GetString("Option_GoToDefinition", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Highlighting.
        '''</summary>
        Friend Shared ReadOnly Property Option_Highlighting() As String
            Get
                Return ResourceManager.GetString("Option_Highlighting", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to _Navigate to Object Browser for symbols defined in metadata.
        '''</summary>
        Friend Shared ReadOnly Property Option_NavigateToObjectBrowser() As String
            Get
                Return ResourceManager.GetString("Option_NavigateToObjectBrowser", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Optimize for solution size.
        '''</summary>
        Friend Shared ReadOnly Property Option_OptimizeForSolutionSize() As String
            Get
                Return ResourceManager.GetString("Option_OptimizeForSolutionSize", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Large.
        '''</summary>
        Friend Shared ReadOnly Property Option_OptimizeForSolutionSize_Large() As String
            Get
                Return ResourceManager.GetString("Option_OptimizeForSolutionSize_Large", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Regular.
        '''</summary>
        Friend Shared ReadOnly Property Option_OptimizeForSolutionSize_Regular() As String
            Get
                Return ResourceManager.GetString("Option_OptimizeForSolutionSize_Regular", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Small.
        '''</summary>
        Friend Shared ReadOnly Property Option_OptimizeForSolutionSize_Small() As String
            Get
                Return ResourceManager.GetString("Option_OptimizeForSolutionSize_Small", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Outlining.
        '''</summary>
        Friend Shared ReadOnly Property Option_Outlining() As String
            Get
                Return ResourceManager.GetString("Option_Outlining", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Performance.
        '''</summary>
        Friend Shared ReadOnly Property Option_Performance() As String
            Get
                Return ResourceManager.GetString("Option_Performance", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Show preview for _rename tracking.
        '''</summary>
        Friend Shared ReadOnly Property Option_RenameTrackingPreview() As String
            Get
                Return ResourceManager.GetString("Option_RenameTrackingPreview", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Prefer intrinsic predefined type keyword when declaring locals, parameters and members.
        '''</summary>
        Friend Shared ReadOnly Property PreferIntrinsicPredefinedTypeKeywordInDeclaration() As String
            Get
                Return ResourceManager.GetString("PreferIntrinsicPredefinedTypeKeywordInDeclaration", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Prefer intrinsic predefined type keyword in member access expressions.
        '''</summary>
        Friend Shared ReadOnly Property PreferIntrinsicPredefinedTypeKeywordInMemberAccess() As String
            Get
                Return ResourceManager.GetString("PreferIntrinsicPredefinedTypeKeywordInMemberAccess", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Qualify member access with &apos;Me&apos;.
        '''</summary>
        Friend Shared ReadOnly Property QualifyMemberAccessWithMe() As String
            Get
                Return ResourceManager.GetString("QualifyMemberAccessWithMe", resourceCulture)
            End Get
        End Property
    End Class
End Namespace
