﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Roslyn.Test.Utilities

Namespace Microsoft.VisualStudio.LanguageServices.UnitTests.CodeModel.MethodXML
    Partial Public Class MethodXMLTests

        <ConditionalFact(GetType(x86)), Trait(Traits.Feature, Traits.Features.CodeModelMethodXml)>
        Public Sub VBStatements_AddHandler1()
            Dim definition =
<Workspace>
    <Project Language="Visual Basic" CommonReferences="true" AssemblyName="Test">
        <CompilationOptions RootNamespace="N"/>
        <Document>
Imports System

Class C
    Event E As EventHandler

    Sub Handler(sender As Object, e As EventArgs)
    End Sub

    $$Sub M()
        AddHandler E, AddressOf Handler
    End Sub
End Class
        </Document>
    </Project>
</Workspace>

            Dim expected =
<Block>
    <ExpressionStatement line="10">
        <Expression>
            <MethodCall>
                <Expression>
                    <NameRef variablekind="method" name="add_E">
                        <Expression>
                            <ThisReference/>
                        </Expression>
                    </NameRef>
                </Expression>
                <Type implicit="yes">N.C, Test, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null</Type>
                <Argument>
                    <Expression>
                        <NewDelegate name="Handler">
                            <Type implicit="yes">System.EventHandler, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</Type>
                            <Expression>
                                <ThisReference/>
                            </Expression>
                            <Type implicit="yes">N.C, Test, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null</Type>
                        </NewDelegate>
                    </Expression>
                </Argument>
            </MethodCall>
        </Expression>
    </ExpressionStatement>
</Block>

            Test(definition, expected)
        End Sub

        <ConditionalFact(GetType(x86)), Trait(Traits.Feature, Traits.Features.CodeModelMethodXml)>
        Public Sub VBStatements_AddHandler2()
            Dim definition =
<Workspace>
    <Project Language="Visual Basic" CommonReferences="true" AssemblyName="Test">
        <CompilationOptions RootNamespace="N"/>
        <Document>
Imports System

Class B
    Event E As EventHandler
End Class

Class C
    Dim b As New B

    Sub Handler(sender As Object, e As EventArgs)
    End Sub

    $$Sub M()
        AddHandler b.E, AddressOf Handler
    End Sub
End Class
        </Document>
    </Project>
</Workspace>

            Dim expected =
<Block>
    <ExpressionStatement line="14">
        <Expression>
            <MethodCall>
                <Expression>
                    <NameRef variablekind="method" name="add_E">
                        <Expression>
                            <NameRef variablekind="field" name="b" fullname="N.C.b">
                                <Expression>
                                    <ThisReference/>
                                </Expression>
                            </NameRef>
                        </Expression>
                    </NameRef>
                </Expression>
                <Type implicit="yes">N.B, Test, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null</Type>
                <Argument>
                    <Expression>
                        <NewDelegate name="Handler">
                            <Type implicit="yes">System.EventHandler, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</Type>
                            <Expression>
                                <ThisReference/>
                            </Expression>
                            <Type implicit="yes">N.C, Test, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null</Type>
                        </NewDelegate>
                    </Expression>
                </Argument>
            </MethodCall>
        </Expression>
    </ExpressionStatement>
</Block>

            Test(definition, expected)
        End Sub

        <ConditionalFact(GetType(x86)), Trait(Traits.Feature, Traits.Features.CodeModelMethodXml)>
        Public Sub VBStatements_AddHandler3()
            Dim definition =
<Workspace>
    <Project Language="Visual Basic" CommonReferences="true" AssemblyName="Test">
        <CompilationOptions RootNamespace="N"/>
        <Document>
Imports System

Class B
    Event E As EventHandler

    Sub Handler(sender As Object, e As EventArgs)
    End Sub
End Class

Class C
    Dim b As New B

    $$Sub M()
        AddHandler b.E, AddressOf b.Handler
    End Sub
End Class
        </Document>
    </Project>
</Workspace>

            Dim expected =
<Block>
    <ExpressionStatement line="14">
        <Expression>
            <MethodCall>
                <Expression>
                    <NameRef variablekind="method" name="add_E">
                        <Expression>
                            <NameRef variablekind="field" name="b" fullname="N.C.b">
                                <Expression>
                                    <ThisReference/>
                                </Expression>
                            </NameRef>
                        </Expression>
                    </NameRef>
                </Expression>
                <Type implicit="yes">N.B, Test, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null</Type>
                <Argument>
                    <Expression>
                        <NewDelegate name="Handler">
                            <Type implicit="yes">System.EventHandler, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</Type>
                            <Expression>
                                <NameRef variablekind="field" name="b" fullname="N.C.b">
                                    <Expression>
                                        <ThisReference/>
                                    </Expression>
                                </NameRef>
                            </Expression>
                            <Type implicit="yes">N.B, Test, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null</Type>
                        </NewDelegate>
                    </Expression>
                </Argument>
            </MethodCall>
        </Expression>
    </ExpressionStatement>
</Block>

            Test(definition, expected)
        End Sub

    End Class
End Namespace