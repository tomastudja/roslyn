﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp
{
    // DO NOT CHANGE NUMBERS ASSIGNED TO EXISTING KINDS OR YOU WILL BREAK BINARY COMPATIBILITY
    public enum SyntaxKind : ushort
    {
        None = 0,
        List = GreenNode.ListKind,

        // punctuation
        TildeToken = 8193,
        ExclamationToken = 8194,
        DollarToken = 8195,
        PercentToken = 8196,
        CaretToken = 8197,
        AmpersandToken = 8198,
        AsteriskToken = 8199,
        OpenParenToken = 8200,
        CloseParenToken = 8201,
        MinusToken = 8202,
        PlusToken = 8203,
        EqualsToken = 8204,
        OpenBraceToken = 8205,
        CloseBraceToken = 8206,
        OpenBracketToken = 8207,
        CloseBracketToken = 8208,
        BarToken = 8209,
        BackslashToken = 8210,
        ColonToken = 8211,
        SemicolonToken = 8212,
        DoubleQuoteToken = 8213,
        SingleQuoteToken = 8214,
        LessThanToken = 8215,
        CommaToken = 8216,
        GreaterThanToken = 8217,
        DotToken = 8218,
        QuestionToken = 8219,
        HashToken = 8220,
        SlashToken = 8221,
        DotDotToken = 8222,

        // additional xml tokens
        SlashGreaterThanToken = 8232, // xml empty element end
        LessThanSlashToken = 8233, // element end tag start token
        XmlCommentStartToken = 8234, // <!--
        XmlCommentEndToken = 8235, // -->
        XmlCDataStartToken = 8236, // <![CDATA[
        XmlCDataEndToken = 8237, // ]]>
        XmlProcessingInstructionStartToken = 8238, // <?
        XmlProcessingInstructionEndToken = 8239, // ?>

        // compound punctuation
        BarBarToken = 8260,
        AmpersandAmpersandToken = 8261,
        MinusMinusToken = 8262,
        PlusPlusToken = 8263,
        ColonColonToken = 8264,
        QuestionQuestionToken = 8265,
        MinusGreaterThanToken = 8266,
        ExclamationEqualsToken = 8267,
        EqualsEqualsToken = 8268,
        EqualsGreaterThanToken = 8269,
        LessThanEqualsToken = 8270,
        LessThanLessThanToken = 8271,
        LessThanLessThanEqualsToken = 8272,
        GreaterThanEqualsToken = 8273,
        GreaterThanGreaterThanToken = 8274,
        GreaterThanGreaterThanEqualsToken = 8275,
        SlashEqualsToken = 8276,
        AsteriskEqualsToken = 8277,
        BarEqualsToken = 8278,
        AmpersandEqualsToken = 8279,
        PlusEqualsToken = 8280,
        MinusEqualsToken = 8281,
        CaretEqualsToken = 8282,
        PercentEqualsToken = 8283,
        QuestionQuestionEqualsToken = 8284,

        // Keywords
        BoolKeyword = 8304,
        ByteKeyword = 8305,
        SByteKeyword = 8306,
        ShortKeyword = 8307,
        UShortKeyword = 8308,
        IntKeyword = 8309,
        UIntKeyword = 8310,
        LongKeyword = 8311,
        ULongKeyword = 8312,
        DoubleKeyword = 8313,
        FloatKeyword = 8314,
        DecimalKeyword = 8315,
        StringKeyword = 8316,
        CharKeyword = 8317,
        VoidKeyword = 8318,
        ObjectKeyword = 8319,
        TypeOfKeyword = 8320,
        SizeOfKeyword = 8321,
        NullKeyword = 8322,
        TrueKeyword = 8323,
        FalseKeyword = 8324,
        IfKeyword = 8325,
        ElseKeyword = 8326,
        WhileKeyword = 8327,
        ForKeyword = 8328,
        ForEachKeyword = 8329,
        DoKeyword = 8330,
        SwitchKeyword = 8331,
        CaseKeyword = 8332,
        DefaultKeyword = 8333,
        TryKeyword = 8334,
        CatchKeyword = 8335,
        FinallyKeyword = 8336,
        LockKeyword = 8337,
        GotoKeyword = 8338,
        BreakKeyword = 8339,
        ContinueKeyword = 8340,
        ReturnKeyword = 8341,
        ThrowKeyword = 8342,
        PublicKeyword = 8343,
        PrivateKeyword = 8344,
        InternalKeyword = 8345,
        ProtectedKeyword = 8346,
        StaticKeyword = 8347,
        ReadOnlyKeyword = 8348,
        SealedKeyword = 8349,
        ConstKeyword = 8350,
        FixedKeyword = 8351,
        StackAllocKeyword = 8352,
        VolatileKeyword = 8353,
        NewKeyword = 8354,
        OverrideKeyword = 8355,
        AbstractKeyword = 8356,
        VirtualKeyword = 8357,
        EventKeyword = 8358,
        ExternKeyword = 8359,
        RefKeyword = 8360,
        OutKeyword = 8361,
        InKeyword = 8362,
        IsKeyword = 8363,
        AsKeyword = 8364,
        ParamsKeyword = 8365,
        ArgListKeyword = 8366,
        MakeRefKeyword = 8367,
        RefTypeKeyword = 8368,
        RefValueKeyword = 8369,
        ThisKeyword = 8370,
        BaseKeyword = 8371,
        NamespaceKeyword = 8372,
        UsingKeyword = 8373,
        ClassKeyword = 8374,
        StructKeyword = 8375,
        InterfaceKeyword = 8376,
        EnumKeyword = 8377,
        DelegateKeyword = 8378,
        CheckedKeyword = 8379,
        UncheckedKeyword = 8380,
        UnsafeKeyword = 8381,
        OperatorKeyword = 8382,
        ExplicitKeyword = 8383,
        ImplicitKeyword = 8384,

        // contextual keywords
        YieldKeyword = 8405,
        PartialKeyword = 8406,
        AliasKeyword = 8407,
        GlobalKeyword = 8408,
        AssemblyKeyword = 8409,
        ModuleKeyword = 8410,
        TypeKeyword = 8411,
        FieldKeyword = 8412,
        MethodKeyword = 8413,
        ParamKeyword = 8414,
        PropertyKeyword = 8415,
        TypeVarKeyword = 8416,
        GetKeyword = 8417,
        SetKeyword = 8418,
        AddKeyword = 8419,
        RemoveKeyword = 8420,
        WhereKeyword = 8421,
        FromKeyword = 8422,
        GroupKeyword = 8423,
        JoinKeyword = 8424,
        IntoKeyword = 8425,
        LetKeyword = 8426,
        ByKeyword = 8427,
        SelectKeyword = 8428,
        OrderByKeyword = 8429,
        OnKeyword = 8430,
        EqualsKeyword = 8431,
        AscendingKeyword = 8432,
        DescendingKeyword = 8433,
        NameOfKeyword = 8434,
        AsyncKeyword = 8435,
        AwaitKeyword = 8436,
        WhenKeyword = 8437,
        OrKeyword = 8438,
        AndKeyword = 8439,
        NotKeyword = 8440,
        DataKeyword = 8441,
        WithKeyword = 8442,
        InitKeyword = 8443,
        RecordKeyword = 8444,

        /// when adding a contextual keyword following functions must be adapted:
        /// <see cref="SyntaxFacts.GetContextualKeywordKinds"/>
        /// <see cref="SyntaxFacts.IsContextualKeyword(SyntaxKind)"/>
        /// <see cref="SyntaxFacts.GetContextualKeywordKind(string)"/>

        // keywords with an enum value less than ElifKeyword are considered i.a. contextual keywords
        // additional preprocessor keywords
        ElifKeyword = 8467,
        EndIfKeyword = 8468,
        RegionKeyword = 8469,
        EndRegionKeyword = 8470,
        DefineKeyword = 8471,
        UndefKeyword = 8472,
        WarningKeyword = 8473,
        ErrorKeyword = 8474,
        LineKeyword = 8475,
        PragmaKeyword = 8476,
        HiddenKeyword = 8477,
        ChecksumKeyword = 8478,
        DisableKeyword = 8479,
        RestoreKeyword = 8480,
        ReferenceKeyword = 8481,

        InterpolatedStringStartToken = 8482,            // $"
        InterpolatedStringEndToken = 8483,              // "
        InterpolatedVerbatimStringStartToken = 8484,    // $@" or @$"

        // additional preprocessor keywords (continued)
        LoadKeyword = 8485,
        NullableKeyword = 8486,
        EnableKeyword = 8487,

        // targets for #nullable directive
        WarningsKeyword = 8488,
        AnnotationsKeyword = 8489,

        // Other
        VarKeyword = 8490,
        UnderscoreToken = 8491,
        OmittedTypeArgumentToken = 8492,
        OmittedArraySizeExpressionToken = 8493,
        EndOfDirectiveToken = 8494,
        EndOfDocumentationCommentToken = 8495,
        EndOfFileToken = 8496, //NB: this is assumed to be the last textless token

        // tokens with text
        BadToken = 8507,
        IdentifierToken = 8508,
        NumericLiteralToken = 8509,
        CharacterLiteralToken = 8510,
        StringLiteralToken = 8511,
        XmlEntityLiteralToken = 8512,  // &lt; &gt; &quot; &amp; &apos; or &name; or &#nnnn; or &#xhhhh;
        XmlTextLiteralToken = 8513,    // xml text node text
        XmlTextLiteralNewLineToken = 8514,

        InterpolatedStringToken = 8515,                 // terminal for a whole interpolated string $" ... { expr } ..."
                                                        // This only exists in transient form during parsing.
        InterpolatedStringTextToken = 8517,             // literal text that is part of an interpolated string

        // trivia
        EndOfLineTrivia = 8539,
        WhitespaceTrivia = 8540,
        SingleLineCommentTrivia = 8541,
        MultiLineCommentTrivia = 8542,
        DocumentationCommentExteriorTrivia = 8543,
        SingleLineDocumentationCommentTrivia = 8544,
        MultiLineDocumentationCommentTrivia = 8545,
        DisabledTextTrivia = 8546,
        PreprocessingMessageTrivia = 8547,
        IfDirectiveTrivia = 8548,
        ElifDirectiveTrivia = 8549,
        ElseDirectiveTrivia = 8550,
        EndIfDirectiveTrivia = 8551,
        RegionDirectiveTrivia = 8552,
        EndRegionDirectiveTrivia = 8553,
        DefineDirectiveTrivia = 8554,
        UndefDirectiveTrivia = 8555,
        ErrorDirectiveTrivia = 8556,
        WarningDirectiveTrivia = 8557,
        LineDirectiveTrivia = 8558,
        PragmaWarningDirectiveTrivia = 8559,
        PragmaChecksumDirectiveTrivia = 8560,
        ReferenceDirectiveTrivia = 8561,
        BadDirectiveTrivia = 8562,
        SkippedTokensTrivia = 8563,
        ConflictMarkerTrivia = 8564,

        // xml nodes (for xml doc comment structure)
        XmlElement = 8574,
        XmlElementStartTag = 8575,
        XmlElementEndTag = 8576,
        XmlEmptyElement = 8577,
        XmlTextAttribute = 8578,
        XmlCrefAttribute = 8579,
        XmlNameAttribute = 8580,
        XmlName = 8581,
        XmlPrefix = 8582,
        XmlText = 8583,
        XmlCDataSection = 8584,
        XmlComment = 8585,
        XmlProcessingInstruction = 8586,

        // documentation comment nodes (structure inside DocumentationCommentTrivia)
        TypeCref = 8597,
        QualifiedCref = 8598,
        NameMemberCref = 8599,
        IndexerMemberCref = 8600,
        OperatorMemberCref = 8601,
        ConversionOperatorMemberCref = 8602,
        CrefParameterList = 8603,
        CrefBracketedParameterList = 8604,
        CrefParameter = 8605,

        // names & type-names
        IdentifierName = 8616,
        QualifiedName = 8617,
        GenericName = 8618,
        TypeArgumentList = 8619,
        AliasQualifiedName = 8620,
        PredefinedType = 8621,
        ArrayType = 8622,
        ArrayRankSpecifier = 8623,
        PointerType = 8624,
        NullableType = 8625,
        OmittedTypeArgument = 8626,

        // expressions
        ParenthesizedExpression = 8632,
        ConditionalExpression = 8633,
        InvocationExpression = 8634,
        ElementAccessExpression = 8635,
        ArgumentList = 8636,
        BracketedArgumentList = 8637,
        Argument = 8638,
        NameColon = 8639,
        CastExpression = 8640,
        AnonymousMethodExpression = 8641,
        SimpleLambdaExpression = 8642,
        ParenthesizedLambdaExpression = 8643,
        ObjectInitializerExpression = 8644,
        CollectionInitializerExpression = 8645,
        ArrayInitializerExpression = 8646,
        AnonymousObjectMemberDeclarator = 8647,
        ComplexElementInitializerExpression = 8648,
        ObjectCreationExpression = 8649,
        AnonymousObjectCreationExpression = 8650,
        ArrayCreationExpression = 8651,
        ImplicitArrayCreationExpression = 8652,
        StackAllocArrayCreationExpression = 8653,
        OmittedArraySizeExpression = 8654,
        InterpolatedStringExpression = 8655,
        ImplicitElementAccess = 8656,
        IsPatternExpression = 8657,
        RangeExpression = 8658,
        ImplicitObjectCreationExpression = 8659,

        // binary expressions
        AddExpression = 8668,
        SubtractExpression = 8669,
        MultiplyExpression = 8670,
        DivideExpression = 8671,
        ModuloExpression = 8672,
        LeftShiftExpression = 8673,
        RightShiftExpression = 8674,
        LogicalOrExpression = 8675,
        LogicalAndExpression = 8676,
        BitwiseOrExpression = 8677,
        BitwiseAndExpression = 8678,
        ExclusiveOrExpression = 8679,
        EqualsExpression = 8680,
        NotEqualsExpression = 8681,
        LessThanExpression = 8682,
        LessThanOrEqualExpression = 8683,
        GreaterThanExpression = 8684,
        GreaterThanOrEqualExpression = 8685,
        IsExpression = 8686,
        AsExpression = 8687,
        CoalesceExpression = 8688,
        SimpleMemberAccessExpression = 8689,  // dot access:   a.b
        PointerMemberAccessExpression = 8690,  // arrow access:   a->b
        ConditionalAccessExpression = 8691,    // question mark access:   a?.b , a?[1]

        // binding expressions
        MemberBindingExpression = 8707,
        ElementBindingExpression = 8708,

        // binary assignment expressions
        SimpleAssignmentExpression = 8714,
        AddAssignmentExpression = 8715,
        SubtractAssignmentExpression = 8716,
        MultiplyAssignmentExpression = 8717,
        DivideAssignmentExpression = 8718,
        ModuloAssignmentExpression = 8719,
        AndAssignmentExpression = 8720,
        ExclusiveOrAssignmentExpression = 8721,
        OrAssignmentExpression = 8722,
        LeftShiftAssignmentExpression = 8723,
        RightShiftAssignmentExpression = 8724,
        CoalesceAssignmentExpression = 8725,

        // unary expressions
        UnaryPlusExpression = 8730,
        UnaryMinusExpression = 8731,
        BitwiseNotExpression = 8732,
        LogicalNotExpression = 8733,
        PreIncrementExpression = 8734,
        PreDecrementExpression = 8735,
        PointerIndirectionExpression = 8736,
        AddressOfExpression = 8737,
        PostIncrementExpression = 8738,
        PostDecrementExpression = 8739,
        AwaitExpression = 8740,
        IndexExpression = 8741,

        // primary expression
        ThisExpression = 8746,
        BaseExpression = 8747,
        ArgListExpression = 8748,
        NumericLiteralExpression = 8749,
        StringLiteralExpression = 8750,
        CharacterLiteralExpression = 8751,
        TrueLiteralExpression = 8752,
        FalseLiteralExpression = 8753,
        NullLiteralExpression = 8754,
        DefaultLiteralExpression = 8755,

        // primary function expressions
        TypeOfExpression = 8760,
        SizeOfExpression = 8761,
        CheckedExpression = 8762,
        UncheckedExpression = 8763,
        DefaultExpression = 8764,
        MakeRefExpression = 8765,
        RefValueExpression = 8766,
        RefTypeExpression = 8767,
        // NameOfExpression = 8768, // we represent nameof(x) as an invocation expression

        // query expressions
        QueryExpression = 8774,
        QueryBody = 8775,
        FromClause = 8776,
        LetClause = 8777,
        JoinClause = 8778,
        JoinIntoClause = 8779,
        WhereClause = 8780,
        OrderByClause = 8781,
        AscendingOrdering = 8782,
        DescendingOrdering = 8783,
        SelectClause = 8784,
        GroupClause = 8785,
        QueryContinuation = 8786,

        // statements
        Block = 8792,
        LocalDeclarationStatement = 8793,
        VariableDeclaration = 8794,
        VariableDeclarator = 8795,
        EqualsValueClause = 8796,
        ExpressionStatement = 8797,
        EmptyStatement = 8798,
        LabeledStatement = 8799,

        // jump statements
        GotoStatement = 8800,
        GotoCaseStatement = 8801,
        GotoDefaultStatement = 8802,
        BreakStatement = 8803,
        ContinueStatement = 8804,
        ReturnStatement = 8805,
        YieldReturnStatement = 8806,
        YieldBreakStatement = 8807,
        ThrowStatement = 8808,

        WhileStatement = 8809,
        DoStatement = 8810,
        ForStatement = 8811,
        ForEachStatement = 8812,
        UsingStatement = 8813,
        FixedStatement = 8814,

        // checked statements
        CheckedStatement = 8815,
        UncheckedStatement = 8816,

        UnsafeStatement = 8817,
        LockStatement = 8818,
        IfStatement = 8819,
        ElseClause = 8820,
        SwitchStatement = 8821,
        SwitchSection = 8822,
        CaseSwitchLabel = 8823,
        DefaultSwitchLabel = 8824,
        TryStatement = 8825,
        CatchClause = 8826,
        CatchDeclaration = 8827,
        CatchFilterClause = 8828,
        FinallyClause = 8829,

        // statements that didn't fit above
        LocalFunctionStatement = 8830,

        // declarations
        CompilationUnit = 8840,
        GlobalStatement = 8841,
        NamespaceDeclaration = 8842,
        UsingDirective = 8843,
        ExternAliasDirective = 8844,

        // attributes
        AttributeList = 8847,
        AttributeTargetSpecifier = 8848,
        Attribute = 8849,
        AttributeArgumentList = 8850,
        AttributeArgument = 8851,
        NameEquals = 8852,

        // type declarations
        ClassDeclaration = 8855,
        StructDeclaration = 8856,
        InterfaceDeclaration = 8857,
        EnumDeclaration = 8858,
        DelegateDeclaration = 8859,

        BaseList = 8864,
        SimpleBaseType = 8865,
        TypeParameterConstraintClause = 8866,
        ConstructorConstraint = 8867,
        ClassConstraint = 8868,
        StructConstraint = 8869,
        TypeConstraint = 8870,
        ExplicitInterfaceSpecifier = 8871,
        EnumMemberDeclaration = 8872,
        FieldDeclaration = 8873,
        EventFieldDeclaration = 8874,
        MethodDeclaration = 8875,
        OperatorDeclaration = 8876,
        ConversionOperatorDeclaration = 8877,
        ConstructorDeclaration = 8878,

        BaseConstructorInitializer = 8889,
        ThisConstructorInitializer = 8890,
        DestructorDeclaration = 8891,
        PropertyDeclaration = 8892,
        EventDeclaration = 8893,
        IndexerDeclaration = 8894,
        AccessorList = 8895,
        GetAccessorDeclaration = 8896,
        SetAccessorDeclaration = 8897,
        AddAccessorDeclaration = 8898,
        RemoveAccessorDeclaration = 8899,
        UnknownAccessorDeclaration = 8900,
        ParameterList = 8906,
        BracketedParameterList = 8907,
        Parameter = 8908,
        TypeParameterList = 8909,
        TypeParameter = 8910,
        IncompleteMember = 8916,
        ArrowExpressionClause = 8917,
        Interpolation = 8918, // part of an interpolated string
        InterpolatedStringText = 8919,
        InterpolationAlignmentClause = 8920,
        InterpolationFormatClause = 8921,

        ShebangDirectiveTrivia = 8922,
        LoadDirectiveTrivia = 8923,
        // Changes after C# 6

        // tuples
        TupleType = 8924,
        TupleElement = 8925,
        TupleExpression = 8926,
        SingleVariableDesignation = 8927,
        ParenthesizedVariableDesignation = 8928,
        ForEachVariableStatement = 8929,

        // patterns (for pattern-matching)
        DeclarationPattern = 9000,
        ConstantPattern = 9002,
        CasePatternSwitchLabel = 9009,
        WhenClause = 9013,
        DiscardDesignation = 9014,

        // added along with recursive patterns
        RecursivePattern = 9020,
        PropertyPatternClause = 9021,
        Subpattern = 9022,
        PositionalPatternClause = 9023,
        DiscardPattern = 9024,
        SwitchExpression = 9025,
        SwitchExpressionArm = 9026,
        VarPattern = 9027,

        // new patterns added in C# 9.0
        ParenthesizedPattern = 9028,
        RelationalPattern = 9029,
        TypePattern = 9030,
        OrPattern = 9031,
        AndPattern = 9032,
        NotPattern = 9033,

        // Kinds between 9000 and 9039 are "reserved" for pattern matching.

        DeclarationExpression = 9040,
        RefExpression = 9050,
        RefType = 9051,
        ThrowExpression = 9052,
        ImplicitStackAllocArrayCreationExpression = 9053,
        SuppressNullableWarningExpression = 9054,
        NullableDirectiveTrivia = 9055,
        FunctionPointerType = 9056,

        InitAccessorDeclaration = 9060,

        WithExpression = 9061,
        WithInitializerExpression = 9062,
        RecordDeclaration = 9063
    }
}
