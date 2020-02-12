﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis.ChangeSignature;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Test.Utilities.ChangeSignature;

namespace Microsoft.CodeAnalysis.Editor.UnitTests.ChangeSignature
{
    [ExportWorkspaceService(typeof(IChangeSignatureOptionsService), ServiceLayer.Default), Shared]
    internal class TestChangeSignatureOptionsService : IChangeSignatureOptionsService
    {
        public AddedParameterOrExistingIndex[]? UpdatedSignature = null;

        [ImportingConstructor]
        public TestChangeSignatureOptionsService()
        {
        }

        ChangeSignatureOptionsResult IChangeSignatureOptionsService.GetChangeSignatureOptions(
            Document document,
            int insertPosition,
            ISymbol symbol,
            ParameterConfiguration parameters)
        {
            var list = parameters.ToListOfParameters();
            IEnumerable<Parameter> updateParameters = UpdatedSignature != null
                ? UpdatedSignature.Select(item => item.IsExisting ? list[item.OldIndex ?? -1] : item.GetAddedParameter(document))
                : new Parameter?[0]!;
            return new ChangeSignatureOptionsResult(new SignatureChange(
                    parameters,
                    UpdatedSignature == null
                    ? parameters
                    : ParameterConfiguration.Create(updateParameters, parameters.ThisParameter != null, selectedIndex: 0)), previewChanges: false);
        }
    }
}
