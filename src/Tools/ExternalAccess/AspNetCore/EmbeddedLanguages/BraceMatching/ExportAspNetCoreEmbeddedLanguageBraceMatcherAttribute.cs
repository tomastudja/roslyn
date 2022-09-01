﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;

namespace Microsoft.CodeAnalysis.ExternalAccess.AspNetCore.EmbeddedLanguages
{
    /// <summary>
    /// Use this attribute to export a <see cref="IAspNetCoreEmbeddedLanguageBraceMatcher"/>.
    /// </summary>
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class ExportAspNetCoreEmbeddedLanguageBraceMatcherAttribute : ExportAttribute
    {
        /// <summary>
        /// Name of the brace matcher.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Name of the containing language hosting the embedded language.  e.g. C# or VB.
        /// </summary>
        public string Language { get; }

        public ExportAspNetCoreEmbeddedLanguageBraceMatcherAttribute(
            string name, string language)
            : base(typeof(IAspNetCoreEmbeddedLanguageBraceMatcher))
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Language = language ?? throw new ArgumentNullException(nameof(language));
        }
    }
}
