Option Strict On
Option Explicit On
Imports System.Windows.Forms

Namespace Microsoft.VisualStudio.Editors.AppDesCommon

    '**************************************************************************
    ';WaitCursor
    '
    'Remarks:
    '   Utility class that will display a wait cursor over
    '   the lifetime of the object.   It is designed to be used
    '   with the Using keyword as follows:
    '
    '   Sub Func()
    '       Using New WaitCursor
    '           <do work>
    '       End Using
    '   End Sub
    '**************************************************************************
    Friend Class WaitCursor
        Implements IDisposable

        Private PreviousCursor As Cursor


        '**************************************************************************
        ';New
        '
        'Summary:
        '   Constructor
        'Remarks:
        '   Changes the cursor to a wait cursor until the class is Disposed
        '**************************************************************************
        Public Sub New()
            PreviousCursor = Cursor.Current
            Cursor.Current = Cursors.WaitCursor
        End Sub 'Ne


        '**************************************************************************
        ';Dispose
        '
        'Summary:
        '   Disposes the object, and restores the previous cursor.
        'Remarks:
        '   May be called multiple times safely.
        '**************************************************************************
        Public Sub Dispose() Implements IDisposable.Dispose
            If Not (PreviousCursor Is Nothing) Then
                Cursor.Current = PreviousCursor
                PreviousCursor = Nothing
            Else
                Cursor.Current = Cursors.Default
            End If
        End Sub 'IDisposable.Dispose
    End Class 'WaitCursor

End Namespace
