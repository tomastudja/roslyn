﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
extern alias PDB;


using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.Emit;
using PDB::Microsoft.CodeAnalysis;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests.Emit
{
    public class CustomDebugInfoTests
    {
        [Fact]
        public void TryGetCustomDebugInfoRecord1()
        {
            byte[] cdi;

            Assert.Throws<InvalidOperationException>(() => CustomDebugInfoReader.TryGetCustomDebugInfoRecord(new byte[0], CustomDebugInfoKind.EditAndContinueLocalSlotMap));
            Assert.Throws<InvalidOperationException>(() => CustomDebugInfoReader.TryGetCustomDebugInfoRecord(new byte[] { 1 }, CustomDebugInfoKind.EditAndContinueLocalSlotMap));
            Assert.Throws<InvalidOperationException>(() => CustomDebugInfoReader.TryGetCustomDebugInfoRecord(new byte[] { 1, 2 }, CustomDebugInfoKind.EditAndContinueLocalSlotMap));

            // unknown version
            Assert.True(CustomDebugInfoReader.TryGetCustomDebugInfoRecord(new byte[] { 5, 1, 0, 0 }, CustomDebugInfoKind.EditAndContinueLocalSlotMap).IsDefault);

            // incomplete record header
            cdi = new byte[]
            {
                4, 1, 0, 0, // global header
                4, (byte)CustomDebugInfoKind.EditAndContinueLocalSlotMap,
            };

            Assert.True(CustomDebugInfoReader.TryGetCustomDebugInfoRecord(cdi, CustomDebugInfoKind.EditAndContinueLocalSlotMap).IsDefault);

            // record size too small
            cdi = new byte[]
            {
                4, 1, 0, 0, // global header
                /*version*/4, /*kind*/(byte)CustomDebugInfoKind.EditAndContinueLocalSlotMap, /*padding*/0, 0, /*size:*/ 0, 0, 0, 0,
            };

            Assert.Throws<InvalidOperationException>(() => CustomDebugInfoReader.TryGetCustomDebugInfoRecord(cdi, CustomDebugInfoKind.EditAndContinueLocalSlotMap));

            // invalid record size = Int32.MinValue
            cdi = new byte[]
            {
                4, 1, 0, 0, // global header
                /*version*/4, /*kind*/(byte)CustomDebugInfoKind.EditAndContinueLocalSlotMap, /*padding*/0, 0, /*size:*/ 0x00, 0x00, 0x00, 0x80,
                0, 0, 0, 0
            };

            Assert.Throws<InvalidOperationException>(() => CustomDebugInfoReader.TryGetCustomDebugInfoRecord(cdi, CustomDebugInfoKind.EditAndContinueLocalSlotMap));

            // empty record
            cdi = new byte[]
            {
                4, 1, 0, 0, // global header
                /*version*/4, /*kind*/(byte)CustomDebugInfoKind.EditAndContinueLocalSlotMap, /*padding*/0, 0, /*size:*/ 0x08, 0x00, 0x00, 0x00,
            };

            Assert.True(CustomDebugInfoReader.TryGetCustomDebugInfoRecord(cdi, CustomDebugInfoKind.EditAndContinueLocalSlotMap).IsEmpty);

            // record size too big
            cdi = new byte[]
            {
                4, 1, 0, 0, // global header
                /*version*/4, /*kind*/(byte)CustomDebugInfoKind.EditAndContinueLocalSlotMap, /*padding*/0, 0, /*size:*/ 0x0a, 0x00, 0x00, 0x00,
                0xab
            };

            Assert.Throws<InvalidOperationException>(() => CustomDebugInfoReader.TryGetCustomDebugInfoRecord(cdi, CustomDebugInfoKind.EditAndContinueLocalSlotMap));

            // valid record
            cdi = new byte[]
            {
                4, 1, 0, 0, // global header
                /*version*/4, /*kind*/(byte)CustomDebugInfoKind.EditAndContinueLocalSlotMap, /*padding*/0, 0, /*size:*/ 0x09, 0x00, 0x00, 0x00,
                0xab
            };

            AssertEx.Equal(new byte[] { 0xab }, CustomDebugInfoReader.TryGetCustomDebugInfoRecord(cdi, CustomDebugInfoKind.EditAndContinueLocalSlotMap));

            // record not matching
            cdi = new byte[]
            {
                4, 1, 0, 0, // global header
                /*version*/4, /*kind*/(byte)CustomDebugInfoKind.DynamicLocals, /*padding*/0, 0, /*size:*/ 0x09, 0x00, 0x00, 0x00,
                0xab
            };

            Assert.True(CustomDebugInfoReader.TryGetCustomDebugInfoRecord(cdi, CustomDebugInfoKind.EditAndContinueLocalSlotMap).IsDefault);

            // unknown record kind
            cdi = new byte[]
            {
                4, 1, 0, 0, // global header
                /*version*/4, /*kind*/0xff, /*padding*/0, 0, /*size:*/ 0x09, 0x00, 0x00, 0x00,
                0xab
            };

            Assert.True(CustomDebugInfoReader.TryGetCustomDebugInfoRecord(cdi, CustomDebugInfoKind.EditAndContinueLocalSlotMap).IsDefault);

            // multiple records (number in global header is ignored, the first matching record is returned)
            cdi = new byte[]
            {
                4, 1, 0, 0, // global header
                /*version*/4, /*kind*/(byte)CustomDebugInfoKind.EditAndContinueLocalSlotMap, /*padding*/0, 0, /*size:*/ 0x09, 0x00, 0x00, 0x00,
                0xab,
                /*version*/4, /*kind*/(byte)CustomDebugInfoKind.EditAndContinueLocalSlotMap, /*padding*/0, 0, /*size:*/ 0x09, 0x00, 0x00, 0x00,
                0xcd
            };

            AssertEx.Equal(new byte[] { 0xab }, CustomDebugInfoReader.TryGetCustomDebugInfoRecord(cdi, CustomDebugInfoKind.EditAndContinueLocalSlotMap));

            // multiple records (number in global header is ignored, the first record is returned)
            cdi = new byte[]
            {
                4, 1, 0, 0, // global header
                /*version*/4, /*kind*/(byte)CustomDebugInfoKind.DynamicLocals, /*padding*/0, 0, /*size:*/ 0x09, 0x00, 0x00, 0x00,
                0xab,
                /*version*/4, /*kind*/(byte)CustomDebugInfoKind.EditAndContinueLocalSlotMap, /*padding*/0, 0, /*size:*/ 0x09, 0x00, 0x00, 0x00,
                0xcd
            };

            AssertEx.Equal(new byte[] { 0xcd }, CustomDebugInfoReader.TryGetCustomDebugInfoRecord(cdi, CustomDebugInfoKind.EditAndContinueLocalSlotMap));

            // multiple records (number in global header is ignored, the first record is returned)
            cdi = new byte[]
            {
                4, 1, 0, 0, // global header
                /*version*/4, /*kind*/(byte)CustomDebugInfoKind.DynamicLocals, /*padding*/0, 0, /*size:*/ 0x09, 0x00, 0x00, 0x00,
                0xab,
                /*version*/4, /*kind*/(byte)CustomDebugInfoKind.EditAndContinueLocalSlotMap, /*padding*/0, 0, /*size:*/ 0x09, 0x00, 0x00, 0x00,
                0xcd
            };

            AssertEx.Equal(new byte[] { 0xab }, CustomDebugInfoReader.TryGetCustomDebugInfoRecord(cdi, CustomDebugInfoKind.DynamicLocals));
        }

        [Fact]
        public void UncompressSlotMap1()
        {
            using (new EnsureEnglishUICulture())
            {
                var e = Assert.Throws<InvalidDataException>(() => EditAndContinueMethodDebugInformation.Create(ImmutableArray.Create(new byte[] { 0x01, 0x68, 0xff }), ImmutableArray<byte>.Empty));
                Assert.Equal("Invalid data at offset 3: 01-68-FF*", e.Message);

                e = Assert.Throws<InvalidDataException>(() => EditAndContinueMethodDebugInformation.Create(ImmutableArray.Create(new byte[] { 0x01, 0x68, 0xff, 0xff, 0xff, 0xff }), ImmutableArray<byte>.Empty));
                Assert.Equal("Invalid data at offset 3: 01-68-FF*FF-FF-FF", e.Message);

                e = Assert.Throws<InvalidDataException>(() => EditAndContinueMethodDebugInformation.Create(ImmutableArray.Create(new byte[] { 0xff, 0xff, 0xff, 0xff }), ImmutableArray<byte>.Empty));
                Assert.Equal("Invalid data at offset 1: FF*FF-FF-FF", e.Message);

                byte[] largeData = new byte[10000];
                largeData[400] = 0xff;
                largeData[401] = 0xff;
                largeData[402] = 0xff;
                largeData[403] = 0xff;
                largeData[404] = 0xff;
                largeData[405] = 0xff;

                e = Assert.Throws<InvalidDataException>(() => EditAndContinueMethodDebugInformation.Create(ImmutableArray.Create(largeData), ImmutableArray<byte>.Empty));
                Assert.Equal(
                    "Invalid data at offset 401: 00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00-FF*FF-FF-FF-FF-FF-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
                    "00-00-00-00-00-00-00-00-00-00-00...", e.Message);
            }
        }

        [Fact]
        public void EditAndContinueLocalSlotMap_NegativeSyntaxOffsets()
        {
            var slots = ImmutableArray.Create(
                new LocalSlotDebugInfo(SynthesizedLocalKind.UserDefined, new LocalDebugId(-1, 10)),
                new LocalSlotDebugInfo(SynthesizedLocalKind.TryAwaitPendingCaughtException, new LocalDebugId(-20000, 10)));

            var closures = ImmutableArray<ClosureDebugInfo>.Empty;
            var lambdas = ImmutableArray<LambdaDebugInfo>.Empty;

            var cmw = new Cci.BlobWriter();

            new EditAndContinueMethodDebugInformation(123, slots, closures, lambdas).SerializeLocalSlots(cmw);

            var bytes = cmw.ToImmutableArray();
            AssertEx.Equal(new byte[] { 0xFF, 0xC0, 0x00, 0x4E, 0x20, 0x81, 0xC0, 0x00, 0x4E, 0x1F, 0x0A, 0x9A, 0x00, 0x0A }, bytes);

            var deserialized = EditAndContinueMethodDebugInformation.Create(bytes, default(ImmutableArray<byte>)).LocalSlots;

            AssertEx.Equal(slots, deserialized);
        }

        [Fact]
        public void EditAndContinueLambdaAndClosureMap_NegativeSyntaxOffsets()
        {
            var slots = ImmutableArray<LocalSlotDebugInfo>.Empty;

            var closures = ImmutableArray.Create(
                new ClosureDebugInfo(-100, new DebugId(0, 0)),
                new ClosureDebugInfo(10, new DebugId(1, 0)),
                new ClosureDebugInfo(-200, new DebugId(2, 0)));

            var lambdas = ImmutableArray.Create(
                new LambdaDebugInfo(20, new DebugId(0, 0), 1),
                new LambdaDebugInfo(-50, new DebugId(1, 0), 0),
                new LambdaDebugInfo(-180, new DebugId(2, 0), LambdaDebugInfo.StaticClosureOrdinal));

            var cmw = new Cci.BlobWriter();

            new EditAndContinueMethodDebugInformation(0x7b, slots, closures, lambdas).SerializeLambdaMap(cmw);

            var bytes = cmw.ToImmutableArray();

            AssertEx.Equal(new byte[] { 0x7C, 0x80, 0xC8, 0x03, 0x64, 0x80, 0xD2, 0x00, 0x80, 0xDC, 0x03, 0x80, 0x96, 0x02, 0x14, 0x01 }, bytes);

            var deserialized = EditAndContinueMethodDebugInformation.Create(default(ImmutableArray<byte>), bytes);

            AssertEx.Equal(closures, deserialized.Closures);
            AssertEx.Equal(lambdas, deserialized.Lambdas);
        }

        [Fact]
        public void EditAndContinueLambdaAndClosureMap_NoClosures()
        {
            var slots = ImmutableArray<LocalSlotDebugInfo>.Empty;

            var closures = ImmutableArray<ClosureDebugInfo>.Empty;
            var lambdas = ImmutableArray.Create(new LambdaDebugInfo(20, new DebugId(0, 0), LambdaDebugInfo.StaticClosureOrdinal));

            var cmw = new Cci.BlobWriter();

            new EditAndContinueMethodDebugInformation(-1, slots, closures, lambdas).SerializeLambdaMap(cmw);

            var bytes = cmw.ToImmutableArray();

            AssertEx.Equal(new byte[] { 0x00, 0x01, 0x00, 0x15, 0x01 }, bytes);

            var deserialized = EditAndContinueMethodDebugInformation.Create(default(ImmutableArray<byte>), bytes);

            AssertEx.Equal(closures, deserialized.Closures);
            AssertEx.Equal(lambdas, deserialized.Lambdas);
        }

        [Fact]
        public void EditAndContinueLambdaAndClosureMap_NoLambdas()
        {
            // should not happen in practice, but EditAndContinueMethodDebugInformation should handle it just fine

            var slots = ImmutableArray<LocalSlotDebugInfo>.Empty;
            var closures = ImmutableArray<ClosureDebugInfo>.Empty;
            var lambdas = ImmutableArray<LambdaDebugInfo>.Empty;

            var cmw = new Cci.BlobWriter();

            new EditAndContinueMethodDebugInformation(10, slots, closures, lambdas).SerializeLambdaMap(cmw);

            var bytes = cmw.ToImmutableArray();

            AssertEx.Equal(new byte[] { 0x0B, 0x01, 0x00 }, bytes);

            var deserialized = EditAndContinueMethodDebugInformation.Create(default(ImmutableArray<byte>), bytes);

            AssertEx.Equal(closures, deserialized.Closures);
            AssertEx.Equal(lambdas, deserialized.Lambdas);
        }

        [Fact]
        public void EncCdiAlignment()
        {
            var slots = ImmutableArray.Create(
               new LocalSlotDebugInfo(SynthesizedLocalKind.UserDefined, new LocalDebugId(-1, 10)),
               new LocalSlotDebugInfo(SynthesizedLocalKind.TryAwaitPendingCaughtException, new LocalDebugId(-20000, 10)));

            var closures = ImmutableArray.Create(
               new ClosureDebugInfo(-100, new DebugId(0, 0)),
               new ClosureDebugInfo(10, new DebugId(1, 0)),
               new ClosureDebugInfo(-200, new DebugId(2, 0)));

            var lambdas = ImmutableArray.Create(
                new LambdaDebugInfo(20, new DebugId(0, 0), 1),
                new LambdaDebugInfo(-50, new DebugId(1, 0), 0),
                new LambdaDebugInfo(-180, new DebugId(2, 0), LambdaDebugInfo.StaticClosureOrdinal));

            var debugInfo = new EditAndContinueMethodDebugInformation(1, slots, closures, lambdas);
            var records = new ArrayBuilder<Cci.BlobWriter>();

            Cci.CustomDebugInfoWriter.SerializeCustomDebugInformation(debugInfo, records);
            var cdi = Cci.CustomDebugInfoWriter.SerializeCustomDebugMetadata(records);

            Assert.Equal(2, records.Count);

            AssertEx.Equal(new byte[]
            {
                0x04, // version
                0x06, // record kind
                0x00,
                0x02, // alignment size

                // aligned record size
                0x18, 0x00, 0x00, 0x00,

                // payload (4B aligned)
                0xFF, 0xC0, 0x00, 0x4E,
                0x20, 0x81, 0xC0, 0x00,
                0x4E, 0x1F, 0x0A, 0x9A,
                0x00, 0x0A, 0x00, 0x00
            }, records[0].ToArray());

            AssertEx.Equal(new byte[]
            {
                0x04, // version
                0x07, // record kind
                0x00,
                0x00, // alignment size

                // aligned record size
                0x18, 0x00, 0x00, 0x00,

                // payload (4B aligned)
                0x02, 0x80, 0xC8, 0x03,
                0x64, 0x80, 0xD2, 0x00,
                0x80, 0xDC, 0x03, 0x80,
                0x96, 0x02, 0x14, 0x01
            }, records[1].ToArray());

            var deserialized = CustomDebugInfoReader.GetCustomDebugInfoRecords(cdi).ToArray();
            Assert.Equal(CustomDebugInfoKind.EditAndContinueLocalSlotMap, deserialized[0].Kind);
            Assert.Equal(4, deserialized[0].Version);

            Assert.Equal(new byte[]
            {
                0xFF, 0xC0, 0x00, 0x4E,
                0x20, 0x81, 0xC0, 0x00,
                0x4E, 0x1F, 0x0A, 0x9A,
                0x00, 0x0A
            }, deserialized[0].Data);

            Assert.Equal(CustomDebugInfoKind.EditAndContinueLambdaMap, deserialized[1].Kind);
            Assert.Equal(4, deserialized[1].Version);

            Assert.Equal(new byte[]
            {
                0x02, 0x80, 0xC8, 0x03,
                0x64, 0x80, 0xD2, 0x00,
                0x80, 0xDC, 0x03, 0x80,
                0x96, 0x02, 0x14, 0x01
            }, deserialized[1].Data);
        }

        [Fact]
        public void InvalidAlignment1()
        {
            // CDIs that don't support alignment:
            var bytes = new byte[]
            {
                0x04, // version
                0x01, // count
                0x00,
                0x00,

                0x04, // version
                0x06, // kind
                0x00,
                0x03, // bad alignment

                // body size
                0x0a, 0x00, 0x00, 0x00,

                // payload
                0x01, 0x00
            };

            Assert.Throws<InvalidOperationException>(() => CustomDebugInfoReader.GetCustomDebugInfoRecords(bytes).ToArray());
        }

        [Fact]
        public void InvalidAlignment2()
        {
            // CDIs that don't support alignment:
            var bytes = new byte[]
            {
                0x04, // version
                0x01, // count
                0x00,
                0x00,

                0x04, // version
                0x06, // kind
                0x00,
                0x03, // bad alignment

                // body size
                0x02, 0x00, 0x00, 0x00,

                // payload
                0x01, 0x00, 0x00, 0x06
            };

            Assert.Throws<InvalidOperationException>(() => CustomDebugInfoReader.GetCustomDebugInfoRecords(bytes).ToArray());
        }

        [Fact]
        public void InvalidAlignment_KindDoesntSupportAlignment()
        {
            // CDIs that don't support alignment:
            var bytes = new byte[]
            {
                0x04, // version
                0x01, // count
                0x00,
                0x00,

                0x04, // version
                0x01, // kind
                0x11, // invalid data
                0x14, // invalid data

                // body size
                0x0c, 0x00, 0x00, 0x00,

                // payload
                0x01, 0x00, 0x00, 0x06
            };

            var records = CustomDebugInfoReader.GetCustomDebugInfoRecords(bytes).ToArray();
            Assert.Equal(1, records.Length);

            Assert.Equal(CustomDebugInfoKind.ForwardInfo, records[0].Kind);
            Assert.Equal(4, records[0].Version);
            AssertEx.Equal(new byte[] { 0x01, 0x00, 0x00, 0x06 }, records[0].Data);
        }
    }
}
