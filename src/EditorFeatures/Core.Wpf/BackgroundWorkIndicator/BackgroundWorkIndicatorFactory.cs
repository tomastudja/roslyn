﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.BackgroundWorkIndicator
{
    [Export(typeof(IBackgroundWorkIndicatorFactory))]
    [Export(typeof(IKeyProcessorProvider))]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    [ContentType(ContentTypeNames.RoslynContentType)]
    [Name(nameof(WpfBackgroundWorkIndicatorFactory))]
    internal partial class WpfBackgroundWorkIndicatorFactory : IBackgroundWorkIndicatorFactory, IKeyProcessorProvider
    {
        private readonly IThreadingContext _threadingContext;
        private readonly IToolTipPresenterFactory _toolTipPresenterFactory;
        private readonly IAsynchronousOperationListener _listener;

        private BackgroundWorkIndicatorContext? _currentContext;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public WpfBackgroundWorkIndicatorFactory(
            IThreadingContext threadingContext,
            IToolTipPresenterFactory toolTipPresenterFactory,
            IAsynchronousOperationListenerProvider listenerProvider)
        {
            _threadingContext = threadingContext;
            _toolTipPresenterFactory = toolTipPresenterFactory;
            _listener = listenerProvider.GetListener(FeatureAttribute.QuickInfo);
        }

        IUIThreadOperationContext IBackgroundWorkIndicatorFactory.Create(
            ITextView textView,
            SnapshotSpan applicableToSpan,
            string description,
            bool cancelOnEdit,
            bool cancelOnFocusLost)
        {
            Contract.ThrowIfFalse(_threadingContext.HasMainThread);

            // If we have an outstanding context in flight, cancel it and create a new one to show the user.
            _currentContext?.CancelAndDispose();

            // Create the indicator in its default/empty state.
            _currentContext = new BackgroundWorkIndicatorContext(
                this, textView, applicableToSpan, description,
                cancelOnEdit, cancelOnFocusLost);

            // Then add a single scope representing the how the UI should look initially.
            _currentContext.AddScope(allowCancellation: true, description);
            return _currentContext;
        }

        public KeyProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            return wpfTextView.Properties.GetOrCreateSingletonProperty(
                () => new BackgroundWorkIndicatorKeyProcessor(_threadingContext));
        }

        private class BackgroundWorkIndicatorKeyProcessor : KeyProcessor
        {
            private readonly IThreadingContext _threadingContext;
            private readonly HashSet<BackgroundWorkIndicatorContext> _registeredContexts = new();

            public BackgroundWorkIndicatorKeyProcessor(IThreadingContext threadingContext)
            {
                _threadingContext = threadingContext;
            }

            public void RegisterContext(BackgroundWorkIndicatorContext context)
            {
                Contract.ThrowIfFalse(_threadingContext.HasMainThread);
                _registeredContexts.Add(context);

            }
        }
    }
}
