﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Namespace Microsoft.VisualStudio.LanguageServices.VisualBasic.CodeModel
    <Flags>
    Friend Enum ModifierFlags
        ' Note: These are in the order that they appear in modifier lists as generated by Code Model.

        [Partial] = 1 << 0
        [Default] = 1 << 1
        [Private] = 1 << 2
        [Protected] = 1 << 3
        [Public] = 1 << 4
        [Friend] = 1 << 5
        [MustOverride] = 1 << 6
        [Overridable] = 1 << 7
        [NotOverridable] = 1 << 8
        [Overrides] = 1 << 9
        [MustInherit] = 1 << 10
        [NotInheritable] = 1 << 11
        [Static] = 1 << 12
        [Shared] = 1 << 13
        [Shadows] = 1 << 14
        [ReadOnly] = 1 << 15
        [WriteOnly] = 1 << 16
        [Dim] = 1 << 17
        [Const] = 1 << 18
        [WithEvents] = 1 << 19
        [Widening] = 1 << 20
        [Narrowing] = 1 << 21
        [Custom] = 1 << 22
        [ByVal] = 1 << 23
        [ByRef] = 1 << 24
        [Optional] = 1 << 25
        [ParamArray] = 1 << 26

        AccessModifierMask = [Private] Or [Protected] Or [Public] Or [Friend]
    End Enum
End Namespace
