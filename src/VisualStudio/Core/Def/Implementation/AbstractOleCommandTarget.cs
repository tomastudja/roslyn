﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation
{
    internal abstract partial class AbstractOleCommandTarget : IOleCommandTarget
    {
        private readonly IComponentModel _componentModel;

        /// <summary>
        /// This is set only during Exec. Currently, this is required to disambiguate the editor calls to
        /// <see cref="IVsTextViewFilter.GetPairExtents(int, int, TextSpan[])"/> between GotoBrace and GotoBraceExt commands.
        /// </summary>
        protected uint CurrentlyExecutingCommand { get; private set; }

        public AbstractOleCommandTarget(
            IWpfTextView wpfTextView,
            IComponentModel componentModel)
        {
            Contract.ThrowIfNull(wpfTextView);
            Contract.ThrowIfNull(componentModel);

            WpfTextView = wpfTextView;
            _componentModel = componentModel;
        }

        public IVsEditorAdaptersFactoryService EditorAdaptersFactory
        {
            get { return _componentModel.GetService<IVsEditorAdaptersFactoryService>(); }
        }

        /// <summary>
        /// The IWpfTextView that this command filter is attached to.
        /// </summary>
        public IWpfTextView WpfTextView { get; }

        /// <summary>
        /// The next command target in the chain. This is set by the derived implementation of this
        /// class.
        /// </summary>
        [DisallowNull]
        protected internal IOleCommandTarget? NextCommandTarget { get; set; }

        internal AbstractOleCommandTarget AttachToVsTextView()
        {
            var vsTextView = EditorAdaptersFactory.GetViewAdapter(WpfTextView);

            // Add command filter to IVsTextView. If something goes wrong, throw.
            var returnValue = vsTextView.AddCommandFilter(this, out var nextCommandTarget);
            Marshal.ThrowExceptionForHR(returnValue);
            Contract.ThrowIfNull(nextCommandTarget);

            NextCommandTarget = nextCommandTarget;

            return this;
        }

        protected virtual ITextBuffer GetSubjectBufferContainingCaret()
        {
            return WpfTextView.GetBufferContainingCaret();
        }

        protected virtual ITextView ConvertTextView()
        {
            return WpfTextView;
        }
    }
}
