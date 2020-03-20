﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Text;
using Xunit;

#nullable enable
namespace Microsoft.CodeAnalysis.CSharp.Semantic.UnitTests.SourceGeneration
{
    public class GeneratorDriverTests
         : CSharpTestBase
    {
        [Fact]
        public void Running_With_No_Changes_Is_NoOp()
        {
            var source = @"
class C { }
";
            var parseOptions = TestOptions.Regular;
            Compilation compilation = CreateCompilation(source, options: TestOptions.DebugDll, parseOptions: parseOptions);
            compilation.VerifyDiagnostics();

            Assert.Single(compilation.SyntaxTrees);

            GeneratorDriver driver = new CSharpGeneratorDriver(compilation, parseOptions, ImmutableArray<ISourceGenerator>.Empty, ImmutableArray<AdditionalText>.Empty);
            driver.RunFullGeneration(compilation, out var outputCompilation);

            Assert.Single(outputCompilation.SyntaxTrees);
            Assert.Equal(compilation, outputCompilation);
        }

        [Fact]
        public void Generator_Is_Intialized_Before_Running()
        {
            var source = @"
class C { }
";
            var parseOptions = TestOptions.Regular;
            Compilation compilation = CreateCompilation(source, options: TestOptions.DebugDll, parseOptions: parseOptions);
            compilation.VerifyDiagnostics();

            Assert.Single(compilation.SyntaxTrees);

            int initCount = 0, executeCount = 0;
            var generator = new CallbackGenerator((ic) => initCount++, (sgc) => executeCount++);

            GeneratorDriver driver = new CSharpGeneratorDriver(compilation, parseOptions, ImmutableArray.Create<ISourceGenerator>(generator), ImmutableArray<AdditionalText>.Empty);
            driver.RunFullGeneration(compilation, out var outputCompilation);

            Assert.Equal(1, initCount);
            Assert.Equal(1, executeCount);
        }

        [Fact]
        public void Generator_Is_Not_Initialized_If_Not_Run()
        {
            var source = @"
class C { }
";
            var parseOptions = TestOptions.Regular;
            Compilation compilation = CreateCompilation(source, options: TestOptions.DebugDll, parseOptions: parseOptions);
            compilation.VerifyDiagnostics();

            Assert.Single(compilation.SyntaxTrees);

            int initCount = 0, executeCount = 0;
            var generator = new CallbackGenerator((ic) => initCount++, (sgc) => executeCount++);

            GeneratorDriver driver = new CSharpGeneratorDriver(compilation, parseOptions, ImmutableArray.Create<ISourceGenerator>(generator), ImmutableArray<AdditionalText>.Empty);

            Assert.Equal(0, initCount);
            Assert.Equal(0, executeCount);
        }

        [Fact]
        public void Generator_Is_Only_Initialized_Once()
        {
            var source = @"
class C { }
";
            var parseOptions = TestOptions.Regular;
            Compilation compilation = CreateCompilation(source, options: TestOptions.DebugDll, parseOptions: parseOptions);
            compilation.VerifyDiagnostics();

            Assert.Single(compilation.SyntaxTrees);

            int initCount = 0, executeCount = 0;
            var generator = new CallbackGenerator((ic) => initCount++, (sgc) => executeCount++, sourceOpt: "public class C { }");

            GeneratorDriver driver = new CSharpGeneratorDriver(compilation, parseOptions, ImmutableArray.Create<ISourceGenerator>(generator), ImmutableArray<AdditionalText>.Empty);
            driver = driver.RunFullGeneration(compilation, out var outputCompilation);
            driver = driver.RunFullGeneration(outputCompilation, out outputCompilation);
            driver.RunFullGeneration(outputCompilation, out outputCompilation);

            Assert.Equal(1, initCount);
            Assert.Equal(3, executeCount);
        }

        [Fact]
        public void Single_File_Is_Added()
        {
            var source = @"
class C { }
";

            var generatorSource = @"
class GeneratedClass { }
";

            var parseOptions = TestOptions.Regular;
            Compilation compilation = CreateCompilation(source, options: TestOptions.DebugDll, parseOptions: parseOptions);
            compilation.VerifyDiagnostics();

            Assert.Single(compilation.SyntaxTrees);

            SingleFileTestGenerator testGenerator = new SingleFileTestGenerator(generatorSource);

            GeneratorDriver driver = new CSharpGeneratorDriver(compilation, parseOptions, ImmutableArray.Create<ISourceGenerator>(testGenerator), ImmutableArray<AdditionalText>.Empty);
            driver.RunFullGeneration(compilation, out var outputCompilation);

            Assert.Equal(2, outputCompilation.SyntaxTrees.Count());
            Assert.NotEqual(compilation, outputCompilation);
        }

        [Fact]
        public void Single_File_Is_Added_OnlyOnce_For_Multiple_Calls()
        {
            var source = @"
class C { }
";

            var generatorSource = @"
class GeneratedClass { }
";

            var parseOptions = TestOptions.Regular;
            Compilation compilation = CreateCompilation(source, options: TestOptions.DebugDll, parseOptions: parseOptions);
            compilation.VerifyDiagnostics();

            Assert.Single(compilation.SyntaxTrees);

            SingleFileTestGenerator testGenerator = new SingleFileTestGenerator(generatorSource);

            GeneratorDriver driver = new CSharpGeneratorDriver(compilation, parseOptions, ImmutableArray.Create<ISourceGenerator>(testGenerator), ImmutableArray<AdditionalText>.Empty);
            driver = driver.RunFullGeneration(compilation, out var outputCompilation1);
            driver = driver.RunFullGeneration(compilation, out var outputCompilation2);
            driver = driver.RunFullGeneration(compilation, out var outputCompilation3);

            Assert.Equal(2, outputCompilation1.SyntaxTrees.Count());
            Assert.Equal(2, outputCompilation2.SyntaxTrees.Count());
            Assert.Equal(2, outputCompilation3.SyntaxTrees.Count());

            Assert.NotEqual(compilation, outputCompilation1);
            Assert.NotEqual(compilation, outputCompilation2);
            Assert.NotEqual(compilation, outputCompilation3);
        }

        [Fact]
        public void TryApply_Edits_Fails_If_FullGeneration_Has_Not_Run()
        {
            var source = @"
class C { }
";

            var parseOptions = TestOptions.Regular;
            Compilation compilation = CreateCompilation(source, options: TestOptions.DebugDll, parseOptions: parseOptions);
            compilation.VerifyDiagnostics();
            Assert.Single(compilation.SyntaxTrees);

            AdditionalFileAddedGenerator testGenerator = new AdditionalFileAddedGenerator() { CanApplyChanges = false };

            GeneratorDriver driver = new CSharpGeneratorDriver(compilation, parseOptions, ImmutableArray.Create<ISourceGenerator>(testGenerator), ImmutableArray<AdditionalText>.Empty);

            // try apply edits should fail if we've not run a full compilation yet
            driver = driver.TryApplyEdits(compilation, out var outputCompilation, out var succeeded);
            Assert.False(succeeded);
            Assert.Equal(compilation, outputCompilation);
        }

        [Fact]
        public void TryApply_Edits_Does_Nothing_When_Nothing_Pending()
        {
            var source = @"
class C { }
";

            var parseOptions = TestOptions.Regular;
            Compilation compilation = CreateCompilation(source, options: TestOptions.DebugDll, parseOptions: parseOptions);
            compilation.VerifyDiagnostics();
            Assert.Single(compilation.SyntaxTrees);

            AdditionalFileAddedGenerator testGenerator = new AdditionalFileAddedGenerator();

            GeneratorDriver driver = new CSharpGeneratorDriver(compilation, parseOptions, ImmutableArray.Create<ISourceGenerator>(testGenerator), ImmutableArray<AdditionalText>.Empty);

            // run an initial generation pass
            driver = driver.RunFullGeneration(compilation, out var outputCompilation);
            Assert.Single(outputCompilation.SyntaxTrees);

            // now try apply edits (which should succeed, but do nothing)
            driver = driver.TryApplyEdits(compilation, out var editedCompilation, out var succeeded);
            Assert.True(succeeded);
            Assert.Equal(outputCompilation, editedCompilation);
        }

        [Fact]
        public void Failed_Edit_Does_Not_Change_Compilation()
        {
            var source = @"
class C { }
";

            var parseOptions = TestOptions.Regular;
            Compilation compilation = CreateCompilation(source, options: TestOptions.DebugDll, parseOptions: parseOptions);
            compilation.VerifyDiagnostics();
            Assert.Single(compilation.SyntaxTrees);

            AdditionalFileAddedGenerator testGenerator = new AdditionalFileAddedGenerator() { CanApplyChanges = false };

            GeneratorDriver driver = new CSharpGeneratorDriver(compilation, parseOptions, ImmutableArray.Create<ISourceGenerator>(testGenerator), ImmutableArray<AdditionalText>.Empty);

            driver.RunFullGeneration(compilation, out var outputCompilation);
            Assert.Single(outputCompilation.SyntaxTrees);

            // create an edit
            AdditionalFileAddedEdit edit = new AdditionalFileAddedEdit(new InMemoryAdditionalText("c:\\a\\file2.cs", ""));
            driver = driver.WithPendingEdits(ImmutableArray.Create<PendingEdit>(edit));

            // now try apply edits (which will fail)
            driver = driver.TryApplyEdits(compilation, out var editedCompilation, out var succeeded);
            Assert.False(succeeded);
            Assert.Single(editedCompilation.SyntaxTrees);
            Assert.Equal(compilation, editedCompilation);
        }

        [Fact]
        public void Added_Additional_File()
        {
            var source = @"
class C { }
";

            var parseOptions = TestOptions.Regular;
            Compilation compilation = CreateCompilation(source, options: TestOptions.DebugDll, parseOptions: parseOptions);
            compilation.VerifyDiagnostics();
            Assert.Single(compilation.SyntaxTrees);

            AdditionalFileAddedGenerator testGenerator = new AdditionalFileAddedGenerator();

            GeneratorDriver driver = new CSharpGeneratorDriver(compilation, parseOptions, ImmutableArray.Create<ISourceGenerator>(testGenerator), ImmutableArray<AdditionalText>.Empty);

            // run initial generation pass
            driver = driver.RunFullGeneration(compilation, out var outputCompilation);
            Assert.Single(outputCompilation.SyntaxTrees);

            // create an edit
            AdditionalFileAddedEdit edit = new AdditionalFileAddedEdit(new InMemoryAdditionalText("c:\\a\\file1.cs", ""));
            driver = driver.WithPendingEdits(ImmutableArray.Create<PendingEdit>(edit));

            // now try apply edits
            driver = driver.TryApplyEdits(compilation, out outputCompilation, out var succeeded);
            Assert.True(succeeded);
            Assert.Equal(2, outputCompilation.SyntaxTrees.Count());
        }

        [Fact]
        public void Multiple_Added_Additional_Files()
        {
            var source = @"
class C { }
";

            var parseOptions = TestOptions.Regular;
            Compilation compilation = CreateCompilation(source, options: TestOptions.DebugDll, parseOptions: parseOptions);
            compilation.VerifyDiagnostics();
            Assert.Single(compilation.SyntaxTrees);

            AdditionalFileAddedGenerator testGenerator = new AdditionalFileAddedGenerator();

            GeneratorDriver driver = new CSharpGeneratorDriver(compilation, parseOptions,
                                                               generators: ImmutableArray.Create<ISourceGenerator>(testGenerator),
                                                               additionalTexts: ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText("c:\\a\\file1.cs", "")));

            // run initial generation pass
            driver = driver.RunFullGeneration(compilation, out var outputCompilation);
            Assert.Equal(2, outputCompilation.SyntaxTrees.Count());

            // create an edit
            AdditionalFileAddedEdit edit = new AdditionalFileAddedEdit(new InMemoryAdditionalText("c:\\a\\file2.cs", ""));
            driver = driver.WithPendingEdits(ImmutableArray.Create<PendingEdit>(edit));

            // now try apply edits
            driver = driver.TryApplyEdits(compilation, out var editedCompilation, out var succeeded);
            Assert.True(succeeded);
            Assert.Equal(3, editedCompilation.SyntaxTrees.Count());

            // if we run a full compilation again, we should still get 3 syntax trees
            driver = driver.RunFullGeneration(compilation, out outputCompilation);
            Assert.Equal(3, outputCompilation.SyntaxTrees.Count());

            // lets add multiple edits   
            driver = driver.WithPendingEdits(ImmutableArray.Create<PendingEdit>(new AdditionalFileAddedEdit(new InMemoryAdditionalText("c:\\a\\file3.cs", "")),
                                                                                new AdditionalFileAddedEdit(new InMemoryAdditionalText("c:\\a\\file4.cs", "")),
                                                                                new AdditionalFileAddedEdit(new InMemoryAdditionalText("c:\\a\\file5.cs", ""))));
            // now try apply edits
            driver = driver.TryApplyEdits(compilation, out editedCompilation, out succeeded);
            Assert.True(succeeded);
            Assert.Equal(6, editedCompilation.SyntaxTrees.Count());
        }

        [Fact]
        public void Added_Additional_File_With_Full_Generation()
        {
            var source = @"
class C { }
";

            var parseOptions = TestOptions.Regular;
            Compilation compilation = CreateCompilation(source, options: TestOptions.DebugDll, parseOptions: parseOptions);
            compilation.VerifyDiagnostics();
            Assert.Single(compilation.SyntaxTrees);

            AdditionalFileAddedGenerator testGenerator = new AdditionalFileAddedGenerator();
            var text = new InMemoryAdditionalText("c:\\a\\file1.cs", "");


            GeneratorDriver driver = new CSharpGeneratorDriver(compilation, parseOptions,
                                                               generators: ImmutableArray.Create<ISourceGenerator>(testGenerator),
                                                               additionalTexts: ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText("c:\\a\\file1.cs", "")));

            driver = driver.RunFullGeneration(compilation, out var outputCompilation);

            // we should have the a single extra file for the additional texts
            Assert.Equal(2, outputCompilation.SyntaxTrees.Count());

            // even if we run a full gen, or partial, nothing should change yet
            driver = driver.TryApplyEdits(outputCompilation, out var editedCompilation, out var succeeded);
            Assert.True(succeeded);
            Assert.Equal(2, editedCompilation.SyntaxTrees.Count());

            driver = driver.RunFullGeneration(compilation, out outputCompilation);
            Assert.Equal(2, outputCompilation.SyntaxTrees.Count());

            // create an edit
            AdditionalFileAddedEdit edit = new AdditionalFileAddedEdit(new InMemoryAdditionalText("c:\\a\\file2.cs", ""));
            driver = driver.WithPendingEdits(ImmutableArray.Create<PendingEdit>(edit));

            // now try apply edits
            driver = driver.TryApplyEdits(compilation, out editedCompilation, out succeeded);
            Assert.True(succeeded);
            Assert.Equal(3, editedCompilation.SyntaxTrees.Count());

            // if we run a full compilation again, we should still get 3 syntax trees
            driver = driver.RunFullGeneration(compilation, out outputCompilation);
            Assert.Equal(3, outputCompilation.SyntaxTrees.Count());
        }

        [Fact]
        public void Edits_Are_Applied_During_Full_Generation()
        {
            var source = @"
class C { }
";

            var parseOptions = TestOptions.Regular;
            Compilation compilation = CreateCompilation(source, options: TestOptions.DebugDll, parseOptions: parseOptions);
            compilation.VerifyDiagnostics();
            Assert.Single(compilation.SyntaxTrees);

            AdditionalFileAddedGenerator testGenerator = new AdditionalFileAddedGenerator();
            var text = new InMemoryAdditionalText("c:\\a\\file1.cs", "");

            GeneratorDriver driver = new CSharpGeneratorDriver(compilation, parseOptions,
                                                               generators: ImmutableArray.Create<ISourceGenerator>(testGenerator),
                                                               additionalTexts: ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText("c:\\a\\file1.cs", "")));

            driver.RunFullGeneration(compilation, out var outputCompilation);
            Assert.Equal(2, outputCompilation.SyntaxTrees.Count());

            // add multiple edits   
            driver = driver.WithPendingEdits(ImmutableArray.Create<PendingEdit>(new AdditionalFileAddedEdit(new InMemoryAdditionalText("c:\\a\\file2.cs", "")),
                                                                                new AdditionalFileAddedEdit(new InMemoryAdditionalText("c:\\a\\file3.cs", "")),
                                                                                new AdditionalFileAddedEdit(new InMemoryAdditionalText("c:\\a\\file4.cs", ""))));

            // but just do a full generation (don't try apply)
            driver.RunFullGeneration(compilation, out outputCompilation);
            Assert.Equal(5, outputCompilation.SyntaxTrees.Count());
        }

        [Fact]
        public void Adding_Another_Generator_Makes_TryApplyEdits_Fail()
        {
            var source = @"
class C { }
";

            var parseOptions = TestOptions.Regular;
            Compilation compilation = CreateCompilation(source, options: TestOptions.DebugDll, parseOptions: parseOptions);
            compilation.VerifyDiagnostics();
            Assert.Single(compilation.SyntaxTrees);

            SingleFileTestGenerator testGenerator1 = new SingleFileTestGenerator("public class D { }");
            SingleFileTestGenerator testGenerator2 = new SingleFileTestGenerator("public class E { }");

            GeneratorDriver driver = new CSharpGeneratorDriver(compilation, parseOptions,
                                                               generators: ImmutableArray.Create<ISourceGenerator>(testGenerator1),
                                                               additionalTexts: ImmutableArray<AdditionalText>.Empty);

            driver = driver.RunFullGeneration(compilation, out var outputCompilation);
            Assert.Equal(2, outputCompilation.SyntaxTrees.Count());

            // try apply edits
            driver = driver.TryApplyEdits(compilation, out _, out bool success);
            Assert.True(success);

            // add another generator
            driver = driver.AddGenerators(ImmutableArray.Create<ISourceGenerator>(testGenerator2));

            // try apply changes should now fail
            driver = driver.TryApplyEdits(compilation, out _, out success);
            Assert.False(success);

            // full generation
            driver = driver.RunFullGeneration(compilation, out outputCompilation);
            Assert.Equal(3, outputCompilation.SyntaxTrees.Count());

            // try apply changes should now succeed
            driver.TryApplyEdits(compilation, out _, out success);
            Assert.True(success);
        }
    }

    internal class SingleFileTestGenerator : ISourceGenerator
    {
        private readonly string _content;
        private readonly string _hintName;

        public SingleFileTestGenerator(string content, string hintName = "generatedFile")
        {
            _content = content;
            _hintName = hintName;
        }

        public void Execute(SourceGeneratorContext context)
        {
            context.AdditionalSources.Add(this._hintName, SourceText.From(_content, Encoding.UTF8));
        }

        public void Initialize(InitializationContext context)
        {
        }
    }

    internal class CallbackGenerator : ISourceGenerator
    {
        private readonly Action<InitializationContext> _onInit;
        private readonly Action<SourceGeneratorContext> _onExecute;
        private readonly string _sourceOpt;

        public CallbackGenerator(Action<InitializationContext> onInit, Action<SourceGeneratorContext> onExecute, string sourceOpt = "")
        {
            _onInit = onInit;
            _onExecute = onExecute;
            _sourceOpt = sourceOpt;
        }

        public void Execute(SourceGeneratorContext context)
        {
            _onExecute(context);
            if (!string.IsNullOrWhiteSpace(_sourceOpt))
            {
                context.AdditionalSources.Add("source.cs", SourceText.From(_sourceOpt, Encoding.UTF8));
            }
        }
        public void Initialize(InitializationContext context) => _onInit(context);
    }

    internal class AdditionalFileAddedGenerator : ISourceGenerator
    {
        public bool CanApplyChanges { get; set; } = true;

        public void Execute(SourceGeneratorContext context)
        {
            foreach (var file in context.AnalyzerOptions.AdditionalFiles)
            {
                AddSourceForAdditionalFile(context.AdditionalSources, file);
            }
        }

        public void Initialize(InitializationContext context)
        {
            context.RegisterForAdditionalFileChanges(UpdateContext);
        }

        bool UpdateContext(EditContext context, AdditionalFileEdit edit)
        {
            if (edit is AdditionalFileAddedEdit add && CanApplyChanges)
            {
                AddSourceForAdditionalFile(context.AdditionalSources, add.AddedText);
                return true;
            }
            return false;
        }

        private void AddSourceForAdditionalFile(AdditionalSourcesCollection sources, AdditionalText file) => sources.Add(GetGeneratedFileName(file.Path), SourceText.From("", Encoding.UTF8));

        private string GetGeneratedFileName(string path) => $"{Path.GetFileNameWithoutExtension(path)}.generated";
    }

    internal class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _content;

        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            _content = SourceText.From(content);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => _content;

    }
}

