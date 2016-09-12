// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.UnitTests.Classification;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.QuickInfo;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.UnitTests.QuickInfo
{
    public abstract class AbstractSemanticQuickInfoSourceTests
    {
        protected readonly ClassificationBuilder ClassificationBuilder;

        protected AbstractSemanticQuickInfoSourceTests()
        {
            this.ClassificationBuilder = new ClassificationBuilder();
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> Struct(string value)
        {
            return ClassificationBuilder.Struct(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> Enum(string value)
        {
            return ClassificationBuilder.Enum(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> Interface(string value)
        {
            return ClassificationBuilder.Interface(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> Class(string value)
        {
            return ClassificationBuilder.Class(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> Delegate(string value)
        {
            return ClassificationBuilder.Delegate(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> TypeParameter(string value)
        {
            return ClassificationBuilder.TypeParameter(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> String(string value)
        {
            return ClassificationBuilder.String(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> Verbatim(string value)
        {
            return ClassificationBuilder.Verbatim(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> Keyword(string value)
        {
            return ClassificationBuilder.Keyword(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> WhiteSpace(string value)
        {
            return ClassificationBuilder.WhiteSpace(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> Text(string value)
        {
            return ClassificationBuilder.Text(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> NumericLiteral(string value)
        {
            return ClassificationBuilder.NumericLiteral(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> PPKeyword(string value)
        {
            return ClassificationBuilder.PPKeyword(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> PPText(string value)
        {
            return ClassificationBuilder.PPText(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> Identifier(string value)
        {
            return ClassificationBuilder.Identifier(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> Inactive(string value)
        {
            return ClassificationBuilder.Inactive(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> Comment(string value)
        {
            return ClassificationBuilder.Comment(value);
        }

        [DebuggerStepThrough]
        protected Tuple<string, string> Number(string value)
        {
            return ClassificationBuilder.Number(value);
        }

        protected ClassificationBuilder.PunctuationClassificationTypes Punctuation
        {
            get { return ClassificationBuilder.Punctuation; }
        }

        protected ClassificationBuilder.OperatorClassificationTypes Operators
        {
            get { return ClassificationBuilder.Operator; }
        }

        protected ClassificationBuilder.XmlDocClassificationTypes XmlDoc
        {
            get { return ClassificationBuilder.XmlDoc; }
        }

        protected string Lines(params string[] lines)
        {
            return string.Join("\r\n", lines);
        }

        protected Tuple<string, string>[] ExpectedClassifications(
            params Tuple<string, string>[] expectedClassifications)
        {
            return expectedClassifications;
        }

        protected Tuple<string, string>[] NoClassifications()
        {
            return null;
        }

        protected void WaitForDocumentationComment(object content)
        {
/*
            if (content is QuickInfoDisplayDeferredContent)
            {
                var docCommentDeferredContent = ((QuickInfoDisplayDeferredContent)content).Documentation as DocumentationCommentDeferredContent;
                if (docCommentDeferredContent != null)
                {
                    docCommentDeferredContent.WaitForDocumentationCommentTask_ForTestingPurposesOnly();
                }
            }
*/
        }

        internal Action<QuickInfoItem> SymbolGlyph(Glyph expectedGlyph)
        {
            return (qi) =>
            {
                Assert.True(qi.Tags.GetGlyphs().Contains(expectedGlyph));
            };
        }

        internal Action<QuickInfoItem> WarningGlyph(Glyph expectedGlyph)
        {
            return SymbolGlyph(expectedGlyph);
        }

        internal void AssertTaggedText(
            string expectedText,
            ImmutableArray<TaggedText> taggedText,
            Tuple<string, string>[] expectedClassifications = null)
        {
            var actualText = string.Concat(taggedText.Select(tt => tt.Text));
            Assert.Equal(expectedText, actualText);
        }

        internal Action<QuickInfoItem> MainDescription(
            string expectedText,
            Tuple<string, string>[] expectedClassifications = null)
        {
            return item => AssertTaggedText(expectedText, item.Description, expectedClassifications);
        }

        internal Action<QuickInfoItem> Documentation(
            string expectedText,
            Tuple<string, string>[] expectedClassifications = null)
        {
            return item => AssertTaggedText(expectedText, item.DocumentationComments, expectedClassifications);
        }

        internal Action<QuickInfoItem> TypeParameterMap(
            string expectedText,
            Tuple<string, string>[] expectedClassifications = null)
        {
            return item => AssertTaggedText(expectedText, item.TypeParameters, expectedClassifications);
        }

        internal Action<QuickInfoItem> AnonymousTypes(
            string expectedText,
            Tuple<string, string>[] expectedClassifications = null)
        {
            return item => AssertTaggedText(expectedText, item.AnonymousTypes, expectedClassifications);
        }

        internal Action<QuickInfoItem> NoTypeParameterMap
        {
            get
            {
                return item => AssertTaggedText(string.Empty, item.TypeParameters);
            }
        }

        internal Action<QuickInfoItem> Usage(string expectedText, bool expectsWarningGlyph = false)
        {
            return item => 
            {
                AssertTaggedText(expectedText, item.Usage);
                if (expectsWarningGlyph)
                {
                    WarningGlyph(Glyph.CompletionWarning)(item);
                }
                else
                {
                    Assert.False(item.Tags.GetGlyphs().Contains(Glyph.CompletionWarning));
                }
            };
        }

        internal Action<QuickInfoItem> Exceptions(string expectedText)
        {
            return item => AssertTaggedText(expectedText, item.Exception);
        }

        protected static async Task<bool> CanUseSpeculativeSemanticModelAsync(Document document, int position)
        {
            var service = document.Project.LanguageServices.GetService<ISyntaxFactsService>();
            var node = (await document.GetSyntaxRootAsync()).FindToken(position).Parent;

            return !service.GetMemberBodySpanForSpeculativeBinding(node).IsEmpty;
        }

        internal abstract Task TestAsync(string markup, params Action<QuickInfoItem>[] expectedResults);
    }
}