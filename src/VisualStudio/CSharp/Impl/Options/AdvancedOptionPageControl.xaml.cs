﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Media;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Editor.CSharp.SplitStringLiteral;
using Microsoft.CodeAnalysis.Editor.Options;
using Microsoft.CodeAnalysis.Editor.Shared.Options;
using Microsoft.CodeAnalysis.EmbeddedLanguages.RegularExpressions;
using Microsoft.CodeAnalysis.ExtractMethod;
using Microsoft.CodeAnalysis.Fading;
using Microsoft.CodeAnalysis.ImplementType;
using Microsoft.CodeAnalysis.Options.EditorConfig;
using Microsoft.CodeAnalysis.SolutionCrawler;
using Microsoft.CodeAnalysis.Structure;
using Microsoft.CodeAnalysis.SymbolSearch;
using Microsoft.CodeAnalysis.ValidateFormatString;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices.ColorSchemes;
using Microsoft.VisualStudio.LanguageServices.Implementation.Options;

namespace Microsoft.VisualStudio.LanguageServices.CSharp.Options
{
    internal partial class AdvancedOptionPageControl : AbstractOptionPageControl
    {
        private ColorSchemeApplier _colorSchemeApplier;

        public AdvancedOptionPageControl(OptionStore optionStore, IComponentModel componentModel) : base(optionStore)
        {
            _colorSchemeApplier = componentModel.GetService<ColorSchemeApplier>();

            InitializeComponent();

            BindToOption(Background_analysis_scope_active_file, SolutionCrawlerOptions.BackgroundAnalysisScopeOption, BackgroundAnalysisScope.ActiveFile, LanguageNames.CSharp);
            BindToOption(Background_analysis_scope_open_files, SolutionCrawlerOptions.BackgroundAnalysisScopeOption, BackgroundAnalysisScope.OpenFilesAndProjects, LanguageNames.CSharp);
            BindToOption(Background_analysis_scope_full_solution, SolutionCrawlerOptions.BackgroundAnalysisScopeOption, BackgroundAnalysisScope.FullSolution, LanguageNames.CSharp);
            BindToOption(Enable_navigation_to_decompiled_sources, FeatureOnOffOptions.NavigateToDecompiledSources);
            BindToOption(Use_editorconfig_compatibility_mode, EditorConfigDocumentOptionsProviderFactory.UseLegacyEditorConfigSupport);

            BindToOption(PlaceSystemNamespaceFirst, GenerationOptions.PlaceSystemNamespaceFirst, LanguageNames.CSharp);
            BindToOption(SeparateImportGroups, GenerationOptions.SeparateImportDirectiveGroups, LanguageNames.CSharp);
            BindToOption(SuggestForTypesInReferenceAssemblies, SymbolSearchOptions.SuggestForTypesInReferenceAssemblies, LanguageNames.CSharp);
            BindToOption(SuggestForTypesInNuGetPackages, SymbolSearchOptions.SuggestForTypesInNuGetPackages, LanguageNames.CSharp);
            BindToOption(Split_string_literals_on_enter, SplitStringLiteralOptions.Enabled, LanguageNames.CSharp);

            BindToOption(EnterOutliningMode, FeatureOnOffOptions.Outlining, LanguageNames.CSharp);
            BindToOption(Show_outlining_for_declaration_level_constructs, BlockStructureOptions.ShowOutliningForDeclarationLevelConstructs, LanguageNames.CSharp);
            BindToOption(Show_outlining_for_code_level_constructs, BlockStructureOptions.ShowOutliningForCodeLevelConstructs, LanguageNames.CSharp);
            BindToOption(Show_outlining_for_comments_and_preprocessor_regions, BlockStructureOptions.ShowOutliningForCommentsAndPreprocessorRegions, LanguageNames.CSharp);
            BindToOption(Collapse_regions_when_collapsing_to_definitions, BlockStructureOptions.CollapseRegionsWhenCollapsingToDefinitions, LanguageNames.CSharp);

            BindToOption(Fade_out_unused_usings, FadingOptions.FadeOutUnusedImports, LanguageNames.CSharp);
            BindToOption(Fade_out_unreachable_code, FadingOptions.FadeOutUnreachableCode, LanguageNames.CSharp);

            BindToOption(Show_guides_for_declaration_level_constructs, BlockStructureOptions.ShowBlockStructureGuidesForDeclarationLevelConstructs, LanguageNames.CSharp);
            BindToOption(Show_guides_for_code_level_constructs, BlockStructureOptions.ShowBlockStructureGuidesForCodeLevelConstructs, LanguageNames.CSharp);

            BindToOption(GenerateXmlDocCommentsForTripleSlash, FeatureOnOffOptions.AutoXmlDocCommentGeneration, LanguageNames.CSharp);
            BindToOption(InsertAsteriskAtTheStartOfNewLinesWhenWritingBlockComments, FeatureOnOffOptions.AutoInsertBlockCommentStartString, LanguageNames.CSharp);
            BindToOption(DisplayLineSeparators, FeatureOnOffOptions.LineSeparator, LanguageNames.CSharp);
            BindToOption(EnableHighlightReferences, FeatureOnOffOptions.ReferenceHighlighting, LanguageNames.CSharp);
            BindToOption(EnableHighlightKeywords, FeatureOnOffOptions.KeywordHighlighting, LanguageNames.CSharp);
            BindToOption(RenameTrackingPreview, FeatureOnOffOptions.RenameTrackingPreview, LanguageNames.CSharp);

            BindToOption(DontPutOutOrRefOnStruct, ExtractMethodOptions.DontPutOutOrRefOnStruct, LanguageNames.CSharp);
            BindToOption(AllowMovingDeclaration, ExtractMethodOptions.AllowMovingDeclaration, LanguageNames.CSharp);

            BindToOption(with_other_members_of_the_same_kind, ImplementTypeOptions.InsertionBehavior, ImplementTypeInsertionBehavior.WithOtherMembersOfTheSameKind, LanguageNames.CSharp);
            BindToOption(at_the_end, ImplementTypeOptions.InsertionBehavior, ImplementTypeInsertionBehavior.AtTheEnd, LanguageNames.CSharp);

            BindToOption(prefer_throwing_properties, ImplementTypeOptions.PropertyGenerationBehavior, ImplementTypePropertyGenerationBehavior.PreferThrowingProperties, LanguageNames.CSharp);
            BindToOption(prefer_auto_properties, ImplementTypeOptions.PropertyGenerationBehavior, ImplementTypePropertyGenerationBehavior.PreferAutoProperties, LanguageNames.CSharp);

            BindToOption(Report_invalid_placeholders_in_string_dot_format_calls, ValidateFormatStringOption.ReportInvalidPlaceholdersInStringDotFormatCalls, LanguageNames.CSharp);

            BindToOption(Colorize_regular_expressions, RegularExpressionsOptions.ColorizeRegexPatterns, LanguageNames.CSharp);
            BindToOption(Report_invalid_regular_expressions, RegularExpressionsOptions.ReportInvalidRegexPatterns, LanguageNames.CSharp);
            BindToOption(Highlight_related_components_under_cursor, RegularExpressionsOptions.HighlightRelatedRegexComponentsUnderCursor, LanguageNames.CSharp);
            BindToOption(Show_completion_list, RegularExpressionsOptions.ProvideRegexCompletions, LanguageNames.CSharp);

            BindToOption(Editor_color_scheme, ColorSchemeOptions.ColorScheme);
        }

        // Since the VS theme can change after this dialog is constructed, we need to update the
        // Color Scheme state based on the current value of the VS Theme before it is rendered.
        protected override void OnRender(DrawingContext drawingContext)
        {
            var isKnownTheme = _colorSchemeApplier.IsKnownTheme();

            Editor_color_scheme.IsEnabled = isKnownTheme;
            Custom_VS_Theme_Warning.Visibility = isKnownTheme ? Visibility.Collapsed : Visibility.Visible;

            base.OnRender(drawingContext);
        }
    }
}
