﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Remote;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Classification
{
    internal interface IRemoteSemanticClassificationService
    {
        Task<SerializableClassifiedSpans> GetSemanticClassificationsAsync(
            PinnedSolutionInfo solutionInfo, DocumentId documentId, TextSpan span, bool isFullyLoaded, CancellationToken cancellationToken);
    }

    /// <summary>
    /// For space efficiency, we encode classified spans as triples of ints in one large array.  The
    /// first int is the index of classification type in <see cref="ClassificationTypes"/>, and the
    /// second and third ints encode the span.
    /// </summary>
    internal sealed class SerializableClassifiedSpans
    {
        public List<string> ClassificationTypes;
        public List<int> ClassificationTriples;

        internal static SerializableClassifiedSpans Dehydrate(ArrayBuilder<ClassifiedSpan> classifiedSpans)
        {
            using var _ = PooledDictionary<string, int>.GetInstance(out var classificationTypeToId);
            return Dehydrate(classifiedSpans, classificationTypeToId);
        }

        private static SerializableClassifiedSpans Dehydrate(ArrayBuilder<ClassifiedSpan> classifiedSpans, Dictionary<string, int> classificationTypeToId)
        {
            var classificationTypes = new List<string>();
            var classificationTriples = new List<int>(capacity: classifiedSpans.Count * 3);

            foreach (var classifiedSpan in classifiedSpans)
            {
                var type = classifiedSpan.ClassificationType;
                if (!classificationTypeToId.TryGetValue(type, out var id))
                {
                    id = classificationTypes.Count;
                    classificationTypes.Add(type);
                    classificationTypeToId.Add(type, id);
                }

                var textSpan = classifiedSpan.TextSpan;
                classificationTriples.Add(id);
                classificationTriples.Add(textSpan.Start);
                classificationTriples.Add(textSpan.Length);
            }

            return new SerializableClassifiedSpans
            {
                ClassificationTypes = classificationTypes,
                ClassificationTriples = classificationTriples,
            };
        }

        internal void Rehydrate(List<ClassifiedSpan> classifiedSpans)
        {
            for (var i = 0; i < ClassificationTriples.Count; i += 3)
            {
                classifiedSpans.Add(new ClassifiedSpan(
                    ClassificationTypes[ClassificationTriples[i + 0]],
                    new TextSpan(
                        ClassificationTriples[i + 1],
                        ClassificationTriples[i + 2])));
            }
        }
    }
}
