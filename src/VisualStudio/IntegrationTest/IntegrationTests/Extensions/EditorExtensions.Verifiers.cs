﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.IntegrationTest.Utilities;
using Microsoft.VisualStudio.IntegrationTest.Utilities.Common;
using Xunit;

namespace Roslyn.VisualStudio.IntegrationTests.Extensions.Editor
{
    public static partial class EditorExtensions
    {
        public static void VerifyCurrentTokenType(this AbstractIntegrationTest test, string tokenType)
        {
            CommonExtensions.VerifyCurrentTokenType(test, tokenType, test.VisualStudio.Instance.Editor);
        }

        public static void VerifyCompletionItemExists(this AbstractIntegrationTest test, params string[] expectedItems)
        {
            CommonExtensions.VerifyCompletionItemExists(test.VisualStudio.Instance.Editor, expectedItems);
        }

        public static void VerifyCaretPosition(this AbstractIntegrationTest test, int expectedCaretPosition)
        {
            CommonExtensions.VerifyCaretPosition(test.VisualStudio.Instance.Editor, expectedCaretPosition);
        }

        public static void VerifyCurrentLineText(this AbstractIntegrationTest test, string expectedText, bool assertCaretPosition = false, bool trimWhitespace = true)
        {
            if (assertCaretPosition)
            {
                VerifyCurrentLineTextAndAssertCaretPosition(test, expectedText, trimWhitespace);
            }
            else
            {
                var lineText = test.VisualStudio.Instance.Editor.GetCurrentLineText();

                if (trimWhitespace)
                {
                    lineText = lineText.Trim();
                }

                Assert.Equal(expectedText, lineText);
            }
        }

        private static void VerifyCurrentLineTextAndAssertCaretPosition(AbstractIntegrationTest test, string expectedText, bool trimWhitespace)
        {
            var caretStartIndex = expectedText.IndexOf("$$");
            if (caretStartIndex < 0)
            {
                throw new ArgumentException("Expected caret position to be specified with $$", nameof(expectedText));
            }

            var caretEndIndex = caretStartIndex + "$$".Length;

            var expectedTextBeforeCaret = expectedText.Substring(0, caretStartIndex);
            var expectedTextAfterCaret = expectedText.Substring(caretEndIndex);

            var lineText = test.VisualStudio.Instance.Editor.GetCurrentLineText();

            if (trimWhitespace)
            {
                if (caretStartIndex == 0)
                {
                    lineText = lineText.TrimEnd();
                }
                else if (caretEndIndex == expectedText.Length)
                {
                    lineText = lineText.TrimStart();
                }
                else
                {
                    lineText = lineText.Trim();
                }
            }

            var lineTextBeforeCaret = caretStartIndex < lineText.Length
                ? lineText.Substring(0, caretStartIndex)
                : lineText;

            var lineTextAfterCaret = caretStartIndex < lineText.Length
                ? lineText.Substring(caretStartIndex)
                : string.Empty;

            Assert.Equal(expectedTextBeforeCaret, lineTextBeforeCaret);
            Assert.Equal(expectedTextAfterCaret, lineTextAfterCaret);
            Assert.Equal(expectedTextBeforeCaret.Length + expectedTextAfterCaret.Length, lineText.Length);
        }

        public static void VerifyTextContains(this AbstractIntegrationTest test, string expectedText, bool assertCaretPosition = false)
        {
            if (assertCaretPosition)
            {
                VerifyTextContainsAndAssertCaretPosition(test, expectedText);
            }
            else
            {
                var editorText = test.VisualStudio.Instance.Editor.GetText();
                Assert.Contains(expectedText, editorText);
            }
        }

        private static void VerifyTextContainsAndAssertCaretPosition(AbstractIntegrationTest test, string expectedText)
        {
            var caretStartIndex = expectedText.IndexOf("$$");
            if (caretStartIndex < 0)
            {
                throw new ArgumentException("Expected caret position to be specified with $$", nameof(expectedText));
            }

            var caretEndIndex = caretStartIndex + "$$".Length;

            var expectedTextBeforeCaret = expectedText.Substring(0, caretStartIndex);
            var expectedTextAfterCaret = expectedText.Substring(caretEndIndex);

            var expectedTextWithoutCaret = expectedTextBeforeCaret + expectedTextAfterCaret;

            var editorText = test.VisualStudio.Instance.Editor.GetText();
            Assert.Contains(expectedTextWithoutCaret, editorText);

            var index = editorText.IndexOf(expectedTextWithoutCaret);

            var caretPosition = test.VisualStudio.Instance.Editor.GetCaretPosition();
            Assert.Equal(caretStartIndex + index, caretPosition);
        }

        public static void VerifyCompletionItemDoesNotExist(this AbstractIntegrationTest test, params string[] expectedItems)
        {
            var completionItems = test.VisualStudio.Instance.Editor.GetCompletionItems();
            foreach (var expectedItem in expectedItems)
            {
                Assert.DoesNotContain(expectedItem, completionItems);
            }
        }

        public static void VerifyCurrentCompletionItem(this AbstractIntegrationTest test, string expectedItem)
        {
            var currentItem = test.VisualStudio.Instance.Editor.GetCurrentCompletionItem();
            Assert.Equal(expectedItem, currentItem);
        }

        public static void VerifyCurrentSignature(this AbstractIntegrationTest test, Signature expectedSignature)
        {
            var currentSignature = test.VisualStudio.Instance.Editor.GetCurrentSignature();
            Assert.Equal(expectedSignature, currentSignature);
        }

        public static void VerifyCurrentSignature(this AbstractIntegrationTest test, string content)
        {
            var currentSignature = test.VisualStudio.Instance.Editor.GetCurrentSignature();
            Assert.Equal(content, currentSignature.Content);
        }

        public static void VerifyCodeAction(
            this AbstractIntegrationTest test,
            string expectedItem,
            bool applyFix = false,
            bool verifyNotShowing = false,
            bool ensureExpectedItemsAreOrdered = false,
            FixAllScope? fixAllScope = null,
            bool blockUntilComplete = true)
        {
            var expectedItems = new[] { expectedItem };
            test.VerifyCodeActions(
                expectedItems, applyFix ? expectedItem : null, verifyNotShowing,
                ensureExpectedItemsAreOrdered, fixAllScope, blockUntilComplete);
        }

        public static void VerifyCodeActions(
            this AbstractIntegrationTest test,
            IEnumerable<string> expectedItems,
            string applyFix = null,
            bool verifyNotShowing = false,
            bool ensureExpectedItemsAreOrdered = false,
            FixAllScope? fixAllScope = null,
            bool blockUntilComplete = true)
        {
            test.VisualStudio.Instance.Editor.ShowLightBulb();
            test.VisualStudio.Instance.Editor.WaitForLightBulbSession();

            if (verifyNotShowing)
            {
                test.VerifyCodeActionsNotShowing();
                return;
            }

            var actions = test.VisualStudio.Instance.Editor.GetLightBulbActions();

            if (expectedItems != null && expectedItems.Any())
            {
                if (ensureExpectedItemsAreOrdered)
                {
                    TestUtilities.ThrowIfExpectedItemNotFoundInOrder(
                        actions,
                        expectedItems);
                }
                else
                {
                    TestUtilities.ThrowIfExpectedItemNotFound(
                        actions,
                        expectedItems);
                }
            }

            if (!string.IsNullOrEmpty(applyFix) || fixAllScope.HasValue)
            {
                test.VisualStudio.Instance.Editor.ApplyLightBulbAction(applyFix, fixAllScope, blockUntilComplete);

                if (blockUntilComplete)
                {
                    // wait for action to complete
                    test.WaitForAsyncOperations(FeatureAttribute.LightBulb);
                }
            }
        }

        public static void VerifyCodeActionsNotShowing(this AbstractIntegrationTest test)
        {
            if (test.VisualStudio.Instance.Editor.IsLightBulbSessionExpanded())
            {
                throw new InvalidOperationException("Expected no light bulb session, but one was found.");
            }
        }

        public static void VerifyCurrentParameter(this AbstractIntegrationTest test, string name, string documentation)
        {
            var currentParameter = test.VisualStudio.Instance.Editor.GetCurrentSignature().CurrentParameter;
            Assert.Equal(name, currentParameter.Name);
            Assert.Equal(documentation, currentParameter.Documentation);
        }

        public static void VerifyParameters(this AbstractIntegrationTest test, params (string name, string documentation)[] parameters)
        {
            var currentParameters = test.VisualStudio.Instance.Editor.GetCurrentSignature().Parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                var (expectedName, expectedDocumentation) = parameters[i];
                Assert.Equal(expectedName, currentParameters[i].Name);
                Assert.Equal(expectedDocumentation, currentParameters[i].Documentation);
            }
        }

        public static void VerifyDialog(this AbstractIntegrationTest test, string dialogName, bool isOpen)
        {
            test.VisualStudio.Instance.Editor.VerifyDialog(dialogName, isOpen);
        }
    }
}
