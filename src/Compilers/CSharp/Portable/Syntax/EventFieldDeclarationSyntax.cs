﻿
namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    public sealed partial class EventFieldDeclarationSyntax : BaseFieldDeclarationSyntax
    {
        public EventFieldDeclarationSyntax AddDeclarationVariables(params VariableDeclaratorSyntax[] items)
        {
            return this.WithDeclaration(this.Declaration.WithVariables(this.Declaration.Variables.AddRange(items)));
        }
    }
}