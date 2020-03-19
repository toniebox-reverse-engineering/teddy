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

namespace Id3
{
    public sealed class AudioStreamProperties
    {
        public AudioStreamProperties(int bitrate, int frequency, TimeSpan duration, AudioMode mode)
        {
            Bitrate = bitrate;
            Frequency = frequency;
            Duration = duration;
            Mode = mode;
        }

        public int Bitrate { get; }

        public TimeSpan Duration { get; }

        public int Frequency { get; }

        public AudioMode Mode { get; }
    }
}