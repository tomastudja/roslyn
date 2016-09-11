﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Roslyn.VisualStudio.Test.Utilities;
using Roslyn.VisualStudio.Test.Utilities.OutOfProcess;
using Xunit;

namespace Roslyn.VisualStudio.IntegrationTests
{
    public abstract class AbstractInteractiveWindowTest : AbstractIntegrationTest
    {
        private const string Edit_SelectionCancelCommand = "Edit.SelectionCancel";

        private static readonly char[] LineSeparators = { '\r', '\n' };

        protected readonly CSharpInteractiveWindow_OutOfProc _interactiveWindow;

        protected AbstractInteractiveWindowTest(VisualStudioInstanceFactory instanceFactory)
            : base(instanceFactory)
        {
            _interactiveWindow = _visualStudio.Instance.CSharpInteractiveWindow;
            ClearInteractiveWindow();
        }

        protected void ClearInteractiveWindow()
        {
            _interactiveWindow.Initialize();
            _interactiveWindow.ShowWindow();
            _interactiveWindow.Reset();
        }

        protected void ClearReplText()
        {
            // Dismiss the pop-up (if any)
            _visualStudio.Instance.ExecuteCommand(Edit_SelectionCancelCommand);

            // Clear the line
            _visualStudio.Instance.ExecuteCommand(Edit_SelectionCancelCommand);
        }

        protected void Reset(bool waitForPrompt = true)
        {
            _interactiveWindow.Reset(waitForPrompt: true);
        }

        protected void SubmitText(string text, bool waitForPrompt = true)
        {
            _interactiveWindow.SubmitText(text, waitForPrompt);
        }

        protected void VerifyLastReplOutput(string expectedReplOutput)
        {
            var lastReplOutput = _interactiveWindow.GetLastReplOutput();
            Assert.Equal(expectedReplOutput, lastReplOutput);
        }

        protected void VerifyLastReplOutputContains(string expectedReplOutput)
        {
            var lastReplOutput = _interactiveWindow.GetLastReplOutput();
            Assert.Contains(expectedReplOutput, lastReplOutput);
        }

        protected void VerifyLastReplOutputEndsWith(string expectedReplOutput)
        {
            var lastReplOutput = _interactiveWindow.GetLastReplOutput();
            Assert.EndsWith(expectedReplOutput, lastReplOutput);
        }

        protected void VerifyReplPromptConsistency(string prompt, string output)
        {
            var replText = _interactiveWindow.GetReplText();
            var replTextLines = replText.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries);

            foreach (var replTextLine in replTextLines)
            {
                if (!replTextLine.Contains(prompt))
                {
                    continue;
                }

                // The prompt must be at the beginning of the line
                Assert.StartsWith(prompt, replTextLine);

                var promptIndex = replTextLine.IndexOf(prompt, prompt.Length);

                // A 'subsequent' prompt is only allowed on a line containing #prompt
                if (promptIndex >= 0)
                {
                    Assert.StartsWith(prompt + "#prompt", replTextLine);
                    Assert.False(replTextLine.IndexOf(prompt, promptIndex + prompt.Length) >= 0);
                }

                // There must be no output on a prompt line.
                Assert.DoesNotContain(output, replTextLine);
            }
        }

        protected void WaitForReplOutput(string outputText)
        {
            _interactiveWindow.WaitForReplOutput(outputText);
        }
    }
}
