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
using JetBrains.Annotations;

namespace Id3.Frames
{
    public sealed class LyricsFrame : Id3Frame
    {
        public LyricsFrame()
        {
        }

        public LyricsFrame([NotNull] string lyrics)
        {
            Lyrics = lyrics ?? throw new ArgumentNullException(nameof(lyrics));
        }

        public LyricsFrame([NotNull] string lyrics, [NotNull] string description)
        {
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Lyrics = lyrics ?? throw new ArgumentNullException(nameof(lyrics));
        }

        public override bool Equals(Id3Frame other)
        {
            return other is LyricsFrame lyricsFrame &&
                lyricsFrame.Language == Language &&
                lyricsFrame.Description == Description;
        }

        public string Description { get; set; }

        public Id3TextEncoding EncodingType { get; set; }

        public override bool IsAssigned => !string.IsNullOrEmpty(Lyrics);

        public Id3Language Language { get; set; } = Id3Language.eng;

        public string Lyrics { get; set; }

        public static implicit operator LyricsFrame(string lyrics) => new LyricsFrame(lyrics);
    }

    public sealed class LyricsFrameList : Collection<LyricsFrame>
    {
        public void Add(string lyrics, string description)
        {
            Add(new LyricsFrame(lyrics, description));
        }
    }
}