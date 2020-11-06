﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editor.Host;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.VisualStudio.LanguageServices.Implementation.PullMemberUp.MainDialog;
using Microsoft.VisualStudio.LanguageServices.Implementation.Utilities;
using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.CommonControls
{
    internal class MemberSelectionViewModel : AbstractNotifyPropertyChanged
    {
        private readonly IWaitIndicator _waitIndicator;
        private readonly ImmutableDictionary<ISymbol, Task<ImmutableArray<ISymbol>>> _symbolToDependentsMap;
        private readonly ImmutableDictionary<ISymbol, PullMemberUpSymbolViewModel> _symbolToMemberViewMap;

        public MemberSelectionViewModel(
            IWaitIndicator waitIndicator,
            ImmutableArray<PullMemberUpSymbolViewModel> members,
            ImmutableDictionary<ISymbol, Task<ImmutableArray<ISymbol>>> dependentsMap,
            TypeKind destinationTypeKind = TypeKind.Class)
        {
            _waitIndicator = waitIndicator;
            // Use public property to hook property change events up
            Members = members;
            _symbolToDependentsMap = dependentsMap;
            _symbolToMemberViewMap = members.ToImmutableDictionary(memberViewModel => memberViewModel.Symbol);

            UpdateMembersBasedOnDestinationKind(destinationTypeKind);
        }

        public ImmutableArray<PullMemberUpSymbolViewModel> CheckedMembers => Members.WhereAsArray(m => m.IsChecked && m.IsCheckable);

        private ImmutableArray<PullMemberUpSymbolViewModel> _members;
        public ImmutableArray<PullMemberUpSymbolViewModel> Members
        {
            get => _members;
            set
            {
                var oldMembers = _members;
                if (SetProperty(ref _members, value))
                {
                    // If we have registered for events before, remove the handlers
                    // to be a good citizen in the world 
                    if (!oldMembers.IsDefaultOrEmpty)
                    {
                        foreach (var oldMember in oldMembers)
                        {
                            oldMember.PropertyChanged -= MemberPropertyChangedHandler;
                        }
                    }

                    foreach (var member in _members)
                    {
                        member.PropertyChanged += MemberPropertyChangedHandler;
                    }
                }
            }
        }

        private void MemberPropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PullMemberUpSymbolViewModel.IsChecked))
            {
                // Hook the CheckedMembers property change to each individual member checked status change
                NotifyPropertyChanged(nameof(CheckedMembers));
            }
        }

        public void SelectPublic()
            => SelectMembers(Members.WhereAsArray(v => v.Symbol.DeclaredAccessibility == Accessibility.Public));

        public void SelectAll()
            => SelectMembers(Members);

        internal void DeselectAll()
            => SelectMembers(Members, isChecked: false);

        public void SelectDependents()
        {
            var checkedMembers = Members
              .WhereAsArray(member => member.IsChecked && member.IsCheckable);

            var waitResult = _waitIndicator.Wait(
                    title: ServicesVSResources.Pull_Members_Up,
                    message: ServicesVSResources.Calculating_dependents,
                    allowCancel: true,
                    showProgress: true,
                    context =>
                    {
                        foreach (var member in Members)
                        {
                            _symbolToDependentsMap[member.Symbol].Wait(context.CancellationToken);
                        }
                    });

            if (waitResult == WaitIndicatorResult.Completed)
            {
                foreach (var member in checkedMembers)
                {
                    var membersToSelected = FindDependentsRecursively(member.Symbol).SelectAsArray(symbol => _symbolToMemberViewMap[symbol]);
                    SelectMembers(membersToSelected);
                }
            }
        }

        public ImmutableArray<(ISymbol member, bool makeAbstract)> GetSelectedMembers()
            => Members.
                Where(memberSymbolView => memberSymbolView.IsChecked && memberSymbolView.IsCheckable).
                SelectAsArray(memberViewModel =>
                    (member: memberViewModel.Symbol,
                    makeAbstract: memberViewModel.IsMakeAbstractCheckable && memberViewModel.MakeAbstract));

        public void UpdateMembersBasedOnDestinationKind(TypeKind destinationType)
        {
            var fields = Members.WhereAsArray(memberViewModel => memberViewModel.Symbol.IsKind(SymbolKind.Field));
            var makeAbstractEnabledCheckboxes = Members.
                WhereAsArray(memberViewModel => !memberViewModel.Symbol.IsKind(SymbolKind.Field) && !memberViewModel.Symbol.IsAbstract);
            var isInterface = destinationType == TypeKind.Interface;

            // Disable field check box and make abstract if destination is interface
            foreach (var member in fields)
            {
                member.IsCheckable = !isInterface;
                member.TooltipText = isInterface ? ServicesVSResources.Interface_cannot_have_field : string.Empty;
            }

            foreach (var member in makeAbstractEnabledCheckboxes)
            {
                member.IsMakeAbstractCheckable = !isInterface;
            }
        }

        private void SelectMembers(ImmutableArray<PullMemberUpSymbolViewModel> members, bool isChecked = true)
        {
            foreach (var member in members.Where(viewModel => viewModel.IsCheckable))
            {
                member.IsChecked = isChecked;
            }
        }

        private ImmutableHashSet<ISymbol> FindDependentsRecursively(ISymbol member)
        {
            var queue = new Queue<ISymbol>();
            // Under situation like two methods call each other, this hashset is used to 
            // prevent the infinity loop.
            var visited = new HashSet<ISymbol>();
            var result = new HashSet<ISymbol>();
            queue.Enqueue(member);
            visited.Add(member);
            while (!queue.IsEmpty())
            {
                var currentMember = queue.Dequeue();
                result.Add(currentMember);
                visited.Add(currentMember);
                foreach (var dependent in _symbolToDependentsMap[currentMember].Result)
                {
                    if (!visited.Contains(dependent))
                    {
                        queue.Enqueue(dependent);
                    }
                }
            }

            return result.ToImmutableHashSet();
        }
    }
}
