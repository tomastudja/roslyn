﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.VisualStudio.Debugger.Evaluation;
using Microsoft.VisualStudio.Debugger.Evaluation.ClrCompilation;

namespace Microsoft.CodeAnalysis.ExpressionEvaluator
{
    internal sealed class RootHiddenExpansion : Expansion
    {
        internal static Expansion CreateExpansion(
            MemberAndDeclarationInfo members,
            CustomTypeInfoTypeArgumentMap customTypeInfoMap)
        {
            return new RootHiddenExpansion(members, customTypeInfoMap);
        }

        private readonly MemberAndDeclarationInfo _member;
        private readonly CustomTypeInfoTypeArgumentMap _customTypeInfoMap;

        internal RootHiddenExpansion(MemberAndDeclarationInfo member, CustomTypeInfoTypeArgumentMap customTypeInfoMap)
        {
            _member = member;
            _customTypeInfoMap = customTypeInfoMap;
        }

        internal override void GetRows(
            ResultProvider resultProvider,
            ArrayBuilder<EvalResult> rows,
            DkmInspectionContext inspectionContext,
            EvalResultDataItem parent,
            DkmClrValue value,
            int startIndex,
            int count,
            bool visitAll,
            ref int index)
        {
            var memberValue = value.GetMemberValue(_member, inspectionContext);
            var isDynamicDebugViewEmptyException = memberValue.Type.GetLmrType().IsDynamicDebugViewEmptyException();
            if (isDynamicDebugViewEmptyException || memberValue.IsError())
            {
                if (InRange(startIndex, count, index))
                {
                    if (isDynamicDebugViewEmptyException)
                    {
                        var emptyMember = memberValue.Type.GetMemberByName("Empty");
                        memberValue = memberValue.GetMemberValue(emptyMember, inspectionContext);
                    }
                    var row = new EvalResult(Resources.ErrorName, (string)memberValue.HostObjectValue, inspectionContext);
                    rows.Add(row);
                }
                index++;
            }
            else
            {
                var other = MemberExpansion.CreateMemberDataItem(
                    resultProvider,
                    inspectionContext,
                    _member,
                    memberValue,
                    parent,
                    _customTypeInfoMap,
                    ExpansionFlags.IncludeBaseMembers | ExpansionFlags.IncludeResultsView);
                var expansion = other.Expansion;
                if (expansion != null)
                {
                    expansion.GetRows(resultProvider, rows, inspectionContext, other.ToDataItem(), other.Value, startIndex, count, visitAll, ref index);
                }
            }
        }
    }
}
