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

using System.Collections.Generic;
using System.Linq;

namespace Id3.Frames
{
    public class FileTypeFrame : TextFrameBase<FileAudioType>
    {
        public FileTypeFrame()
        {
        }

        public FileTypeFrame(FileAudioType value) : base(value)
        {
        }

        internal override string TextValue
        {
            get
            {
                KeyValuePair<string, FileAudioType> mappingEntry = FileAudioTypeMapping.FirstOrDefault(kvp => kvp.Value == Value);
                return mappingEntry.Key ?? "MPG";
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    Value = FileAudioType.Mpeg;
                else
                    Value = FileAudioTypeMapping.TryGetValue(value, out FileAudioType audioType)
                        ? audioType : FileAudioType.Mpeg;
            }
        }

        private static readonly Dictionary<string, FileAudioType> FileAudioTypeMapping = new Dictionary<string, FileAudioType>(8) {
            ["MPG"] = FileAudioType.Mpeg,
            ["MPG/1"] = FileAudioType.Mpeg_1_2_Layer1,
            ["MPG/2"] = FileAudioType.Mpeg_1_2_Layer2,
            ["MPG/3"] = FileAudioType.Mpeg_1_2_Layer3,
            ["MPG/2.5"] = FileAudioType.Mpeg_2_5,
            ["MPG/AAC"] = FileAudioType.Mpeg_Aac,
            ["VQF"] = FileAudioType.Vqf,
            ["PCM"] = FileAudioType.Pcm
        };

        public static implicit operator FileTypeFrame(FileAudioType value) => new FileTypeFrame(value);
    }

    public enum FileAudioType
    {
        Mpeg,
        Mpeg_1_2_Layer1,
        Mpeg_1_2_Layer2,
        Mpeg_1_2_Layer3,
        Mpeg_2_5,
        Mpeg_Aac,
        Vqf,
        Pcm,
    }
}