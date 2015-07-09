﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.BuildTasks;
using Xunit;

namespace Microsoft.CodeAnalysis.BuildTasks.UnitTests
{
    public sealed class VbcTests
    {
        [Fact]
        public void SingleSource()
        {
            var vbc = new Vbc();
            vbc.Sources = MSBuildUtil.CreateTaskItems("test.vb");
            Assert.Equal("/optionstrict:custom /out:test.exe test.vb", vbc.GenerateResponseFileContents());
        }

        [Fact]
        public void MultipleSourceFiles()
        {
            var vbc = new Vbc();
            vbc.Sources = MSBuildUtil.CreateTaskItems("test1.vb", "test2.vb");
            Assert.Equal("/optionstrict:custom /out:test1.exe test1.vb test2.vb", vbc.GenerateResponseFileContents());
        }

        [Fact]
        public void TargetTypeDll()
        {
            var vbc = new Vbc();
            vbc.Sources = MSBuildUtil.CreateTaskItems("test.vb");
            vbc.TargetType = "library";
            Assert.Equal("/optionstrict:custom /out:test.dll /target:library test.vb", vbc.GenerateResponseFileContents());
        }

        [Fact]
        public void TargetTypeBad()
        {
            var vbc = new Vbc();
            vbc.Sources = MSBuildUtil.CreateTaskItems("test.vb");
            vbc.TargetType = "bad";
            Assert.Equal("/optionstrict:custom /out:test.exe /target:bad test.vb", vbc.GenerateResponseFileContents());
        }

        [Fact]
        public void OutputAssembly()
        {
            var vbc = new Vbc();
            vbc.OutputAssembly = MSBuildUtil.CreateTaskItem("x.exe");
            vbc.Sources = MSBuildUtil.CreateTaskItems("test.vb");
            Assert.Equal("/optionstrict:custom /out:x.exe test.vb", vbc.GenerateResponseFileContents());
        }

        [Fact]
        public void DefineConstantsSimple()
        {
            Action<string> test = (s) =>
            {
                var vbc = new Vbc();
                vbc.DefineConstants = s;
                vbc.Sources = MSBuildUtil.CreateTaskItems("test.vb");
                Assert.Equal($@"/optionstrict:custom /define:""{s}"" /out:test.exe test.vb", vbc.GenerateResponseFileContents());
            };

            test("D1;D2");
            test("D1,D2");
            test("D1 D2");
        }
    }
}
