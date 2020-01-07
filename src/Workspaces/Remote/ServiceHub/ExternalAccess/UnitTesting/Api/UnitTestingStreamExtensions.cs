﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.Remote;
using Newtonsoft.Json;
using Roslyn.Utilities;
using StreamJsonRpc;

namespace Microsoft.CodeAnalysis.ExternalAccess.UnitTesting.Api
{
    internal static class UnitTestingStreamExtensions
    {
        public static JsonRpc UnitTesting_CreateStreamJsonRpc(
            this Stream stream,
            object? target,
            TraceSource logger,
            IEnumerable<AggregateJsonConverter>? jsonConverters = null)
        {
            jsonConverters ??= SpecializedCollections.EmptyEnumerable<AggregateJsonConverter>();

            var jsonFormatter = new JsonMessageFormatter();
            jsonFormatter.JsonSerializer.Converters.AddRange(jsonConverters.Concat(AggregateJsonConverter.Instance));

            return new JsonRpc(new HeaderDelimitedMessageHandler(stream, jsonFormatter), target)
            {
                CancelLocallyInvokedMethodsWhenConnectionIsClosed = true,
                TraceSource = logger
            };
        }
    }
}
