﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Namespace Microsoft.CodeAnalysis.Editor.UnitTests.IntelliSense
    <[UseExportProvider]>
    Public Class CSharpCompletionCommandHandlerTests_Conversions
        <WpfTheory, CombinatorialData, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function BuiltInConversion(showCompletionInArgumentLists As Boolean) As Task
            Using state = TestStateFactory.CreateCSharpTestState(
                <Document><![CDATA[
using System.Text.RegularExpressions;
class C
{
    void goo()
    {
        var x = 0;
        var y = x.$$
    }
}
]]></Document>, showCompletionInArgumentLists:=showCompletionInArgumentLists)

                state.SendInvokeCompletionList()
                state.SendTypeChars("by")
                Await state.AssertSelectedCompletionItem("(byte)")
                state.SendTab()
                Await state.AssertNoCompletionSession()
                Assert.Equal("        var y = ((byte)x)", state.GetLineTextFromCaretPosition())
                Assert.Equal(state.GetLineFromCurrentCaretPosition().End, state.GetCaretPoint().BufferPosition)
            End Using
        End Function

        <WpfTheory, CombinatorialData, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function BuiltInConversion_BetweenDots(showCompletionInArgumentLists As Boolean) As Task
            Using state = TestStateFactory.CreateCSharpTestState(
                <Document><![CDATA[
using System.Text.RegularExpressions;
class C
{
    void goo()
    {
        var x = 0;
        var y = x.$$.ToString();
    }
}
]]></Document>, showCompletionInArgumentLists:=showCompletionInArgumentLists)

                state.SendInvokeCompletionList()
                state.SendTypeChars("by")
                Await state.AssertSelectedCompletionItem("(byte)")
                state.SendTab()
                Await state.AssertNoCompletionSession()
                Assert.Equal("        var y = ((byte)x).ToString();", state.GetLineTextFromCaretPosition())
                Assert.Equal(".", state.GetCaretPoint().BufferPosition.GetChar())
            End Using
        End Function

        <WpfTheory, CombinatorialData, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function BuiltInConversion_PartiallyWritten_Before(showCompletionInArgumentLists As Boolean) As Task
            Using state = TestStateFactory.CreateCSharpTestState(
                <Document><![CDATA[
using System.Text.RegularExpressions;
class C
{
    void goo()
    {
        var x = 0;
        var y = x.$$by.ToString();
    }
}
]]></Document>, showCompletionInArgumentLists:=showCompletionInArgumentLists)

                state.SendInvokeCompletionList()
                Await state.AssertSelectedCompletionItem("CompareTo")
            End Using
        End Function

        <WpfTheory, CombinatorialData, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function BuiltInConversion_PartiallyWritten_After(showCompletionInArgumentLists As Boolean) As Task
            Using state = TestStateFactory.CreateCSharpTestState(
                <Document><![CDATA[
using System.Text.RegularExpressions;
class C
{
    void goo()
    {
        var x = 0;
        var y = x.by$$.ToString();
    }
}
]]></Document>, showCompletionInArgumentLists:=showCompletionInArgumentLists)

                state.SendInvokeCompletionList()
                Await state.AssertSelectedCompletionItem("(byte)")
                state.SendTab()
                Await state.AssertNoCompletionSession()
                Assert.Equal("        var y = ((byte)x).ToString();", state.GetLineTextFromCaretPosition())
                Assert.Equal(".", state.GetCaretPoint().BufferPosition.GetChar())
            End Using
        End Function

        <WpfTheory, CombinatorialData, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function BuiltInConversion_NullableType_Dot(showCompletionInArgumentLists As Boolean) As Task
            Using state = TestStateFactory.CreateCSharpTestState(
                <Document><![CDATA[
using System.Text.RegularExpressions;
class C
{
    void goo()
    {
        var x = (int?)0;
        var y = x.$$
    }
}
]]></Document>, showCompletionInArgumentLists:=showCompletionInArgumentLists)

                state.SendInvokeCompletionList()
                state.SendTypeChars("by")
                Await state.AssertSelectedCompletionItem("(byte?)")
                state.SendTab()
                Await state.AssertNoCompletionSession()
                Assert.Equal("        var y = ((byte?)x)", state.GetLineTextFromCaretPosition())
                Assert.Equal(state.GetLineFromCurrentCaretPosition().End, state.GetCaretPoint().BufferPosition)
            End Using
        End Function

        <WpfTheory, CombinatorialData, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function BuiltInConversion_NullableType_Question_BetweenDots(showCompletionInArgumentLists As Boolean) As Task
            Using state = TestStateFactory.CreateCSharpTestState(
                <Document><![CDATA[
using System.Text.RegularExpressions;
class C
{
    void goo()
    {
        var x = (int?)0;
        var y = x?.$$.ToString();
    }
}
]]></Document>, showCompletionInArgumentLists:=showCompletionInArgumentLists)

                state.SendInvokeCompletionList()
                state.SendTypeChars("by")
                Await state.AssertSelectedCompletionItem("(byte?)")
                state.SendTab()
                Await state.AssertNoCompletionSession()
                Assert.Equal("        var y = ((byte?)x)?.ToString();", state.GetLineTextFromCaretPosition())
                Assert.Equal(".", state.GetCaretPoint().BufferPosition.GetChar())
            End Using
        End Function

        Private Shared Async Function VerifyCustomCommitProviderAsync(markupBeforeCommit As String, itemToCommit As String, expectedCodeAfterCommit As String, Optional commitChar As Char? = Nothing) As Task
            Using state = TestStateFactory.CreateCSharpTestState(
                    New XElement("Document", markupBeforeCommit.Replace(vbCrLf, vbLf)))

                state.SendInvokeCompletionList()
                Await state.AssertCompletionSession()
                state.SendSelectCompletionItem(itemToCommit)
                state.SendTab()
                Await state.AssertNoCompletionSession()

                Dim expected As String = Nothing
                Dim cursorPosition As Integer = 0
                MarkupTestFile.GetPosition(expectedCodeAfterCommit, expected, cursorPosition)

                Assert.Equal(expected, state.SubjectBuffer.CurrentSnapshot.GetText())
                Assert.Equal(cursorPosition, state.TextView.Caret.Position.BufferPosition.Position)
            End Using
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(47511, "https://github.com/dotnet/roslyn/issues/47511")>
        Public Async Function ExplicitBuiltInEnumConversionsIsApplied() As Task
            ' built-in enum conversions:
            ' https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/conversions#explicit-enumeration-conversions
            Await VerifyCustomCommitProviderAsync("
public enum E { One }
public class Program
{
    public static void Main()
    {
        var e = E.One;
        e.$$
    }
}
", "(int)", "
public enum E { One }
public class Program
{
    public static void Main()
    {
        var e = E.One;
        ((int)e)$$
    }
}
")
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(47511, "https://github.com/dotnet/roslyn/issues/47511")>
        Public Async Function ExplicitBuiltInEnumConversionsAreLifted() As Task
            ' built-in enum conversions:
            ' https//docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/conversions#explicit-enumeration-conversions
            Await VerifyCustomCommitProviderAsync("
public enum E { One }
public class Program
{
    public static void Main()
    {
        E? e = null;
        e.$$
    }
}
", "(int?)", "
public enum E { One }
public class Program
{
    public static void Main()
    {
        E? e = null;
        ((int?)e)$$
    }
}
")

        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(47511, "https://github.com/dotnet/roslyn/issues/47511")>
        Public Async Function ExplicitBuiltInNumericConversionsAreLifted() As Task
            ' built-in numeric conversions:
            ' https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions
            Await VerifyCustomCommitProviderAsync("
public class Program
{
    public static void Main()
    {
        long? l = 0;
        l.$$
    }
}
", "(int?)", "
public class Program
{
    public static void Main()
    {
        long? l = 0;
        ((int?)l)$$
    }
}
")
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(47511, "https://github.com/dotnet/roslyn/issues/47511")>
        Public Async Function ExplicitBuiltInNumericConversionsAreOffered() As Task
            ' built-in numeric conversions:
            ' https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions
            Await VerifyCustomCommitProviderAsync("
public class Program
{
    public static void Main()
    {
        long l = 0;
        l.$$
    }
}
", "(int)", "
public class Program
{
    public static void Main()
    {
        long l = 0;
        ((int)l)$$
    }
}
")
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(47511, "https://github.com/dotnet/roslyn/issues/47511")>
        Public Async Function ExplicitUserDefinedConversionNullForgivingOperatorHandling() As Task
            Await VerifyCustomCommitProviderAsync("
#nullable enable

public class C {
    public static explicit operator int(C c) => 0;
}

public class Program
{
    public static void Main()
    {
        C? c = null;
        var i = c!.$$
    }
}
", "(int)", "
#nullable enable

public class C {
    public static explicit operator int(C c) => 0;
}

public class Program
{
    public static void Main()
    {
        C? c = null;
        var i = ((int)c!)$$
    }
}
")
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(47511, "https://github.com/dotnet/roslyn/issues/47511")>
        Public Async Function ExplicitConversionOfConditionalAccessOfStructAppliesNullableStruct() As Task
            ' see https://sharplab.io/#gist:08c697b6b9b6384b8ec81cc586e064e6 to run a sample
            ' conversion ((int)c?.S) fails with System.InvalidOperationException: Nullable object must have a value.
            ' conversion ((int?)c?.S) passes (returns an int? with HasValue == false)
            Await VerifyCustomCommitProviderAsync("
public struct S {
    public static explicit operator int(S _) => 0;
}
public class C {
    public S S { get; } = default;
}
public class Program
{
    public static void Main()
    {
        C c = null;
        c?.S.$$
    }
}
", "(int?)", "
public struct S {
    public static explicit operator int(S _) => 0;
}
public class C {
    public S S { get; } = default;
}
public class Program
{
    public static void Main()
    {
        C c = null;
        ((int?)c?.S)$$
    }
}
")
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(47511, "https://github.com/dotnet/roslyn/issues/47511")>
        Public Async Function ExplicitConversionOfNullableStructToNullableStructIsApplied() As Task
            ' Lifted conversion https://docs.microsoft.com/hu-hu/dotnet/csharp/language-reference/language-specification/conversions#lifted-conversion-operators
            Await VerifyCustomCommitProviderAsync("
public struct S {
    public static explicit operator int(S _) => 0;
}
public class Program
{
    public static void Main()
    {
        S? s = null;
        s.$$
    }
}
", "(int?)", "
public struct S {
    public static explicit operator int(S _) => 0;
}
public class Program
{
    public static void Main()
    {
        S? s = null;
        ((int?)s)$$
    }
}
")
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(47511, "https://github.com/dotnet/roslyn/issues/47511")>
        Public Async Function ExplicitUserDefinedConversionOfNullableStructAccessViaNullcondionalOffersLiftedConversion() As Task
            Await VerifyCustomCommitProviderAsync("
public struct S {
    public static explicit operator int(S s) => 0;
}
public class Program
{
    public static void Main()
    {
        S? s = null;
        var i = ((S?)s)?.$$
    }
}
", "(int?)", "
public struct S {
    public static explicit operator int(S s) => 0;
}
public class Program
{
    public static void Main()
    {
        S? s = null;
        var i = ((int?)((S?)s))?$$
    }
}
")
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(47511, "https://github.com/dotnet/roslyn/issues/47511")>
        Public Async Function ExplicitUserDefinedConversionOfPropertyNamedLikeItsTypeIsHandled() As Task
            Await VerifyCustomCommitProviderAsync("
public struct S {
    public static explicit operator int(S s) => 0;
}
public class C {
    public S S { get; }
}
public class Program
{
    public static void Main()
    {
        var c = new C();
        var i = c.S.$$
    }
}
", "(int)", "
public struct S {
    public static explicit operator int(S s) => 0;
}
public class C {
    public S S { get; }
}
public class Program
{
    public static void Main()
    {
        var c = new C();
        var i = ((int)c.S)$$
    }
}
")
        End Function

        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(47511, "https://github.com/dotnet/roslyn/issues/47511")>
        <CombinatorialData>
        Public Async Function ExplicitConversionOfConditionalAccessFromClassOrStructToClassOrStruct(
            <CombinatorialValues("struct", "class")> fromClassOrStruct As String,
            <CombinatorialValues("struct", "class")> toClassOrStruct As String,
            propertyIsNullable As Boolean,
            conditionalAccess As Boolean) As Task

            If fromClassOrStruct = "class" AndAlso propertyIsNullable Then
                ' This test Is solely about lifting of nullable value types. The CombinatorialData also 
                ' adds cases for nullable reference types: public class From ... public From? From { get; }
                ' We don't want to test NRT cases here.
                Return
            End If

            Dim assertShouldBeNullable =
                fromClassOrStruct = "struct" AndAlso
                toClassOrStruct = "struct" AndAlso
                (propertyIsNullable OrElse conditionalAccess)

            Dim propertyNullableQuestionMark = If(propertyIsNullable, "?", "")
            Dim conditionalAccessQuestionMark = If(conditionalAccess, "?", "")
            Dim shouldBeNullableQuestionMark = If(assertShouldBeNullable, "?", "")
            Await VerifyCustomCommitProviderAsync($"
public {fromClassOrStruct} From {{
    public static explicit operator To(From _) => default;
}}
public {toClassOrStruct} To {{
}}
public class C {{
    public From{propertyNullableQuestionMark} From {{ get; }} = default;
}}
public class Program
{{
    public static void Main()
    {{
        C c = null;
        c{conditionalAccessQuestionMark}.From.$$
    }}
}}
", $"(To{shouldBeNullableQuestionMark})", $"
public {fromClassOrStruct} From {{
    public static explicit operator To(From _) => default;
}}
public {toClassOrStruct} To {{
}}
public class C {{
    public From{propertyNullableQuestionMark} From {{ get; }} = default;
}}
public class Program
{{
    public static void Main()
    {{
        C c = null;
        ((To{shouldBeNullableQuestionMark})c{conditionalAccessQuestionMark}.From)$$
    }}
}}
")
        End Function
    End Class
End Namespace
