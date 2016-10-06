// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.Extensibility.Composition;
using Microsoft.CodeAnalysis.Editor.Implementation.NavigateTo;
using Microsoft.CodeAnalysis.Editor.UnitTests.NavigateTo;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using Roslyn.Test.EditorUtilities.NavigateTo;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.NavigateTo
{
    public class NavigateToTests : AbstractNavigateToTests
    {
        protected override string Language => "csharp";

        protected override Task<TestWorkspace> CreateWorkspace(string content, ExportProvider exportProvider)
            => TestWorkspace.CreateCSharpAsync(content, exportProvider: exportProvider);

        private async Task TestAsync(string content, Func<Task> body)
        {
            await TestAsync(content, body, outOfProcess: true);
            await TestAsync(content, body, outOfProcess: false);
        }

        private async Task TestAsync(string content, Func<Task> body, bool outOfProcess)
        {
            using (var workspace = await SetupWorkspaceAsync(content))
            {
                await body();
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task NoItemsForEmptyFile()
        {
            await TestAsync("", async () =>
            {
                Assert.Empty(await _aggregator.GetItemsAsync("Hello"));
            });
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindClass()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupClass, StandardGlyphItem.GlyphItemFriend);
                var item = (await _aggregator.GetItemsAsync("Foo")).Single(x => x.Kind != "Method");
                VerifyNavigateToResultItem(item, "Foo", MatchKind.Exact, NavigateToItemKind.Class);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindVerbatimClass()
        {
            using (var workspace = await SetupWorkspaceAsync("class @static { }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupClass, StandardGlyphItem.GlyphItemFriend);
                var item = (await _aggregator.GetItemsAsync("static")).Single(x => x.Kind != "Method");
                VerifyNavigateToResultItem(item, "static", MatchKind.Exact, NavigateToItemKind.Class, displayName: "@static");

                // Check searching for @static too
                item = (await _aggregator.GetItemsAsync("@static")).Single(x => x.Kind != "Method");
                VerifyNavigateToResultItem(item, "static", MatchKind.Exact, NavigateToItemKind.Class, displayName: "@static");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindNestedClass()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { class Bar { internal class DogBed { } } }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupClass, StandardGlyphItem.GlyphItemFriend);
                var item = (await _aggregator.GetItemsAsync("DogBed")).Single(x => x.Kind != "Method");
                VerifyNavigateToResultItem(item, "DogBed", MatchKind.Exact, NavigateToItemKind.Class);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindMemberInANestedClass()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { class Bar { class DogBed { public void Method() { } } } }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupMethod, StandardGlyphItem.GlyphItemPublic);
                var item = (await _aggregator.GetItemsAsync("Method")).Single();
                VerifyNavigateToResultItem(item, "Method", MatchKind.Exact, NavigateToItemKind.Method, "Method()", $"{FeaturesResources.type_space}Foo.Bar.DogBed");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindGenericClassWithConstraints()
        {
            using (var workspace = await SetupWorkspaceAsync("using System.Collections; class Foo<T> where T : IEnumerable { }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupClass, StandardGlyphItem.GlyphItemFriend);
                var item = (await _aggregator.GetItemsAsync("Foo")).Single(x => x.Kind != "Method");
                VerifyNavigateToResultItem(item, "Foo", MatchKind.Exact, NavigateToItemKind.Class, displayName: "Foo<T>");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindGenericMethodWithConstraints()
        {
            using (var workspace = await SetupWorkspaceAsync("using System; class Foo<U> { public void Bar<T>(T item) where T:IComparable<T> {} }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupMethod, StandardGlyphItem.GlyphItemPublic);
                var item = (await _aggregator.GetItemsAsync("Bar")).Single();
                VerifyNavigateToResultItem(item, "Bar", MatchKind.Exact, NavigateToItemKind.Method, "Bar<T>(T)", $"{FeaturesResources.type_space}Foo<U>");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindPartialClass()
        {
            using (var workspace = await SetupWorkspaceAsync("public partial class Foo { int a; } partial class Foo { int b; }"))
            {
                var expecteditem1 = new NavigateToItem("Foo", NavigateToItemKind.Class, "csharp", null, null, MatchKind.Exact, true, null);
                var expecteditems = new List<NavigateToItem> { expecteditem1, expecteditem1 };

                var items = await _aggregator.GetItemsAsync("Foo");

                VerifyNavigateToResultItems(expecteditems, items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindTypesInMetadata()
        {
            using (var workspace = await SetupWorkspaceAsync("using System; Class Program {FileStyleUriParser f;}"))
            {
                var items = await _aggregator.GetItemsAsync("FileStyleUriParser");
                Assert.Equal(items.Count(), 0);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindClassInNamespace()
        {
            using (var workspace = await SetupWorkspaceAsync("namespace Bar { class Foo { } }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupClass, StandardGlyphItem.GlyphItemFriend);
                var item = (await _aggregator.GetItemsAsync("Foo")).Single(x => x.Kind != "Method");
                VerifyNavigateToResultItem(item, "Foo", MatchKind.Exact, NavigateToItemKind.Class);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindStruct()
        {
            using (var workspace = await SetupWorkspaceAsync("struct Bar { }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupStruct, StandardGlyphItem.GlyphItemFriend);
                var item = (await _aggregator.GetItemsAsync("B")).Single(x => x.Kind != "Method");
                VerifyNavigateToResultItem(item, "Bar", MatchKind.Prefix, NavigateToItemKind.Structure);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindEnum()
        {
            using (var workspace = await SetupWorkspaceAsync("enum Colors {Red, Green, Blue}"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupEnum, StandardGlyphItem.GlyphItemFriend);
                var item = (await _aggregator.GetItemsAsync("Colors")).Single(x => x.Kind != "Method");
                VerifyNavigateToResultItem(item, "Colors", MatchKind.Exact, NavigateToItemKind.Enum);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindEnumMember()
        {
            using (var workspace = await SetupWorkspaceAsync("enum Colors {Red, Green, Blue}"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupEnumMember, StandardGlyphItem.GlyphItemPublic);
                var item = (await _aggregator.GetItemsAsync("R")).Single();
                VerifyNavigateToResultItem(item, "Red", MatchKind.Prefix, NavigateToItemKind.EnumItem);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindField1()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { int bar;}"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupField, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("b")).Single();
                VerifyNavigateToResultItem(item, "bar", MatchKind.Prefix, NavigateToItemKind.Field, additionalInfo: $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindField2()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { int bar;}"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupField, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("ba")).Single();
                VerifyNavigateToResultItem(item, "bar", MatchKind.Prefix, NavigateToItemKind.Field, additionalInfo: $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindField3()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { int bar;}"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupField, StandardGlyphItem.GlyphItemPrivate);
                Assert.Empty(await _aggregator.GetItemsAsync("ar"));
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindVerbatimField()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { int @string;}"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupField, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("string")).Single();
                VerifyNavigateToResultItem(item, "string", MatchKind.Exact, NavigateToItemKind.Field, displayName: "@string", additionalInfo: $"{FeaturesResources.type_space}Foo");

                // Check searching for@string too
                item = (await _aggregator.GetItemsAsync("@string")).Single();
                VerifyNavigateToResultItem(item, "string", MatchKind.Exact, NavigateToItemKind.Field, displayName: "@string", additionalInfo: $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindPtrField1()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { int* bar; }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupField, StandardGlyphItem.GlyphItemPrivate);
                Assert.Empty(await _aggregator.GetItemsAsync("ar"));
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindPtrField2()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { int* bar; }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupField, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("b")).Single();
                VerifyNavigateToResultItem(item, "bar", MatchKind.Prefix, NavigateToItemKind.Field);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindConstField()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { const int bar = 7;}"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupConstant, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("ba")).Single();
                VerifyNavigateToResultItem(item, "bar", MatchKind.Prefix, NavigateToItemKind.Constant);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindIndexer()
        {
            var program = @"class Foo { int[] arr; public int this[int i] { get { return arr[i]; } set { arr[i] = value; } } }";
            using (var workspace = await SetupWorkspaceAsync(program))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupProperty, StandardGlyphItem.GlyphItemPublic);
                var item = (await _aggregator.GetItemsAsync("this")).Single();
                VerifyNavigateToResultItem(item, "this[]", MatchKind.Exact, NavigateToItemKind.Property, displayName: "this[int]", additionalInfo: $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindEvent()
        {
            var program = "class Foo { public event EventHandler ChangedEventHandler; }";
            using (var workspace = await SetupWorkspaceAsync(program))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupEvent, StandardGlyphItem.GlyphItemPublic);
                var item = (await _aggregator.GetItemsAsync("CEH")).Single();
                VerifyNavigateToResultItem(item, "ChangedEventHandler", MatchKind.Regular, NavigateToItemKind.Event, additionalInfo: $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindAutoProperty()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { int Bar { get; set; } }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupProperty, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("B")).Single();
                VerifyNavigateToResultItem(item, "Bar", MatchKind.Prefix, NavigateToItemKind.Property, additionalInfo: $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindMethod()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { void DoSomething(); }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupMethod, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("DS")).Single();
                VerifyNavigateToResultItem(item, "DoSomething", MatchKind.Regular, NavigateToItemKind.Method, "DoSomething()", $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindVerbatimMethod()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { void @static(); }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupMethod, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("static")).Single();
                VerifyNavigateToResultItem(item, "static", MatchKind.Exact, NavigateToItemKind.Method, "@static()", $"{FeaturesResources.type_space}Foo");

                // Verify if we search for @static too
                item = (await _aggregator.GetItemsAsync("@static")).Single();
                VerifyNavigateToResultItem(item, "static", MatchKind.Exact, NavigateToItemKind.Method, "@static()", $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindParameterizedMethod()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { void DoSomething(int a, string b) {} }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupMethod, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("DS")).Single();
                VerifyNavigateToResultItem(item, "DoSomething", MatchKind.Regular, NavigateToItemKind.Method, "DoSomething(int, string)", $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindConstructor()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { public Foo(){} }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupMethod, StandardGlyphItem.GlyphItemPublic);
                var item = (await _aggregator.GetItemsAsync("Foo")).Single(t => t.Kind == NavigateToItemKind.Method);
                VerifyNavigateToResultItem(item, "Foo", MatchKind.Exact, NavigateToItemKind.Method, "Foo()", $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindParameterizedConstructor()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { public Foo(int i){} }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupMethod, StandardGlyphItem.GlyphItemPublic);
                var item = (await _aggregator.GetItemsAsync("Foo")).Single(t => t.Kind == NavigateToItemKind.Method);
                VerifyNavigateToResultItem(item, "Foo", MatchKind.Exact, NavigateToItemKind.Method, "Foo(int)", $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindStaticConstructor()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { static Foo(){} }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupMethod, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("Foo")).Single(t => t.Kind == NavigateToItemKind.Method && t.Name != ".ctor");
                VerifyNavigateToResultItem(item, "Foo", MatchKind.Exact, NavigateToItemKind.Method, "static Foo()", $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindPartialMethods()
        {
            using (var workspace = await SetupWorkspaceAsync("partial class Foo { partial void Bar(); } partial class Foo { partial void Bar() { Console.Write(\"hello\"); } }"))
            {
                var expecteditem1 = new NavigateToItem("Bar", NavigateToItemKind.Method, "csharp", null, null, MatchKind.Exact, true, null);
                var expecteditems = new List<NavigateToItem> { expecteditem1, expecteditem1 };

                var items = await _aggregator.GetItemsAsync("Bar");

                VerifyNavigateToResultItems(expecteditems, items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindPartialMethodDefinitionOnly()
        {
            using (var workspace = await SetupWorkspaceAsync("partial class Foo { partial void Bar(); }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupMethod, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("Bar")).Single();
                VerifyNavigateToResultItem(item, "Bar", MatchKind.Exact, NavigateToItemKind.Method, "Bar()", $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindPartialMethodImplementationOnly()
        {
            using (var workspace = await SetupWorkspaceAsync("partial class Foo { partial void Bar() { } }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupMethod, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("Bar")).Single();
                VerifyNavigateToResultItem(item, "Bar", MatchKind.Exact, NavigateToItemKind.Method, "Bar()", $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindOverriddenMembers()
        {
            var program = "class Foo { public virtual string Name { get; set; } } class DogBed : Foo { public override string Name { get { return base.Name; } set {} } }";
            using (var workspace = await SetupWorkspaceAsync(program))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupProperty, StandardGlyphItem.GlyphItemPublic);
                var expecteditem1 = new NavigateToItem("Name", NavigateToItemKind.Property, "csharp", null, null, MatchKind.Exact, true, null);
                var expecteditems = new List<NavigateToItem> { expecteditem1, expecteditem1 };

                var items = await _aggregator.GetItemsAsync("Name");

                VerifyNavigateToResultItems(expecteditems, items);

                var item = items.ElementAt(0);
                var itemDisplay = item.DisplayFactory.CreateItemDisplay(item);
                var unused = itemDisplay.Glyph;

                Assert.Equal("Name", itemDisplay.Name);
                Assert.Equal($"{FeaturesResources.type_space}DogBed", itemDisplay.AdditionalInformation);
                _glyphServiceMock.Verify();

                item = items.ElementAt(1);
                itemDisplay = item.DisplayFactory.CreateItemDisplay(item);
                unused = itemDisplay.Glyph;

                Assert.Equal("Name", itemDisplay.Name);
                Assert.Equal($"{FeaturesResources.type_space}Foo", itemDisplay.AdditionalInformation);
                _glyphServiceMock.Verify();
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindInterface()
        {
            using (var workspace = await SetupWorkspaceAsync("public interface IFoo { }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupInterface, StandardGlyphItem.GlyphItemPublic);
                var item = (await _aggregator.GetItemsAsync("IF")).Single();
                VerifyNavigateToResultItem(item, "IFoo", MatchKind.Prefix, NavigateToItemKind.Interface);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindDelegateInNamespace()
        {
            using (var workspace = await SetupWorkspaceAsync("namespace Foo { delegate void DoStuff(); }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupDelegate, StandardGlyphItem.GlyphItemFriend);
                var item = (await _aggregator.GetItemsAsync("DoStuff")).Single(x => x.Kind != "Method");
                VerifyNavigateToResultItem(item, "DoStuff", MatchKind.Exact, NavigateToItemKind.Delegate);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindLambdaExpression()
        {
            using (var workspace = await SetupWorkspaceAsync("using System; class Foo { Func<int, int> sqr = x => x*x; }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupField, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("sqr")).Single();
                VerifyNavigateToResultItem(item, "sqr", MatchKind.Exact, NavigateToItemKind.Field, "sqr", $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindArray()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { object[] itemArray; }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupField, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("itemArray")).Single();
                VerifyNavigateToResultItem(item, "itemArray", MatchKind.Exact, NavigateToItemKind.Field, "itemArray", $"{FeaturesResources.type_space}Foo");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindClassAndMethodWithSameName()
        {
            using (var workspace = await SetupWorkspaceAsync("class Foo { } class Test { void Foo() { } }"))
            {
                var expectedItems = new List<NavigateToItem>
                {
                    new NavigateToItem("Foo", NavigateToItemKind.Method, "csharp", "Foo", null, MatchKind.Exact, true, null),
                    new NavigateToItem("Foo", NavigateToItemKind.Class, "csharp", "Foo", null, MatchKind.Exact, true, null)
                };
                var items = await _aggregator.GetItemsAsync("Foo");
                VerifyNavigateToResultItems(expectedItems, items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindMethodNestedInGenericTypes()
        {
            using (var workspace = await SetupWorkspaceAsync("class A<T> { class B { struct C<U> { void M() { } } } }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupMethod, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("M")).Single();
                VerifyNavigateToResultItem(item, "M", MatchKind.Exact, NavigateToItemKind.Method, displayName: "M()", additionalInfo: $"{FeaturesResources.type_space}A<T>.B.C<U>");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task OrderingOfConstructorsAndTypes()
        {
            using (var workspace = await SetupWorkspaceAsync(@"
                class C1
                {
                    C1(int i) {}
                }
                class C2
                {
                    C2(float f) {}
                    static C2() {}
                }"))
            {
                var expecteditems = new List<NavigateToItem>
                {
                    new NavigateToItem("C1", NavigateToItemKind.Class, "csharp", "C1", null, MatchKind.Prefix, true, null),
                    new NavigateToItem("C1", NavigateToItemKind.Method, "csharp", "C1", null, MatchKind.Prefix, true, null),
                    new NavigateToItem("C2", NavigateToItemKind.Class, "csharp", "C2", null, MatchKind.Prefix, true, null),
                    new NavigateToItem("C2", NavigateToItemKind.Method, "csharp", "C2", null, MatchKind.Prefix, true, null), // this is the static ctor
                    new NavigateToItem("C2", NavigateToItemKind.Method, "csharp", "C2", null, MatchKind.Prefix, true, null),
                };
                var items = (await _aggregator.GetItemsAsync("C")).ToList();
                items.Sort(CompareNavigateToItems);
                VerifyNavigateToResultItems(expecteditems, items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task NavigateToMethodWithNullableParameter()
        {
            using (var workspace = await SetupWorkspaceAsync("class C { void M(object? o) {} }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupMethod, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("M")).Single();
                VerifyNavigateToResultItem(item, "M", MatchKind.Exact, NavigateToItemKind.Method, "M(object?)");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task StartStopSanity()
        {
            // Verify that multiple calls to start/stop and dispose don't blow up
            using (var workspace = await SetupWorkspaceAsync("public class Foo { }"))
            {
                // Do one set of queries
                Assert.Single((await _aggregator.GetItemsAsync("Foo")).Where(x => x.Kind != "Method"));
                _provider.StopSearch();

                // Do the same query again, make sure nothing was left over
                Assert.Single((await _aggregator.GetItemsAsync("Foo")).Where(x => x.Kind != "Method"));
                _provider.StopSearch();

                // Dispose the provider
                _provider.Dispose();
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task DescriptionItems()
        {
            using (var workspace = await SetupWorkspaceAsync("public\r\nclass\r\nFoo\r\n{ }"))
            {
                var item = (await _aggregator.GetItemsAsync("F")).Single(x => x.Kind != "Method");
                var itemDisplay = item.DisplayFactory.CreateItemDisplay(item);

                var descriptionItems = itemDisplay.DescriptionItems;

                Action<string, string> assertDescription = (label, value) =>
                {
                    var descriptionItem = descriptionItems.Single(i => i.Category.Single().Text == label);
                    Assert.Equal(value, descriptionItem.Details.Single().Text);
                };

                assertDescription("File:", workspace.Documents.Single().Name);
                assertDescription("Line:", "3"); // one based line number
                assertDescription("Project:", "Test");
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task TermSplittingTest1()
        {
            var source = "class SyllableBreaking {int GetKeyWord; int get_key_word; string get_keyword; int getkeyword; int wake;}";
            using (var workspace = await SetupWorkspaceAsync(source))
            {
                var expecteditem1 = new NavigateToItem("get_keyword", NavigateToItemKind.Field, "csharp", null, null, MatchKind.Regular, null);
                var expecteditem2 = new NavigateToItem("get_key_word", NavigateToItemKind.Field, "csharp", null, null, MatchKind.Regular, null);
                var expecteditem3 = new NavigateToItem("GetKeyWord", NavigateToItemKind.Field, "csharp", null, null, MatchKind.Regular, true, null);
                var expecteditems = new List<NavigateToItem> { expecteditem1, expecteditem2, expecteditem3 };

                var items = await _aggregator.GetItemsAsync("GK");

                Assert.Equal(expecteditems.Count(), items.Count());

                VerifyNavigateToResultItems(expecteditems, items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task TermSplittingTest2()
        {
            var source = "class SyllableBreaking {int GetKeyWord; int get_key_word; string get_keyword; int getkeyword; int wake;}";
            using (var workspace = await SetupWorkspaceAsync(source))
            {
                var expecteditem1 = new NavigateToItem("get_key_word", NavigateToItemKind.Field, "csharp", null, null, MatchKind.Regular, null);
                var expecteditem2 = new NavigateToItem("GetKeyWord", NavigateToItemKind.Field, "csharp", null, null, MatchKind.Regular, true, null);
                var expecteditems = new List<NavigateToItem> { expecteditem1, expecteditem2 };

                var items = await _aggregator.GetItemsAsync("GKW");

                VerifyNavigateToResultItems(expecteditems, items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task TermSplittingTest3()
        {
            var source = "class SyllableBreaking {int GetKeyWord; int get_key_word; string get_keyword; int getkeyword; int wake;}";
            using (var workspace = await SetupWorkspaceAsync(source))
            {
                var expecteditem1 = new NavigateToItem("get_key_word", NavigateToItemKind.Field, "csharp", null, null, MatchKind.Regular, null);
                var expecteditem2 = new NavigateToItem("GetKeyWord", NavigateToItemKind.Field, "csharp", null, null, MatchKind.Substring, true, null);
                var expecteditems = new List<NavigateToItem> { expecteditem1, expecteditem2 };

                var items = await _aggregator.GetItemsAsync("K W");

                VerifyNavigateToResultItems(expecteditems, items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task TermSplittingTest4()
        {
            var source = "class SyllableBreaking {int GetKeyWord; int get_key_word; string get_keyword; int getkeyword; int wake;}";
            using (var workspace = await SetupWorkspaceAsync(source))
            {
                var items = await _aggregator.GetItemsAsync("WKG");
                Assert.Empty(items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task TermSplittingTest5()
        {
            var source = "class SyllableBreaking {int GetKeyWord; int get_key_word; string get_keyword; int getkeyword; int wake;}";
            using (var workspace = await SetupWorkspaceAsync(source))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupField, StandardGlyphItem.GlyphItemPrivate);
                var item = (await _aggregator.GetItemsAsync("G_K_W")).Single();
                VerifyNavigateToResultItem(item, "get_key_word", MatchKind.Regular, NavigateToItemKind.Field);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task TermSplittingTest6()
        {
            var source = "class SyllableBreaking {int GetKeyWord; int get_key_word; string get_keyword; int getkeyword; int wake;}";
            using (var workspace = await SetupWorkspaceAsync(source))
            {
                var expecteditems = new List<NavigateToItem>
                {
                    new NavigateToItem("get_key_word", NavigateToItemKind.Field, "csharp", null, null, MatchKind.Prefix, true, null),
                    new NavigateToItem("GetKeyWord", NavigateToItemKind.Field, "csharp", null, null, MatchKind.Prefix, null)
                };

                var items = await _aggregator.GetItemsAsync("get word");

                VerifyNavigateToResultItems(expecteditems, items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task TermSplittingTest7()
        {
            var source = "class SyllableBreaking {int GetKeyWord; int get_key_word; string get_keyword; int getkeyword; int wake;}";
            using (var workspace = await SetupWorkspaceAsync(source))
            {
                var items = await _aggregator.GetItemsAsync("GTW");
                Assert.Empty(items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task TermIndexer1()
        {
            var source =
@"class C
{
    public int this[int y] { get { } }
}

class D
{
    void Foo()
    {
        var q = new C();
        var b = q[4];
    }
}";
            using (var workspace = await SetupWorkspaceAsync(source))
            {
                var expecteditems = new List<NavigateToItem>
                {
                    new NavigateToItem("this[]", NavigateToItemKind.Property, "csharp", null, null, MatchKind.Exact, true, null),
                };

                var items = await _aggregator.GetItemsAsync("this");

                VerifyNavigateToResultItems(expecteditems, items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task DottedPattern1()
        {
            var source = "namespace Foo { namespace Bar { class Baz { void Quux() { } } } }";
            using (var workspace = await SetupWorkspaceAsync(source))
            {
                var expecteditems = new List<NavigateToItem>
                {
                    new NavigateToItem("Quux", NavigateToItemKind.Method, "csharp", null, null, MatchKind.Prefix, true, null)
                };

                var items = await _aggregator.GetItemsAsync("B.Q");

                VerifyNavigateToResultItems(expecteditems, items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task DottedPattern2()
        {
            var source = "namespace Foo { namespace Bar { class Baz { void Quux() { } } } }";
            using (var workspace = await SetupWorkspaceAsync(source))
            {
                var expecteditems = new List<NavigateToItem>
                {
                };

                var items = await _aggregator.GetItemsAsync("C.Q");

                VerifyNavigateToResultItems(expecteditems, items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task DottedPattern3()
        {
            var source = "namespace Foo { namespace Bar { class Baz { void Quux() { } } } }";
            using (var workspace = await SetupWorkspaceAsync(source))
            {
                var expecteditems = new List<NavigateToItem>
                {
                    new NavigateToItem("Quux", NavigateToItemKind.Method, "csharp", null, null, MatchKind.Prefix, true, null)
                };

                var items = await _aggregator.GetItemsAsync("B.B.Q");

                VerifyNavigateToResultItems(expecteditems, items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task DottedPattern4()
        {
            var source = "namespace Foo { namespace Bar { class Baz { void Quux() { } } } }";
            using (var workspace = await SetupWorkspaceAsync(source))
            {
                var expecteditems = new List<NavigateToItem>
                {
                    new NavigateToItem("Quux", NavigateToItemKind.Method, "csharp", null, null, MatchKind.Exact, true, null)
                };

                var items = await _aggregator.GetItemsAsync("Baz.Quux");

                VerifyNavigateToResultItems(expecteditems, items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task DottedPattern5()
        {
            var source = "namespace Foo { namespace Bar { class Baz { void Quux() { } } } }";
            using (var workspace = await SetupWorkspaceAsync(source))
            {
                var expecteditems = new List<NavigateToItem>
                {
                    new NavigateToItem("Quux", NavigateToItemKind.Method, "csharp", null, null, MatchKind.Exact, true, null)
                };

                var items = await _aggregator.GetItemsAsync("F.B.B.Quux");

                VerifyNavigateToResultItems(expecteditems, items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task DottedPattern6()
        {
            var source = "namespace Foo { namespace Bar { class Baz { void Quux() { } } } }";
            using (var workspace = await SetupWorkspaceAsync(source))
            {
                var expecteditems = new List<NavigateToItem>
                {
                };

                var items = await _aggregator.GetItemsAsync("F.F.B.B.Quux");

                VerifyNavigateToResultItems(expecteditems, items);
            }
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        [WorkItem(7855, "https://github.com/dotnet/Roslyn/issues/7855")]
        public async Task DottedPattern7()
        {
            var source = "namespace Foo { namespace Bar { class Baz<X,Y,Z> { void Quux() { } } } }";
            using (var workspace = await SetupWorkspaceAsync(source))
            {
                var expecteditems = new List<NavigateToItem>
                {
                    new NavigateToItem("Quux", NavigateToItemKind.Method, "csharp", null, null, MatchKind.Prefix, true, null)
                };

                var items = await _aggregator.GetItemsAsync("Baz.Q");

                VerifyNavigateToResultItems(expecteditems, items);
            }
        }

        [WorkItem(1174255, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1174255")]
        [WorkItem(8009, "https://github.com/dotnet/roslyn/issues/8009")]
        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task NavigateToGeneratedFiles()
        {
            using (var workspace = await TestWorkspace.CreateAsync(@"
<Workspace>
    <Project Language=""C#"" CommonReferences=""true"">
        <Document FilePath=""File1.cs"">
            namespace N
            {
                public partial class C
                {
                    public void VisibleMethod() { }
                }
            }
        </Document>
        <Document FilePath=""File1.g.cs"">
            namespace N
            {
                public partial class C
                {
                    public void VisibleMethod_Generated() { }
                }
            }
        </Document>
    </Project>
</Workspace>
", exportProvider: s_exportProvider))
            {
                var aggregateListener = AggregateAsynchronousOperationListener.CreateEmptyListener();

                _provider = new NavigateToItemProvider(
                    workspace, _glyphServiceMock.Object, aggregateListener,
                    workspace.ExportProvider.GetExportedValues<Lazy<INavigateToOptionsService, VisualStudioVersionMetadata>>());
                _aggregator = new NavigateToTestAggregator(_provider);

                var items = await _aggregator.GetItemsAsync("VisibleMethod");
                var expectedItems = new List<NavigateToItem>()
                {
                    new NavigateToItem("VisibleMethod", NavigateToItemKind.Method, "csharp", null, null, MatchKind.Exact, true, null),
                    new NavigateToItem("VisibleMethod_Generated", NavigateToItemKind.Method, "csharp", null, null, MatchKind.Prefix, true, null)
                };

                // The pattern matcher should match 'VisibleMethod' to both 'VisibleMethod' and 'VisibleMethod_Not', except that
                // the _Not method is declared in a generated file.
                VerifyNavigateToResultItems(expectedItems, items);
            }
        }

        [WorkItem(11474, "https://github.com/dotnet/roslyn/pull/11474")]
        [WpfFact, Trait(Traits.Feature, Traits.Features.NavigateTo)]
        public async Task FindFuzzy1()
        {
            using (var workspace = await SetupWorkspaceAsync("class C { public void ToError() { } }"))
            {
                SetupVerifiableGlyph(StandardGlyphGroup.GlyphGroupMethod, StandardGlyphItem.GlyphItemPublic);
                var item = (await _aggregator.GetItemsAsync("ToEror")).Single();
                VerifyNavigateToResultItem(item, "ToError", MatchKind.Regular, NavigateToItemKind.Method, displayName: "ToError()");
            }
        }
    }
}
