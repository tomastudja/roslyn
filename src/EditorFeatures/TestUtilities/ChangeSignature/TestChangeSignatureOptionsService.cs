﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
        public bool IsCancelled = true;
        public AddedParameterOrExistingIndex[] UpdatedSignature = null;

        [ImportingConstructor]
        public TestChangeSignatureOptionsService()
        {
        }

        AddedParameterResult IChangeSignatureOptionsService.GetAddedParameter(Document document, int insertPosition)
        {
            throw new System.NotImplementedException();
        }

        ChangeSignatureOptionsResult IChangeSignatureOptionsService.GetChangeSignatureOptions(
            ISymbol symbol, int insertPosition, ParameterConfiguration parameters,
            Document document)
        {
            var list = parameters.ToListOfParameters();
            var updateParameters = UpdatedSignature.Select(item => item.IsExisting
            ? list[item.OldIndex]
            : item.AddedParameter).ToList();

            return new ChangeSignatureOptionsResult
            {
                IsCancelled = IsCancelled,
                UpdatedSignature = new SignatureChange(
                    parameters,
                    UpdatedSignature == null
                    ? parameters
                    : ParameterConfiguration.Create(
                        updateParameters,
                        parameters.ThisParameter != null,
                        selectedIndex: 0))
            };
        }
    }
}
