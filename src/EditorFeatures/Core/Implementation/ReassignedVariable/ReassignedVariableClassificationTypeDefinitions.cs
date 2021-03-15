﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.ReassignedVariable
{
    internal class ReassignedVariableClassificationTypeDefinitions
    {
        [Export]
        [Name(ClassificationTypeNames.ReassignedVariable)]
        [BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
        internal readonly ClassificationTypeDefinition? ReassignedVariableTypeDefinition;
    }
}
