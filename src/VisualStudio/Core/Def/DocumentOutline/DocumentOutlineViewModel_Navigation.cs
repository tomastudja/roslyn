﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Collections;
using Microsoft.CodeAnalysis.Navigation;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.DocumentOutline
{
    internal partial class DocumentOutlineViewModel
    {
        /// <summary>
        /// A Queue of text spans to navigate to.
        /// </summary>
        private readonly AsyncBatchingWorkQueue<TextSpan> _navigationQueue;

        internal void EnqueueNavigation(TextSpan textSpan)
            => _navigationQueue.AddWork(textSpan, cancelExistingWork: true);

        /// <summary>
        /// Event handler that is called once we have finished navigating to a span.
        /// </summary>
        public event EventHandler? NavigationCompleted;

        private async ValueTask NavigateToTextSpanAsync(ImmutableSegmentedList<TextSpan> textSpans, CancellationToken token)
        {
            var textSpan = textSpans.Last();
            var textView = await _visualStudioCodeWindowInfoService.GetLastActiveIWpfTextViewAsync(token).ConfigureAwait(false);
            var snapShot = textView.TextSnapshot;
            var document = snapShot.GetOpenDocumentInCurrentContextWithChanges();
            if (document is null)
            {
                return;
            }

            var location = await _navigationService.GetLocationForSpanAsync(_workspace, document.Id, textSpan, token).ConfigureAwait(false);
            if (location is null)
            {
                return;
            }

            await location.NavigateToAsync(NavigationOptions.Default, token).ConfigureAwait(false);
            NavigationCompleted?.BeginInvoke(this, new EventArgs(), static (result) => { }, null);
        }
    }
}
