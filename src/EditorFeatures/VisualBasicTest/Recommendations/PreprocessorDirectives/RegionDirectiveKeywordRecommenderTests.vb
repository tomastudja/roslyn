﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Recommendations.PreprocessorDirectives
    <Trait(Traits.Feature, Traits.Features.KeywordRecommending)>
    Public Class RegionDirectiveKeywordRecommenderTests
        Inherits RecommenderTests

        <Fact>
        Public Sub HashRegionInFileTest()
            VerifyRecommendationsContain(<File>|</File>, "#Region")
        End Sub

        <Fact>
        Public Sub HashRegionInLambdaTest()
            VerifyRecommendationsContain(<ClassDeclaration>Dim x = Function()
|
End Function</ClassDeclaration>, "#Region")
        End Sub

        <Fact>
        Public Sub NotInEnumBlockMemberDeclarationTest()
            VerifyRecommendationsMissing(<File>
                                             Enum goo
                                                |
                                            End enum
                                         </File>, "#Region")
        End Sub

        <Fact>
        Public Sub NotAfterHashEndTest()
            VerifyRecommendationsMissing(<File>
#Region "goo"

#End |</File>, "#Region")
        End Sub

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/6389")>
        Public Sub NotAfterHashRegionTest()
            VerifyRecommendationsMissing(<File>
                                         Class C

                                             #Region |

                                         End Class
                                         </File>, "#Region")
        End Sub
    End Class
End Namespace
