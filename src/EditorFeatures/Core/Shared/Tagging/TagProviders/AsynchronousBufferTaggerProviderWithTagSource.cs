// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Editor.Tagging;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Microsoft.CodeAnalysis.Editor.Shared.Tagging
{
    internal sealed class AsynchronousBufferTaggerProviderWithTagSource<TTag> :
        AbstractAsynchronousTaggerProvider<TTag>,
        ITaggerProvider
        where TTag : ITag
    {
        private readonly IAsynchronousTaggerDataSource<TTag> _dataSource;

        public AsynchronousBufferTaggerProviderWithTagSource(
            IAsynchronousTaggerDataSource<TTag> dataSource,
            IAsynchronousOperationListener asyncListener,
            IForegroundNotificationService notificationService)
            : base(asyncListener, notificationService)
        {
            this._dataSource = dataSource;
        }

        public ITagger<T> CreateTagger<T>(ITextBuffer subjectBuffer) where T : ITag
        {
            if (subjectBuffer == null)
            {
                throw new ArgumentNullException(nameof(subjectBuffer));
            }

            return this.GetOrCreateTagger<T>(null, subjectBuffer);
        }

        protected override TagSource<TTag> CreateTagSourceCore(ITextView textViewOpt, ITextBuffer subjectBuffer)
        {
            return new TagSource<TTag>(textViewOpt, subjectBuffer, _dataSource, AsyncListener, NotificationService);
        }

        protected sealed override bool TryRetrieveTagSource(ITextView textViewOpt, ITextBuffer subjectBuffer, out TagSource<TTag> tagSource)
        {
            return subjectBuffer.Properties.TryGetProperty(UniqueKey, out tagSource);
        }

        protected sealed override void StoreTagSource(ITextView textViewOpt, ITextBuffer subjectBuffer, TagSource<TTag> tagSource)
        {
            subjectBuffer.Properties.AddProperty(UniqueKey, tagSource);
        }

        protected sealed override void RemoveTagSource(ITextView textViewOpt, ITextBuffer subjectBuffer)
        {
            subjectBuffer.Properties.RemoveProperty(UniqueKey);
        }
    }
}