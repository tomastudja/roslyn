﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Serialization;

namespace Microsoft.CodeAnalysis.Remote;

[DataContract]
internal readonly record struct RemoteServiceCallbackId(int id)
{
    [DataMember(Order = 0)]
    public readonly int Id = id;
}
