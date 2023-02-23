﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using Roslyn.Utilities;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.LanguageServer.Handler.Configuration
{
    [Method(Methods.WorkspaceDidChangeConfigurationName)]
    internal partial class DidChangeConfigurationNotificationHandler : ILspServiceNotificationHandler<LSP.DidChangeConfigurationParams>, IOnInitialized
    {
        private readonly ILspLogger _lspLogger;
        private readonly IGlobalOptionService _globalOptionService;
        private readonly IClientLanguageServerManager _clientLanguageServerManager;
        private readonly Guid _registrationId;

        public DidChangeConfigurationNotificationHandler(ILspLogger logger, IGlobalOptionService globalOptionService, IClientLanguageServerManager clientLanguageServerManager)
        {
            _lspLogger = logger;
            _globalOptionService = globalOptionService;
            _clientLanguageServerManager = clientLanguageServerManager;
            _registrationId = Guid.NewGuid();
        }

        public bool MutatesSolutionState => true;

        public bool RequiresLSPSolution => false;

        public Task HandleNotificationAsync(DidChangeConfigurationParams request, RequestContext requestContext, CancellationToken cancellationToken)
            => RefreshOptionsAsync(cancellationToken);

        private async Task RefreshOptionsAsync(CancellationToken cancellationToken)
        {
            var configurationItems = s_supportedOptions.SelectAsArray(
                option => new ConfigurationItem() { ScopeUri = null, Section = GenerateSection(option) });
            var configurationsFromClient = await GetConfigurationsAsync(configurationItems, cancellationToken).ConfigureAwait(false);

            for (var i = 0; i < configurationsFromClient.Length; i++)
            {
                var option = s_supportedOptions[i];
                var configurationValue = configurationsFromClient[i];
                if (option.Definition.Serializer.TryParse(configurationValue, out var result))
                {
                    if (option is IPerLanguageValuedOption perLanguageValuedOption)
                    {
                        foreach (var language in s_supportedLanguages)
                        {
                            _globalOptionService.SetGlobalOption(new OptionKey2(perLanguageValuedOption, language), result);
                        }
                    }
                    else
                    {
                        _globalOptionService.SetGlobalOption(new OptionKey2(option, language: null), result);
                    }
                }
                else
                {
                    _lspLogger.LogError($"Failed to parse client value: {configurationsFromClient} to type: {option.Definition.Type}.");
                }
            }
        }

        private async Task<ImmutableArray<string>> GetConfigurationsAsync(ImmutableArray<ConfigurationItem> configurationItems, CancellationToken cancellationToken)
        {
            try
            {
                var configurationParams = new ConfigurationParams() { Items = configurationItems.AsArray() };
                var options = await _clientLanguageServerManager.SendRequestAsync<ConfigurationParams, JArray>(
                    Methods.WorkspaceConfigurationName, configurationParams, cancellationToken).ConfigureAwait(false);

                // Failed to get result from client.
                Contract.ThrowIfNull(options);

                // This is ensured by LSP.
                Contract.ThrowIfTrue(options.Count != configurationItems.Length);
                return options.SelectAsArray(token => token.ToString());
            }
            catch (Exception e)
            {
                _lspLogger.LogException(e, $"Exception occurs when make {Methods.WorkspaceConfigurationName}.");
            }

            return ImmutableArray<string>.Empty;
        }

        private static string GenerateSection(IOption2 option)
            // TODO: Description is localized, we should introduce a non-loc description
            => string.Concat(option.Definition.Group.Description, '.', option.Definition.ConfigName);
    }
}
