#region --- License & Copyright Notice ---
/*
Copyright (c) 2005-2019 Jeevan James
All rights reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/
#endregion

using System;
using System.Collections.ObjectModel;
using System.Linq;
using JetBrains.Annotations;

namespace Id3.Frames
{
    public sealed class CommentFrame : Id3Frame
    {
        public CommentFrame()
        {
        }

        public CommentFrame([NotNull] string comment)
        {
            Comment = comment ?? throw new ArgumentNullException(nameof(comment));
        }

        public CommentFrame([NotNull] string comment, [NotNull] string description)
        {
            Comment = comment ?? throw new ArgumentNullException(nameof(comment));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        public override bool Equals(Id3Frame other)
        {
            return other is CommentFrame comment &&
                comment.Language == Language &&
                comment.Description == Description;
        }

        public override string ToString()
        {
            return Comment ?? base.ToString();
        }

        public string Comment { get; set; }

        public string Description { get; set; }

        public Id3TextEncoding EncodingType { get; set; }

        public override bool IsAssigned => !string.IsNullOrEmpty(Comment);

        public Id3Language Language { get; set; } = Id3Language.eng;

        public static implicit operator CommentFrame(string comment) => new CommentFrame(comment);
    }

    public sealed class CommentFrameList : Collection<CommentFrame>
    {
        public CommentFrame[] ByLanguage(Id3Language language)
        {
            return this.Where(commentFrame => commentFrame.Language == language).ToArray();
        }

        public CommentFrame[] ByDescription(string description)
        {
            return this.Where(frame => frame.Description.Equals(description, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        public CommentFrame ByLanguageAndDescription(Id3Language language, string description)
        {
            return this.FirstOrDefault(frame =>
                frame.Language == language &&
                frame.Description.Equals(description, StringComparison.OrdinalIgnoreCase));
        }
    }
}