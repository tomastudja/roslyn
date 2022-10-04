﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Serialization;
using Microsoft.VisualStudio.Threading;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Remote
{
    /// <summary>
    /// Provides solution assets present locally (in the current process) to a remote process where the solution is being replicated to.
    /// </summary>
    internal sealed class SolutionAssetProvider : ISolutionAssetProvider
    {
        public const string ServiceName = "SolutionAssetProvider";

        internal static ServiceDescriptor ServiceDescriptor { get; } = ServiceDescriptor.CreateInProcServiceDescriptor(ServiceDescriptors.ComponentName, ServiceName, suffix: "", ServiceDescriptors.GetFeatureDisplayName);

        private readonly SolutionServices _services;

        public SolutionAssetProvider(SolutionServices services)
        {
            _services = services;
        }

        public async ValueTask GetAssetsAsync(PipeWriter pipeWriter, Checksum solutionChecksum, Checksum[] checksums, CancellationToken cancellationToken)
        {
            var assetStorage = _services.GetRequiredService<ISolutionAssetStorageProvider>().AssetStorage;
            var serializer = _services.GetRequiredService<ISerializerService>();
            var scope = assetStorage.GetScope(solutionChecksum);

            SolutionAsset? singleAsset = null;
            IReadOnlyDictionary<Checksum, SolutionAsset>? assetMap = null;

            if (checksums.Length == 1)
            {
                singleAsset = await scope.GetAssetAsync(checksums[0], cancellationToken).ConfigureAwait(false);
                singleAsset ??= SolutionAsset.Null;
            }
            else
            {
                assetMap = await scope.GetAssetsAsync(checksums, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var stream = new PipeWriterStream(pipeWriter);
            await RemoteHostAssetSerialization.WriteDataAsync(
                stream, singleAsset, assetMap, serializer, scope.ReplicationContext,
                solutionChecksum, checksums, cancellationToken).ConfigureAwait(false);

            // Ensure any last data written into the stream makes it into the pipe.
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Simple port of
        /// https://github.com/AArnott/Nerdbank.Streams/blob/dafeb5846702bc29e261c9ddf60f42feae01654c/src/Nerdbank.Streams/BufferWriterStream.cs#L16.
        /// Wraps a <see cref="PipeWriter"/> in a <see cref="Stream"/> interface.  Preferred over <see
        /// cref="PipeWriter.AsStream(bool)"/> as that API produces a stream that will synchronously flush after ever
        /// write.  That's undesirable as that will then block a thread pool thread on the actual 
        /// </summary>
        private class PipeWriterStream : Stream, IDisposableObservable
        {
            private readonly PipeWriter writer;

            internal PipeWriterStream(PipeWriter writer)
            {
                this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;

            #region read/seek api (not supported)

            public override long Length => throw this.ThrowDisposedOr(new NotSupportedException());
            public override long Position
            {
                get => throw this.ThrowDisposedOr(new NotSupportedException());
                set => this.ThrowDisposedOr(new NotSupportedException());
            }

            public override int Read(byte[] buffer, int offset, int count)
                => throw this.ThrowDisposedOr(new NotSupportedException());

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => throw this.ThrowDisposedOr(new NotSupportedException());

#if !NETSTANDARD

            public override int Read(Span<byte> buffer)
                => throw this.ThrowDisposedOr(new NotSupportedException());

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
                => throw this.ThrowDisposedOr(new NotSupportedException());

#endif

            public override int ReadByte()
                => throw this.ThrowDisposedOr(new NotSupportedException());

            public override long Seek(long offset, SeekOrigin origin)
                => throw this.ThrowDisposedOr(new NotSupportedException());

            public override void SetLength(long value)
                => this.ThrowDisposedOr(new NotSupportedException());

            #endregion

            public bool IsDisposed { get; private set; }

            public override bool CanWrite => !this.IsDisposed;

            public override void Flush()
            {
                Verify.NotDisposed(this);

                // intentionally a no op. We know that we and RemoteHostAssetSerialization.WriteDataAsync will call
                // FlushAsync at appropriate times to ensure data is being sent through the writer at a reasonable
                // cadence (once per asset).
            }

            public override async Task FlushAsync(CancellationToken cancellationToken)
                => await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            public override void Write(byte[] buffer, int offset, int count)
            {
                Requires.NotNull(buffer, nameof(buffer));
                Verify.NotDisposed(this);

                var span = this.writer.GetSpan(count);
                buffer.AsSpan(offset, count).CopyTo(span);
                this.writer.Advance(count);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.Write(buffer, offset, count);
                return Task.CompletedTask;
            }

            public override void WriteByte(byte value)
            {
                Verify.NotDisposed(this);
                var span = this.writer.GetSpan(1);
                span[0] = value;
                this.writer.Advance(1);
            }

#if !NETSTANDARD

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                Verify.NotDisposed(this);
                var span = this.writer.GetSpan(buffer.Length);
                buffer.CopyTo(span);
                this.writer.Advance(buffer.Length);
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.Write(buffer.Span);
                return default;
            }

#endif

            protected override void Dispose(bool disposing)
            {
                this.IsDisposed = true;
                base.Dispose(disposing);
            }

            private Exception ThrowDisposedOr(Exception ex)
            {
                Verify.NotDisposed(this);
                throw ex;
            }
        }
    }
}
