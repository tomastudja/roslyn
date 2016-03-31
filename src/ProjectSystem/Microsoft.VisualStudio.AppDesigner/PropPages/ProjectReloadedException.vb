Imports Microsoft.VisualBasic
Imports System
Imports System.Diagnostics
Imports System.Runtime.Serialization
Imports Microsoft.VisualStudio.Editors

Namespace Microsoft.VisualStudio.Editors.PropertyPages

    ''' <summary>
    ''' An exception that we throw internally in some situations when the project is unloaded
    '''   because of a programmatic action that we take (e.g., checking out the project file,
    '''   setting the target framework property).
    ''' </summary>
    ''' <remarks>
    ''' This exception should not be allowed to bubble up to the user.
    ''' </remarks>
    <Serializable()> _
    Public Class ProjectReloadedException
        Inherits Exception

        Public Sub New()
            MyBase.New(SR.GetString(SR.PPG_ProjectReloadedSomePropertiesMayNotHaveBeenSet))
        End Sub

        ''' <summary>
        ''' Deserialization constructor.  Required for serialization/remotability support
        '''   (not that we expect this to be needed).
        ''' </summary>
        ''' <param name="Info"></param>
        ''' <param name="Context"></param>
        ''' <remarks>
        '''See .NET Framework Developer's Guide, "Custom Serialization" for more information
        ''' </remarks>
        Private Sub New(ByVal Info As SerializationInfo, ByVal Context As StreamingContext)
            MyBase.New(Info, Context)
        End Sub

    End Class

End Namespace
