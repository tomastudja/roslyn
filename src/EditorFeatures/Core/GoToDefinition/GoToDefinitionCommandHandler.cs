﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.BackgroundWorkIndicator;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Notification;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.GoToDefinition
{
    [Export(typeof(ICommandHandler))]
    [ContentType(ContentTypeNames.RoslynContentType)]
    [Name(PredefinedCommandHandlerNames.GoToDefinition)]
    internal class GoToDefinitionCommandHandler :
        ICommandHandler<GoToDefinitionCommandArgs>
    {
        private readonly IThreadingContext _threadingContext;
        private readonly IUIThreadOperationExecutor _executor;
        private readonly IAsynchronousOperationListener _listener;

        [ImportingConstructor]
        [SuppressMessage("RoslynDiagnosticsReliability", "RS0033:Importing constructor should be [Obsolete]", Justification = "Used in test code: https://github.com/dotnet/roslyn/issues/42814")]
        public GoToDefinitionCommandHandler(
            IThreadingContext threadingContext,
            IUIThreadOperationExecutor executor,
            IAsynchronousOperationListenerProvider listenerProvider)
        {
            _threadingContext = threadingContext;
            _executor = executor;
            _listener = listenerProvider.GetListener(FeatureAttribute.GoToBase);
        }

        public string DisplayName => EditorFeaturesResources.Go_to_Definition;

        private static (Document?, IGoToDefinitionService?) GetDocumentAndService(ITextSnapshot snapshot)
        {
            var document = snapshot.GetOpenDocumentInCurrentContextWithChanges();
            return (document, document?.GetLanguageService<IGoToDefinitionService>());
        }

        public CommandState GetCommandState(GoToDefinitionCommandArgs args)
        {
            var (_, service) = GetDocumentAndService(args.SubjectBuffer.CurrentSnapshot);
            return service != null
                ? CommandState.Available
                : CommandState.Unspecified;
        }

        public bool ExecuteCommand(GoToDefinitionCommandArgs args, CommandExecutionContext context)
        {
            var subjectBuffer = args.SubjectBuffer;
            var (document, service) = GetDocumentAndService(subjectBuffer.CurrentSnapshot);

            // In Live Share, typescript exports a gotodefinition service that returns no results and prevents the LSP client
            // from handling the request.  So prevent the local service from handling goto def commands in the remote workspace.
            // This can be removed once typescript implements LSP support for goto def.
            if (service == null || subjectBuffer.IsInLspEditorContext())
                return false;

            Contract.ThrowIfNull(document);
            var caretPos = args.TextView.GetCaretPoint(subjectBuffer);
            if (!caretPos.HasValue)
                return false;

            if (service is IAsyncGoToDefinitionService asyncService)
            {
                // We're showing our own UI, ensure the editor doesn't show anything itself.
                context.OperationContext.TakeOwnership();
                var token = _listener.BeginAsyncOperation(nameof(ExecuteCommand));
                ExecuteModernCommandAsync(args, document, asyncService, caretPos.Value).CompletesAsyncOperation(token);
            }
            else
            {
                bool succeeded;
                using (context.OperationContext.AddScope(allowCancellation: true, EditorFeaturesResources.Navigating_to_definition))
                {
                    succeeded = service.TryGoToDefinition(document, caretPos.Value, context.OperationContext.UserCancellationToken);
                }

                if (!succeeded)
                {
                    // Dismiss any context dialog that is up before showing our own notification.
                    context.OperationContext.TakeOwnership();
                    ReportFailure(document);
                }
            }

            return true;
        }

        private static void ReportFailure(Document document)
        {
            var notificationService = document.Project.Solution.Workspace.Services.GetRequiredService<INotificationService>();
            notificationService.SendNotification(
                FeaturesResources.Cannot_navigate_to_the_symbol_under_the_caret, EditorFeaturesResources.Go_to_Definition, NotificationSeverity.Information);
        }

        private async Task ExecuteModernCommandAsync(
            GoToDefinitionCommandArgs args, Document document, IAsyncGoToDefinitionService service, SnapshotPoint position)
        {
            bool succeeded;

            var indicatorFactory = document.Project.Solution.Workspace.Services.GetRequiredService<IBackgroundWorkIndicatorFactory>();
            using (var backgroundIndicator = indicatorFactory.Create(
                args.TextView, new SnapshotSpan(args.SubjectBuffer.CurrentSnapshot, position, 1),
                EditorFeaturesResources.Navigating_to_definition))
            {
                await Task.Delay(5000).ConfigureAwait(false);

                var cancellationToken = backgroundIndicator.UserCancellationToken;

                // determine the location first.
                var location = await service.FindDefinitionLocationAsync(document, position, cancellationToken).ConfigureAwait(false);

                // we're about to navigate.  so disable cancellation on focus-lost in our indicator so we don't end up
                // causing ourselves to self-cancel.
                backgroundIndicator.CancelOnFocusLost = false;
                succeeded = location != null && await location.NavigateToAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!succeeded)
            {
                await _threadingContext.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
                ReportFailure(document);
            }
        }
    }
}
