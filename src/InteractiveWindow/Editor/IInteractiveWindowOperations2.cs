﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.InteractiveWindow
{
    public interface IInteractiveWindowOperations2 : IInteractiveWindowOperations
    {
        /// <summary>
        /// Copies the current selection to the clipboard.
        /// </summary>
        void Copy();
    }
}
