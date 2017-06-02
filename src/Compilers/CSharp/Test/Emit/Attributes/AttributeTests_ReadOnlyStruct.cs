﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Metadata.PE;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.CSharp.UnitTests;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    [CompilerTrait(CompilerFeature.ReadOnlyReferences)]
    public class ReadOnlyStruct : CSharpTestBase
    {
        [Fact]
        public void IsReadOnlyIsWrittenToMetadata_SameAssembly()
        {
            var text = @"
namespace System.Runtime.CompilerServices
{
    public class IsReadOnlyAttribute : System.Attribute { }
}
class Test
{
    public readonly struct S1 {}
}
";

            CompileAndVerify(text, verify: false, symbolValidator: module =>
            {
                var type = module.ContainingAssembly.GetTypeByMetadataName("Test").GetTypeMember("S1");
                Assert.True(type.IsReadOnly);

                AssertReferencedIsReadOnlyAttribute(Accessibility.Public, type.GetAttributes(), module.ContainingAssembly.Name);
            });
        }

        [Fact]
        public void IsReadOnlyIsWrittenToMetadata_NeedsToBeGenerated()
        {
            var text = @"
readonly struct S1{}
";

            CompileAndVerify(text, verify: false, symbolValidator: module =>
            {
                var type = module.ContainingAssembly.GetTypeByMetadataName("S1");
                Assert.True(type.IsReadOnly);

                AssertReferencedIsReadOnlyAttribute(Accessibility.Internal, type.GetAttributes(), module.ContainingAssembly.Name);
            });
        }

        [Fact]
        public void IsReadOnlyIsWrittenToMetadata_NeedsToBeGeneratedNested()
        {
            var text = @"
class Test
{
    public readonly struct S1 {}
}
";

            CompileAndVerify(text, verify: false, symbolValidator: module =>
            {
                var type = module.ContainingAssembly.GetTypeByMetadataName("Test").GetTypeMember("S1");
                Assert.True(type.IsReadOnly);

                AssertReferencedIsReadOnlyAttribute(Accessibility.Internal, type.GetAttributes(), module.ContainingAssembly.Name);
            });
        }

        [Fact]
        public void IsReadOnlyIsWrittenToMetadata_NeedsToBeGeneratedGeneric()
        {
            var text = @"
class Test
{
    public readonly struct S1<T> {}
}
";

            CompileAndVerify(text, verify: false, symbolValidator: module =>
            {
                var type = module.ContainingAssembly.GetTypeByMetadataName("Test+S1`1");
                Assert.True(type.IsReadOnly);

                AssertReferencedIsReadOnlyAttribute(Accessibility.Internal, type.GetAttributes(), module.ContainingAssembly.Name);
            });
        }

        [Fact]
        public void IsReadOnlyIsWrittenToMetadata_NeedsToBeGeneratedNestedInGeneric()
        {
            var text = @"
class Test<T>
{
    public readonly struct S1 {}
}
";

            CompileAndVerify(text, verify: false, symbolValidator: module =>
            {
                var type = module.ContainingAssembly.GetTypeByMetadataName("Test`1").GetTypeMember("S1");
                Assert.True(type.IsReadOnly);

                AssertReferencedIsReadOnlyAttribute(Accessibility.Internal, type.GetAttributes(), module.ContainingAssembly.Name);
            });
        }

        [Fact]
        public void IsReadOnlyIsWrittenToMetadata_DifferentAssembly()
        {
            var codeA = @"
namespace System.Runtime.CompilerServices
{
    public class IsReadOnlyAttribute : System.Attribute { }
}";

            var referenceA = CreateStandardCompilation(codeA).VerifyDiagnostics().ToMetadataReference();

            var codeB = @"
class Test
{
     public readonly struct S1 {}
}
";

            CompileAndVerify(codeB, verify: false, additionalRefs: new[] { referenceA }, symbolValidator: module =>
            {
                var type = module.ContainingAssembly.GetTypeByMetadataName("Test").GetTypeMember("S1");
                Assert.True(type.IsReadOnly);

                AssertReferencedIsReadOnlyAttribute(Accessibility.Public, type.GetAttributes(), referenceA.Compilation.AssemblyName);
                AssertNoIsReadOnlyAttributeExists(module.ContainingAssembly);
            });
        }

        [Fact]
        public void IsReadOnlyAttributeIsDisallowedEverywhereInSource_Delegates()
        {
            var codeA = @"
namespace System.Runtime.CompilerServices
{
    public class IsReadOnlyAttribute : System.Attribute { }
}";

            var referenceA = CreateStandardCompilation(codeA).VerifyDiagnostics().ToMetadataReference();

            var codeB = @"
using System.Runtime.CompilerServices;

[IsReadOnly]
public delegate ref readonly int D([IsReadOnly]ref readonly int x);
";

            CreateStandardCompilation(codeB, references: new[] { referenceA }).VerifyDiagnostics(
                // (4,2): error CS8412: Do not use 'System.Runtime.CompilerServices.IsReadOnlyAttribute'. This is reserved for compiler usage.
                // [IsReadOnly]
                Diagnostic(ErrorCode.ERR_ExplicitReservedAttr, "IsReadOnly").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute").WithLocation(4, 2),
                // (5,37): error CS8412: Do not use 'System.Runtime.CompilerServices.IsReadOnlyAttribute'. This is reserved for compiler usage.
                // public delegate ref readonly int D([IsReadOnly]ref readonly int x);
                Diagnostic(ErrorCode.ERR_ExplicitReservedAttr, "IsReadOnly").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute").WithLocation(5, 37));
        }

        [Fact]
        public void IsReadOnlyAttributeIsDisallowedEverywhereInSource_Types()
        {
            var codeA = @"
namespace System.Runtime.CompilerServices
{
    public class IsReadOnlyAttribute : System.Attribute { }
}";

            var referenceA = CreateStandardCompilation(codeA).VerifyDiagnostics().ToMetadataReference();

            var codeB = @"
using System.Runtime.CompilerServices;

[IsReadOnly]
public class Test
{
}
";

            CreateStandardCompilation(codeB, references: new[] { referenceA }).VerifyDiagnostics(
                // (4,2): error CS8412: Do not use 'System.Runtime.CompilerServices.IsReadOnlyAttribute'. This is reserved for compiler usage.
                // [IsReadOnly]
                Diagnostic(ErrorCode.ERR_ExplicitReservedAttr, "IsReadOnly").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute").WithLocation(4, 2));
        }

        [Fact]
        public void IsReadOnlyAttributeIsDisallowedEverywhereInSource_Fields()
        {
            var codeA = @"
namespace System.Runtime.CompilerServices
{
    public class IsReadOnlyAttribute : System.Attribute { }
}";

            var referenceA = CreateStandardCompilation(codeA).VerifyDiagnostics().ToMetadataReference();

            var codeB = @"
using System.Runtime.CompilerServices;

public class Test
{
    [IsReadOnly]
    private int x = 0;

    public int X => x;
}
";

            CreateStandardCompilation(codeB, references: new[] { referenceA }).VerifyDiagnostics(
                // (6,6): error CS8412: Do not use 'System.Runtime.CompilerServices.IsReadOnlyAttribute'. This is reserved for compiler usage.
                //     [IsReadOnly]
                Diagnostic(ErrorCode.ERR_ExplicitReservedAttr, "IsReadOnly").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute").WithLocation(6, 6));
        }

        [Fact]
        public void IsReadOnlyAttributeIsDisallowedEverywhereInSource_Properties()
        {
            var codeA = @"
namespace System.Runtime.CompilerServices
{
    public class IsReadOnlyAttribute : System.Attribute { }
}";

            var referenceA = CreateStandardCompilation(codeA).VerifyDiagnostics().ToMetadataReference();

            var codeB = @"
using System.Runtime.CompilerServices;

public class Test
{
    private int x = 0;

    [IsReadOnly]
    public ref readonly int Property => ref x;
}
";

            CreateStandardCompilation(codeB, references: new[] { referenceA }).VerifyDiagnostics(
                // (8,6): error CS8412: Do not use 'System.Runtime.CompilerServices.IsReadOnlyAttribute'. This is reserved for compiler usage.
                //     [IsReadOnly]
                Diagnostic(ErrorCode.ERR_ExplicitReservedAttr, "IsReadOnly").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute").WithLocation(8, 6));
        }

        [Fact]
        public void IsReadOnlyAttributeIsDisallowedEverywhereInSource_Methods()
        {
            var codeA = @"
namespace System.Runtime.CompilerServices
{
    public class IsReadOnlyAttribute : System.Attribute { }
}";

            var referenceA = CreateStandardCompilation(codeA).VerifyDiagnostics().ToMetadataReference();

            var codeB = @"
using System.Runtime.CompilerServices;

public class Test
{
    [IsReadOnly]
    [return: IsReadOnly]
    public ref readonly int Method([IsReadOnly]ref readonly int x)
    {
        return ref x;
    }
}
";

            CreateStandardCompilation(codeB, references: new[] { referenceA }).VerifyDiagnostics(
                // (6,6): error CS8412: Do not use 'System.Runtime.CompilerServices.IsReadOnlyAttribute'. This is reserved for compiler usage.
                //     [IsReadOnly]
                Diagnostic(ErrorCode.ERR_ExplicitReservedAttr, "IsReadOnly").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute").WithLocation(6, 6),
                // (7,14): error CS8412: Do not use 'System.Runtime.CompilerServices.IsReadOnlyAttribute'. This is reserved for compiler usage.
                //     [return: IsReadOnly]
                Diagnostic(ErrorCode.ERR_ExplicitReservedAttr, "IsReadOnly").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute").WithLocation(7, 14),
                // (8,37): error CS8412: Do not use 'System.Runtime.CompilerServices.IsReadOnlyAttribute'. This is reserved for compiler usage.
                //     public ref readonly int Method([IsReadOnly]ref readonly int x)
                Diagnostic(ErrorCode.ERR_ExplicitReservedAttr, "IsReadOnly").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute").WithLocation(8, 37));
        }

        [Fact]
        public void IsReadOnlyAttributeIsDisallowedEverywhereInSource_Indexers()
        {
            var codeA = @"
namespace System.Runtime.CompilerServices
{
    public class IsReadOnlyAttribute : System.Attribute { }
}";

            var referenceA = CreateStandardCompilation(codeA).VerifyDiagnostics().ToMetadataReference();

            var codeB = @"
using System.Runtime.CompilerServices;

public class Test
{
    [IsReadOnly]
    public ref readonly int this[[IsReadOnly]ref readonly int x] { get { return ref x; } }
}
";

            CreateStandardCompilation(codeB, references: new[] { referenceA }).VerifyDiagnostics(
                // (6,6): error CS8412: Do not use 'System.Runtime.CompilerServices.IsReadOnlyAttribute'. This is reserved for compiler usage.
                //     [IsReadOnly]
                Diagnostic(ErrorCode.ERR_ExplicitReservedAttr, "IsReadOnly").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute").WithLocation(6, 6),
                // (7,35): error CS8412: Do not use 'System.Runtime.CompilerServices.IsReadOnlyAttribute'. This is reserved for compiler usage.
                //     public ref readonly int this[[IsReadOnly]ref readonly int x] { get { return ref x; } }
                Diagnostic(ErrorCode.ERR_ExplicitReservedAttr, "IsReadOnly").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute").WithLocation(7, 35));
        }

        [Fact]
        public void UserReferencingIsReadOnlyAttributeShouldResultInAnError()
        {
            var code = @"
[IsReadOnly]
public class Test
{
	ref struct S1{}
}";

            CreateStandardCompilation(code).VerifyDiagnostics(
                // (2,2): error CS0246: The type or namespace name 'IsReadOnlyAttribute' could not be found (are you missing a using directive or an assembly reference?)
                // [IsReadOnly]
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "IsReadOnly").WithArguments("IsReadOnlyAttribute").WithLocation(2, 2),
                // (2,2): error CS0246: The type or namespace name 'IsReadOnly' could not be found (are you missing a using directive or an assembly reference?)
                // [IsReadOnly]
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "IsReadOnly").WithArguments("IsReadOnly").WithLocation(2, 2)
                );
        }

        [Fact]
        public void TypeReferencingAnotherTypeThatUsesAPublicIsReadOnlyAttributeFromAThirdNotReferencedAssemblyShouldGenerateItsOwn()
        {
            var options = TestOptions.DebugDll.WithMetadataImportOptions(MetadataImportOptions.All);

            var code1 = CreateStandardCompilation(@"
namespace System.Runtime.CompilerServices
{
    public class IsReadOnlyAttribute : System.Attribute { }
}");

            var code2 = CreateStandardCompilation(@"
public class Test1
{
	public readonly struct S1{}
}", references: new[] { code1.ToMetadataReference() }, options: options);

            CompileAndVerify(code2, verify: false, symbolValidator: module =>
            {
                // IsReadOnly is not generated in assembly
                var isReadOnlyAttributeName = WellKnownTypes.GetMetadataName(WellKnownType.System_Runtime_CompilerServices_IsReadOnlyAttribute);
                Assert.Null(module.ContainingAssembly.GetTypeByMetadataName(isReadOnlyAttributeName));
            });

            var code3 = CreateStandardCompilation(@"
public class Test2
{
	public readonly struct S1{}
}", references: new[] { code2.ToMetadataReference() }, options: options);

            CompileAndVerify(code3, symbolValidator: module =>
            {
                // IsReadOnly is generated in assembly
                AssertGeneratedEmbeddedAttribute(module.ContainingAssembly, AttributeDescription.CodeAnalysisEmbeddedAttribute.FullName);
                AssertGeneratedEmbeddedAttribute(module.ContainingAssembly, AttributeDescription.IsReadOnlyAttribute.FullName);
            });
        }

        [Fact]
        public void BuildingAModuleRequiresIsReadOnlyAttributeToBeThere_Missing_SourceMethod()
        {
            var code = @"
public readonly struct S1{}
";

            CreateStandardCompilation(code, options: TestOptions.ReleaseModule).VerifyDiagnostics(
                // (2,19): error CS0518: Predefined type 'System.Runtime.CompilerServices.IsReadOnlyAttribute' is not defined or imported
                // public readonly struct S1{}
                Diagnostic(ErrorCode.ERR_PredefinedTypeNotFound, "S1").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute").WithLocation(2, 24)
                );
        }

        [Fact]
        public void BuildingAModuleRequiresIsReadOnlyAttributeToBeThere_Missing_SourceMethod_MultipleLocations()
        {
            var code = @"
public class Test
{
    public readonly struct S1{}
    public readonly struct S2{}
}";

            CreateStandardCompilation(code, options: TestOptions.ReleaseModule).VerifyDiagnostics(
                // (5,23): error CS0518: Predefined type 'System.Runtime.CompilerServices.IsReadOnlyAttribute' is not defined or imported
                //     public readonly struct S2{}
                Diagnostic(ErrorCode.ERR_PredefinedTypeNotFound, "S2").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute").WithLocation(5, 28),
                // (4,23): error CS0518: Predefined type 'System.Runtime.CompilerServices.IsReadOnlyAttribute' is not defined or imported
                //     public readonly struct S1{}
                Diagnostic(ErrorCode.ERR_PredefinedTypeNotFound, "S1").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute").WithLocation(4, 28)
                );
        }

        [Fact]
        public void BuildingAModuleRequiresIsReadOnlyAttributeToBeThere_InAReference()
        {
            var reference = CreateStandardCompilation(@"
namespace System.Runtime.CompilerServices
{
    public class IsReadOnlyAttribute : System.Attribute { }
}").ToMetadataReference();

            var code = @"
public class Test
{
    public readonly struct S1{}
}";

            CompileAndVerify(code, verify: false, additionalRefs: new[] { reference }, options: TestOptions.ReleaseModule, symbolValidator: module =>
            {
                var type = module.ContainingAssembly.GetTypeByMetadataName("Test").GetTypeMember("S1");
                Assert.True(type.IsReadOnly);

                AssertReferencedIsReadOnlyAttribute(Accessibility.Public, type.GetAttributes(), reference.Display);
                AssertNoIsReadOnlyAttributeExists(module.ContainingAssembly);
            });
        }

        [Fact]
        public void ReferencingAnEmbeddedIsReadOnlyAttributeDoesNotUseIt_InternalsVisible()
        {
            var options = TestOptions.DebugDll.WithMetadataImportOptions(MetadataImportOptions.All);

            var code1 = @"
[assembly:System.Runtime.CompilerServices.InternalsVisibleToAttribute(""Assembly2"")]
public class Test1
{
	public readonly struct S1{}
}";

            var comp1 = CompileAndVerify(code1, options: options, verify: false, symbolValidator: module =>
            {
                AssertGeneratedEmbeddedAttribute(module.ContainingAssembly, AttributeDescription.CodeAnalysisEmbeddedAttribute.FullName);
                AssertGeneratedEmbeddedAttribute(module.ContainingAssembly, AttributeDescription.IsReadOnlyAttribute.FullName);
            });

            var code2 = @"
public class Test2
{
	public readonly struct S1{}
}";

            CompileAndVerify(code2, options: options.WithModuleName("Assembly2"), additionalRefs: new[] { comp1.Compilation.ToMetadataReference() }, symbolValidator: module =>
            {
                AssertGeneratedEmbeddedAttribute(module.ContainingAssembly, AttributeDescription.CodeAnalysisEmbeddedAttribute.FullName);
                AssertGeneratedEmbeddedAttribute(module.ContainingAssembly, AttributeDescription.IsReadOnlyAttribute.FullName);
            });
        }

        [Fact]
        public void IfIsReadOnlyAttributeIsDefinedThenEmbeddedIsNotGenerated()
        {
            var text = @"
namespace System.Runtime.CompilerServices
{
    public class IsReadOnlyAttribute : System.Attribute { }
}
class Test
{
    public readonly struct S1{}
}
";

            CompileAndVerify(text, verify: false, symbolValidator: module =>
            {
                Assert.Null(module.ContainingAssembly.GetTypeByMetadataName(AttributeDescription.CodeAnalysisEmbeddedAttribute.FullName));
            });
        }

        [Fact]
        public void IsReadOnlyAttributeExistsWithWrongConstructorSignature_NetModule()
        {
            var text = @"
namespace System.Runtime.CompilerServices
{
    public class IsReadOnlyAttribute : System.Attribute
    {
        public IsReadOnlyAttribute(int p) { }
    }
}
class Test
{
    public readonly struct S1{}
}";

            CreateStandardCompilation(text, options: TestOptions.ReleaseModule).VerifyDiagnostics(
                // (11,23): error CS0656: Missing compiler required member 'System.Runtime.CompilerServices.IsReadOnlyAttribute..ctor'
                //     public readonly struct S1{}
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "S1").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute", ".ctor").WithLocation(11, 28)
                );
        }

        [Fact]
        public void IsReadOnlyAttributeExistsWithWrongConstructorSignature_Assembly()
        {
            var text = @"
namespace System.Runtime.CompilerServices
{
    public class IsReadOnlyAttribute : System.Attribute
    {
        public IsReadOnlyAttribute(int p) { }
    }
}
class Test
{
   public readonly struct S1{}
}";

            CreateStandardCompilation(text).VerifyEmitDiagnostics(
                // (11,22): error CS0656: Missing compiler required member 'System.Runtime.CompilerServices.IsReadOnlyAttribute..ctor'
                //    public readonly struct S1{}
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "S1").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute", ".ctor").WithLocation(11, 27)
                );
        }

        [Fact]
        public void IsReadOnlyAttributeExistsWithWrongConstructorSignature_PrivateConstructor()
        {
            var text = @"
namespace System.Runtime.CompilerServices
{
    public class IsReadOnlyAttribute : System.Attribute
    {
        private IsReadOnlyAttribute() { }
    }
}
class Test
{
    public readonly struct S1{}
}";

            CreateStandardCompilation(text).VerifyEmitDiagnostics(
                // (11,23): error CS0656: Missing compiler required member 'System.Runtime.CompilerServices.IsReadOnlyAttribute..ctor'
                //     public readonly struct S1{}
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "S1").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute", ".ctor").WithLocation(11, 28)
                );
        }

        [Fact]
        public void IsReadOnlyAttributesInNoPia()
        {
            var comAssembly = CreateStandardCompilation(@"
using System;
using System.Runtime.InteropServices;
[assembly: ImportedFromTypeLib(""test.dll"")]
[assembly: Guid(""5171B851-73E2-4168-9846-E5CF49A2D8B5"")]
[ComImport()]
[Guid(""5171B851-73E2-4168-9846-E5CF49A2D8B5"")]
public interface Test
{
    S1 Property { get; }
    S1 Method(S1 x);
}

public readonly struct S1{}
");

            CompileAndVerify(comAssembly, symbolValidator: module =>
            {
                var type = module.ContainingAssembly.GetTypeByMetadataName("Test");

                var property = type.GetMember<PEPropertySymbol>("Property");
                Assert.NotNull(property);
                AssertReferencedIsReadOnlyAttribute(Accessibility.Internal, property.Type.GetAttributes(), module.ContainingAssembly.Name);
            });

            var code = @"
class User
{
    public void M(Test p)
    {
        p.Method(p.Property);
    }
}";


            var options = TestOptions.DebugDll.WithMetadataImportOptions(MetadataImportOptions.All);

            var compilation_CompilationReference = CreateStandardCompilation(code, options: options, references: new[] { comAssembly.ToMetadataReference(embedInteropTypes: true) });
            CompileAndVerify(compilation_CompilationReference, symbolValidator: symbolValidator);

            var compilation_BinaryReference = CreateStandardCompilation(code, options: options, references: new[] { comAssembly.EmitToImageReference(embedInteropTypes: true) });
            CompileAndVerify(compilation_BinaryReference, symbolValidator: symbolValidator);

            void symbolValidator(ModuleSymbol module)
            {
                // No attribute is copied
                AssertNoIsReadOnlyAttributeExists(module.ContainingAssembly);

                var type = module.ContainingAssembly.GetTypeByMetadataName("Test");

                var property = type.GetMember<PEPropertySymbol>("Property");
                Assert.NotNull(property);
                AssertNotReferencedIsReadOnlyAttribute(property.Type.GetAttributes());
            }
        }

        [Fact]
        public void MissingRequiredConstructorWillReportErrorsOnApproriateSyntax_IsReadOnly()
        {
            var text = @"
namespace System.Runtime.CompilerServices
{
    public class IsReadOnlyAttribute : System.Attribute
    {
        public IsReadOnlyAttribute(int p) { }
    }
}
public class Test
{
    public readonly struct S1{}
}";

            CreateStandardCompilation(text).VerifyEmitDiagnostics(
                // (11,23): error CS0656: Missing compiler required member 'System.Runtime.CompilerServices.IsReadOnlyAttribute..ctor'
                //     public readonly struct S1{}
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "S1").WithArguments("System.Runtime.CompilerServices.IsReadOnlyAttribute", ".ctor").WithLocation(11, 28)
                );
        }

        private static void AssertReferencedIsReadOnlyAttribute(Accessibility accessibility, ImmutableArray<CSharpAttributeData> attributes, string assemblyName)
        {
            var attributeType = attributes.Single().AttributeClass;
            Assert.Equal("System.Runtime.CompilerServices.IsReadOnlyAttribute", attributeType.ToDisplayString());
            Assert.Equal(assemblyName, attributeType.ContainingAssembly.Name);
            Assert.Equal(accessibility, attributeType.DeclaredAccessibility);
        }

        private static void AssertNotReferencedIsReadOnlyAttribute(ImmutableArray<CSharpAttributeData> attributes)
        {
            foreach(var attr in attributes)
            {
                Assert.NotEqual("IsReadOnlyAttribute", attr.AttributeClass.Name);
            }
        }

        private static void AssertNoIsReadOnlyAttributeExists(AssemblySymbol assembly)
        {
            var isReadOnlyAttributeTypeName = WellKnownTypes.GetMetadataName(WellKnownType.System_Runtime_CompilerServices_IsReadOnlyAttribute);
            Assert.Null(assembly.GetTypeByMetadataName(isReadOnlyAttributeTypeName));
        }

        private static void AssertGeneratedEmbeddedAttribute(AssemblySymbol assembly, string expectedTypeName)
        {
            var typeSymbol = assembly.GetTypeByMetadataName(expectedTypeName);
            Assert.NotNull(typeSymbol);
            Assert.Equal(Accessibility.Internal, typeSymbol.DeclaredAccessibility);

            var attributes = typeSymbol.GetAttributes().OrderBy(attribute => attribute.AttributeClass.Name).ToArray();
            Assert.Equal(2, attributes.Length);

            Assert.Equal(WellKnownTypes.GetMetadataName(WellKnownType.System_Runtime_CompilerServices_CompilerGeneratedAttribute), attributes[0].AttributeClass.ToDisplayString());
            Assert.Equal(AttributeDescription.CodeAnalysisEmbeddedAttribute.FullName, attributes[1].AttributeClass.ToDisplayString());
        }
    }
}
