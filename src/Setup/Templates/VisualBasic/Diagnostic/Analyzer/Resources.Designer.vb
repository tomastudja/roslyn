﻿'------------------------------------------------------------------------------
' <auto-generated>
'     This code was generated by a tool.
'     Runtime Version:4.0.30319.0
'
'     Changes to this file may cause incorrect behavior and will be lost if
'     the code is regenerated.
' </auto-generated>
'------------------------------------------------------------------------------

Option Strict On
Option Explicit On

Imports System
Imports System.Reflection

Namespace My.Resources

    'This class was auto-generated by the StronglyTypedResourceBuilder
    'class via a tool like ResGen or Visual Studio.
    'To add or remove a member, edit your .ResX file then rerun ResGen
    'with the /str option, or rebuild your VS project.
    '''<summary>
    '''  A strongly-typed resource class, for looking up localized strings, etc.
    '''</summary>
    <Global.System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0"),
     Global.System.Diagnostics.DebuggerNonUserCodeAttribute(),
     Global.System.Runtime.CompilerServices.CompilerGeneratedAttribute(),
     Global.Microsoft.VisualBasic.HideModuleNameAttribute()>
    Friend Module Resources

        Private resourceMan As Global.System.Resources.ResourceManager

        Private resourceCulture As Global.System.Globalization.CultureInfo

        '''<summary>
        '''  Returns the cached ResourceManager instance used by this class.
        '''</summary>
        <Global.System.ComponentModel.EditorBrowsableAttribute(Global.System.ComponentModel.EditorBrowsableState.Advanced)>
        Friend ReadOnly Property ResourceManager() As Global.System.Resources.ResourceManager
            Get
                If Object.ReferenceEquals(resourceMan, Nothing) Then
                    Dim temp As Global.System.Resources.ResourceManager = New Global.System.Resources.ResourceManager("$saferootprojectname$.Resources", GetType(Resources).GetTypeInfo.Assembly)
                    resourceMan = temp
                End If
                Return resourceMan
            End Get
        End Property

        '''<summary>
        '''  Overrides the current thread's CurrentUICulture property for all
        '''  resource lookups using this strongly typed resource class.
        '''</summary>
        <Global.System.ComponentModel.EditorBrowsableAttribute(Global.System.ComponentModel.EditorBrowsableState.Advanced)>
        Friend Property Culture() As Global.System.Globalization.CultureInfo
            Get
                Return resourceCulture
            End Get
            Set
                resourceCulture = Value
            End Set
        End Property

        '''<summary>
        '''  Looks up a localized string similar to Type name &apos;{0}&apos; contains lowercase letters.
        '''</summary>
        Friend ReadOnly Property AnalyzerMessageFormat() As String
            Get
                Return ResourceManager.GetString("AnalyzerMessageFormat", resourceCulture)
            End Get
        End Property

        '''<summary>
        '''  Looks up a localized string similar to Type name contains lowercase letters.
        '''</summary>
        Friend ReadOnly Property AnalyzerTitle() As String
            Get
                Return ResourceManager.GetString("AnalyzerTitle", resourceCulture)
            End Get
        End Property

        '''<summary>
        '''  Looks up a localized string similar to Type names should be all uppercase.
        '''</summary>
        Friend ReadOnly Property AnalyzerDescription() As String
            Get
                Return ResourceManager.GetString("AnalyzerDescription", resourceCulture)
            End Get
        End Property
    End Module
End Namespace
