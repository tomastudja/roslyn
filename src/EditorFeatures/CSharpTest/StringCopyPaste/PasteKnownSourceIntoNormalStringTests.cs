﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.StringCopyPaste
{
    public class PasteKnownSourceIntoNormalStringTests : StringCopyPasteCommandHandlerKnownSourceTests
    {
        [WpfFact]
        public void TestPasteSimpleNormalLiteralContent()
        {
            TestCopyPaste(
@"var v = ""{|Copy:goo|}"";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""goo[||]"";",
@"
var dest =
    ""[||]"";");
        }

        [WpfFact]
        public void TestPasteSimpleSubstringNormalLiteralContent()
        {
            TestCopyPaste(
@"var v = ""g{|Copy:o|}o"";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""o[||]"";",
@"
var dest =
    ""[||]"";");
        }

        [WpfFact]
        public void TestPastePartiallySelectedEscapeNormalLiteralContent()
        {
            TestCopyPaste(
@"var v = ""\{|Copy:n|}"";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""n[||]"";",
@"
var dest =
    ""[||]"";");
        }

        [WpfFact]
        public void TestPasteFullySelectedEscapeNormalLiteralContent()
        {
            TestCopyPaste(
@"var v = ""{|Copy:\n|}"";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""\n[||]"";",
@"
var dest =
    ""[||]"";");
        }

        [WpfFact]
        public void TestPastePartiallySelectedQuoteNormalLiteralContent()
        {
            TestCopyPaste(
@"var v = ""\{|Copy:""|}"";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""\""[||]"";",
@"
var dest =
    """"[||]"";");
        }

        [WpfFact]
        public void TestPasteFullySelectedQuoteNormalLiteralContent()
        {
            TestCopyPaste(
@"var v = ""{|Copy:\""|}"";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""\""[||]"";",
@"
var dest =
    ""[||]"";");
        }

        [WpfFact]
        public void TestPasteSimpleVerbatimLiteralContent()
        {
            TestCopyPaste(
@"var v = @""{|Copy:goo|}"";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""goo[||]"";",
@"
var dest =
    ""[||]"";");
        }

        [WpfFact]
        public void TestPasteSimpleSubstringVerbatimLiteralContent()
        {
            TestCopyPaste(
@"var v = @""g{|Copy:o|}o"";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""o[||]"";",
@"
var dest =
    ""[||]"";");
        }

        [WpfFact]
        public void TestPasteSelectedVerbatimNewLineLiteralContent()
        {
            TestCopyPaste(
"var v = @\"{|Copy:\r\n|}\";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""\r\n[||]"";",
"\r\nvar dest =\r\n    \"\r\n[||]\";");
        }

        [WpfFact]
        public void TestPasteFullySelectedEscapeVerbatimLiteralContent()
        {
            TestCopyPaste(
@"var v = @""{|Copy:""""|}"";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""\""[||]"";",
@"
var dest =
    """"""[||]"";");
        }

        [WpfFact]
        public void TestPasteSimpleRawSingleLineLiteralContent()
        {
            TestCopyPaste(
@"var v = """"""{|Copy:goo|}"""""";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""goo[||]"";",
@"
var dest =
    ""[||]"";");
        }

        [WpfFact]
        public void TestPasteQuotesRawSingleLineLiteralContent()
        {
            TestCopyPaste(
@"var v = """"""{|Copy: """" |}"""""";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    "" \""\"" [||]"";",
@"
var dest =
    "" """" [||]"";");
        }

        [WpfFact]
        public void TestPasteSimpleRawMultiLineLiteralContent1()
        {
            TestCopyPaste(
@"var v = """"""
    {|Copy:goo|}
    """""";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""goo[||]"";",
@"
var dest =
    ""[||]"";");
        }

        [WpfFact]
        public void TestPasteSimpleRawMultiLineLiteralContent2()
        {
            TestCopyPaste(
@"var v = """"""
    {|Copy:goo
    bar|}
    """""";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""goo\r\nbar[||]"";",
@"
var dest =
    ""goo
    bar[||]"";");
        }

        [WpfFact]
        public void TestPasteSimpleRawMultiLineLiteralContent3()
        {
            TestCopyPaste(
@"var v = """"""
{|Copy:    goo
    bar|}
    """""";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""goo\r\nbar[||]"";",
@"
var dest =
    ""    goo
    bar[||]"";");
        }

        [WpfFact]
        public void TestPasteInterpolationFromInterpolatedStringLiteralContent()
        {
            TestCopyPaste(
@"var v = $""{|Copy:{0}|}"";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""{0}[||]"";",
@"
var dest =
    ""[||]"";");
        }

        [WpfFact]
        public void TestPasteInterpolationWIthStringFromInterpolatedStringLiteralContent1()
        {
            TestCopyPaste(
@"var v = $""{|Copy:{""goo""}|}"";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""{\""goo\""}[||]"";",
@"
var dest =
    ""{""goo""}[||]"";");
        }

        [WpfFact]
        public void TestPasteInterpolationWIthStringFromInterpolatedStringLiteralContent2()
        {
            TestCopyPaste(
@"var v = $""{|Copy:X{""goo""}Y|}"";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""X{\""goo\""}Y[||]"";",
@"
var dest =
    ""X{""goo""}Y[||]"";");
        }

        [WpfFact]
        public void TestPasteInterpolationWIthStringFromInterpolatedStringLiteralContent3()
        {
            TestCopyPaste(
@"var v = $""{|Copy:\""X{""goo""}Y\""|}"";",
@"
var dest =
    ""[||]"";",
@"
var dest =
    ""\""X{\""goo\""}Y\""[||]"";",
@"
var dest =
    ""\""X{""goo""}Y\""[||]"";");
        }
    }
}
