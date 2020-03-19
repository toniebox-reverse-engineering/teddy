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
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Id3.Frames
{
    [DebuggerDisplay("Track {Value} of {TrackCount}")]
    public sealed class TrackFrame : TextFrameBase<int>
    {
        public TrackFrame()
        {
        }

        public TrackFrame(int value) : base(value)
        {
        }

        public TrackFrame(int value, int trackCount) : base(value)
        {
            TrackCount = trackCount;
        }

        /// <summary>
        ///     The total number of tracks.
        ///     <para />
        ///     If greater than 0, the ID3 value will be set as &lt;track&gt;/&lt;track count&gt;
        /// </summary>
        public int TrackCount { get; set; }

        /// <summary>
        ///     Indicates whether to zero-pad the track and track count values. This is useful for some MP3 players that
        ///     incorrectly sort unpadded values such as 1 and 10.
        ///     <para />
        ///     If this value is null, then no padding is applied.
        ///     <para />
        ///     If this value is 0 (zero), then the track value is padded based on the length of the track count value.
        ///     <para />
        ///     If this value is greater than 0, it is used to pad the track and track count values.
        /// </summary>
        public int? Padding { get; set; }

        internal override string TextValue
        {
            get
            {
                if (Value <= 0 && TrackCount <= 0)
                    return null;

                string track = null, trackCount = null;
                if (TrackCount > 0)
                    trackCount = TrackCount.ToString().PadLeft(Padding.GetValueOrDefault(), '0');
                if (Value > 0)
                {
                    track = Value.ToString();
                    if (Padding.HasValue)
                        track = track.PadLeft(Padding.Value <= 0 ? (trackCount ?? "").Length : Padding.Value, '0');
                }

                if (track == null)
                    return null;
                string result = track;
                if (trackCount != null)
                    result += $"/{trackCount}";
                return result;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Value = 0;
                    TrackCount = 0;
                    return;
                }

                Match match = TrackPattern.Match(value);
                if (!match.Success)
                {
                    Value = 0;
                    TrackCount = 0;
                    return;
                }

                string trackCount = match.Groups[2].Value;
                if (string.IsNullOrEmpty(trackCount))
                    TrackCount = 0;
                else
                {
                    TrackCount = int.Parse(trackCount);
                    if (trackCount.StartsWith("0"))
                        Padding = trackCount.Length;
                }

                string track = match.Groups[1].Value;
                Value = int.Parse(track);
                if (track.StartsWith("0"))
                    Padding = Math.Max(track.Length, trackCount.Length);
            }
        }

        private static readonly Regex TrackPattern = new Regex(@"^(\d+)(?:/(\d+))?$");

        public static implicit operator TrackFrame(int value) => new TrackFrame(value);
    }
}
