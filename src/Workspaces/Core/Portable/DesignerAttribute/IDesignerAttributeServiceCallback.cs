﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.DesignerAttribute
{
    /// <summary>
    /// Callback the host (VS) passes to the OOP service to allow it to send batch notifications
    /// about designer attribute info.  There is no guarantee that the host will have done anything
    /// with this data when the callback returns, only that it will try to inform the project system
    /// about the designer attribute info in the future.
    /// </summary>
    internal interface IDesignerAttributeServiceCallback
    {
        Task RegisterDesignerAttributesAsync(IList<DesignerInfo> infos, CancellationToken cancellationToken);
    }
}
