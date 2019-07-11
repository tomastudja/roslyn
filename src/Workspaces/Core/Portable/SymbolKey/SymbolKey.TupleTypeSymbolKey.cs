﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis
{
    internal partial struct SymbolKey
    {
        private static class TupleTypeSymbolKey
        {
            public static void Create(INamedTypeSymbol symbol, SymbolKeyWriter visitor)
            {
                Debug.Assert(symbol.IsTupleType);

                var friendlyNames = ArrayBuilder<string>.GetInstance();
                var locations = ArrayBuilder<Location>.GetInstance();

                var isError = symbol.TupleUnderlyingType.TypeKind == TypeKind.Error;
                visitor.WriteBoolean(isError);

                if (isError)
                {
                    var elementTypes = ArrayBuilder<ISymbol>.GetInstance();

                    foreach (var element in symbol.TupleElements)
                    {
                        elementTypes.Add(element.Type);
                    }

                    visitor.WriteSymbolKeyArray(elementTypes.ToImmutableAndFree());
                }
                else
                {
                    visitor.WriteSymbolKey(symbol.TupleUnderlyingType);
                }

                foreach (var element in symbol.TupleElements)
                {
                    friendlyNames.Add(element.IsImplicitlyDeclared ? null : element.Name);
                    locations.Add(element.Locations.FirstOrDefault() ?? Location.None);
                }

                visitor.WriteStringArray(friendlyNames.ToImmutableAndFree());
                visitor.WriteLocationArray(locations.ToImmutableAndFree());
            }

            public static SymbolKeyResolution Resolve(SymbolKeyReader reader)
            {
                var isError = reader.ReadBoolean();
                if (isError)
                {
                    using var elementTypes = reader.ReadSymbolArray<ITypeSymbol>();
                    using var elementNames = reader.ReadStringArray();
                    var elementLocations = ReadElementLocations(reader);

                    if (elementTypes.Count == elementNames.Count)
                    {
                        try
                        {
                            var result = reader.Compilation.CreateTupleTypeSymbol(
                                elementTypes.ToImmutable(), elementNames.ToImmutable(), elementLocations);
                            return new SymbolKeyResolution(result);
                        }
                        catch (ArgumentException)
                        {
                        }
                    }
                }
                else
                {
                    var underlyingTypeResolution = reader.ReadSymbolKey();
                    using var elementNamesBuilder = reader.ReadStringArray();
                    var elementLocations = ReadElementLocations(reader);

                    try
                    {
                        using var result = PooledArrayBuilder<INamedTypeSymbol>.GetInstance();

                        var elementNames = elementNamesBuilder.ToImmutable();
                        foreach (var symbol in underlyingTypeResolution)
                        {
                            if (symbol is INamedTypeSymbol namedType)
                            {
                                result.AddIfNotNull(reader.Compilation.CreateTupleTypeSymbol(
                                    namedType, elementNames, elementLocations));
                            }
                        }

                        return CreateSymbolInfo(result);
                    }
                    catch (ArgumentException)
                    {
                    }
                }

                return new SymbolKeyResolution(reader.Compilation.ObjectType);
            }

            private static ImmutableArray<Location> ReadElementLocations(SymbolKeyReader reader)
            {
                using var elementLocations = reader.ReadLocationArray();

                // Compiler API requires that all the locations are non-null, or that there is a default
                // immutable array passed in.
                if (elementLocations.Builder.All(loc => loc == null))
                {
                    return default;
                }

                return elementLocations.ToImmutable();
            }
        }
    }
}
