﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.InheritanceMargin;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin.MarginGlyph;
using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin
{
    internal static class InheritanceMarginHelpers
    {
        private static readonly ImmutableHashSet<InheritanceRelationship> s_relationshipsShownAs_I_UpArrow
            = ImmutableHashSet<InheritanceRelationship>.Empty
            .Add(InheritanceRelationship.ImplementedInterface)
            .Add(InheritanceRelationship.InheritedInterface)
            .Add(InheritanceRelationship.ImplmentedMember);

        private static readonly ImmutableHashSet<InheritanceRelationship> s_relationshipsShownAs_I_DownArrow
            = ImmutableHashSet<InheritanceRelationship>.Empty
            .Add(InheritanceRelationship.ImplementingType)
            .Add(InheritanceRelationship.ImplementingMember);

        private static readonly ImmutableHashSet<InheritanceRelationship> s_relationshipsShownAs_O_UpArrow
            = ImmutableHashSet<InheritanceRelationship>.Empty
            .Add(InheritanceRelationship.BaseType)
            .Add(InheritanceRelationship.OverriddenMember);

        private static readonly ImmutableHashSet<InheritanceRelationship> s_relationshipsShownAs_O_DownArrow
            = ImmutableHashSet<InheritanceRelationship>.Empty
            .Add(InheritanceRelationship.DerivedType)
            .Add(InheritanceRelationship.OverridingMember);

        /// <summary>
        /// Decide which moniker should be shown.
        /// </summary>
        public static ImageMoniker GetMoniker(InheritanceRelationship inheritanceRelationship)
        {
            //  If there are multiple targets and we have the corresponding compound image, use it
            if (s_relationshipsShownAs_I_UpArrow.Any(flag => inheritanceRelationship.HasFlag(flag))
                && s_relationshipsShownAs_O_DownArrow.Any(flag => inheritanceRelationship.HasFlag(flag)))
            {
                return KnownMonikers.ImplementingOverridden;
            }

            if (s_relationshipsShownAs_I_UpArrow.Any(flag => inheritanceRelationship.HasFlag(flag))
                && s_relationshipsShownAs_O_UpArrow.Any(flag => inheritanceRelationship.HasFlag(flag)))
            {
                return KnownMonikers.ImplementingOverriding;
            }

            // Otherwise, show the image based on this preference
            if (s_relationshipsShownAs_I_UpArrow.Any(flag => inheritanceRelationship.HasFlag(flag)))
            {
                return KnownMonikers.Implementing;
            }

            if (s_relationshipsShownAs_I_DownArrow.Any(flag => inheritanceRelationship.HasFlag(flag)))
            {
                return KnownMonikers.Implemented;
            }

            if (s_relationshipsShownAs_O_UpArrow.Any(flag => inheritanceRelationship.HasFlag(flag)))
            {
                return KnownMonikers.Overriding;
            }

            if (s_relationshipsShownAs_O_DownArrow.Any(flag => inheritanceRelationship.HasFlag(flag)))
            {
                return KnownMonikers.Overridden;
            }

            // The relationship is None. Don't know what image should be shown, throws
            throw ExceptionUtilities.UnexpectedValue(inheritanceRelationship);
        }

        public static ImmutableArray<InheritanceMenuItemViewModel> CreateMenuItemViewModelsForSingleMember(ImmutableArray<InheritanceTargetItem> targets)
        {
            var targetsByRelationship = targets
                .OrderBy(target => target.DisplayName)
                .GroupBy(target => target.RelationToMember)
                .ToImmutableDictionary(
                    keySelector: grouping => grouping.Key,
                    elementSelector: grouping => grouping);

            return targetsByRelationship.SelectMany(kvp => CreateMenuItemsWithHeader(kvp.Key, kvp.Value)).ToImmutableArray();
        }

        /// <summary>
        /// Create the view models for the inheritance targets of multiple members
        /// There are two cases:
        /// 1. If all the targets have the same inheritance relationship. It would have this structure:
        /// e.g.
        /// MemberViewModel1 -> Target1ViewModel
        ///                     Target2ViewModel
        /// MemberViewModel2 -> Target4ViewModel
        ///                     Target5ViewModel
        ///
        /// 2. If targets belongs to different inheritance group. It would be grouped.
        /// e.g.
        /// MemberViewModel1 -> HeaderViewModel
        ///                     Target1ViewModel
        ///                     HeaderViewModel
        ///                     Target2ViewModel
        /// MemberViewModel2 -> HeaderViewModel
        ///                     Target4ViewModel
        ///                     HeaderViewModel
        ///                     Target5ViewModel
        /// </summary>
        public static ImmutableArray<InheritanceMenuItemViewModel> CreateMenuItemViewModelsForMultipleMembers(ImmutableArray<InheritanceMarginItem> members)
        {
            Contract.ThrowIfTrue(members.Length <= 1);
            // For multiple members, check if all the targets have the same inheritance relationship.
            // If so, then don't add the header, because it is already indicated by the margin.
            // Otherwise, add the Header.
            return members.SelectAsArray(MemberMenuItemViewModel.CreateWithHeaderInTargets).CastArray<InheritanceMenuItemViewModel>();
        }

        public static ImmutableArray<InheritanceMenuItemViewModel> CreateMenuItemsWithHeader(
            InheritanceRelationship relationship,
            IEnumerable<InheritanceTargetItem> targets)
        {
            using var _ = CodeAnalysis.PooledObjects.ArrayBuilder<InheritanceMenuItemViewModel>.GetInstance(out var builder);
            var displayContent = relationship switch
            {
                InheritanceRelationship.ImplementedInterface => ServicesVSResources.Implemented_interfaces,
                InheritanceRelationship.BaseType => ServicesVSResources.Base_Types,
                InheritanceRelationship.DerivedType => ServicesVSResources.Derived_types,
                InheritanceRelationship.InheritedInterface => ServicesVSResources.Inherited_interfaces,
                InheritanceRelationship.ImplementingType => ServicesVSResources.Implementing_types,
                InheritanceRelationship.ImplmentedMember => ServicesVSResources.Implemented_members,
                InheritanceRelationship.OverriddenMember => ServicesVSResources.Overridden_members,
                InheritanceRelationship.OverridingMember => ServicesVSResources.Overriding_members,
                InheritanceRelationship.ImplementingMember => ServicesVSResources.Implementing_members,
                _ => throw ExceptionUtilities.UnexpectedValue(relationship)
            };

            var headerViewModel = new HeaderMenuItemViewModel(displayContent, GetMoniker(relationship), displayContent);
            builder.Add(headerViewModel);
            foreach (var targetItem in targets)
            {
                builder.Add(TargetMenuItemViewModel.Create(targetItem, indent: true));
            }

            return builder.ToImmutable();
        }
    }
}
