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

//http://www.devhood.com/tutorials/tutorial_details.aspx?tutorial_id=79

using System;

namespace Id3
{
    internal sealed class AudioStream
    {
        private readonly byte[] _audioStream;

        private ulong _bitHeader;
        private bool _isVariableBitrate;
        private int _variableFrames;

        internal AudioStream(byte[] audioStream)
        {
            _audioStream = audioStream;
        }

        internal AudioStreamProperties Calculate()
        {
            var position = 0;
            bool isValidHeader;
            do
            {
                _bitHeader =
                    (ulong)
                        (((_audioStream[position] & 255) << 24) | ((_audioStream[position + 1] & 255) << 16) |
                            ((_audioStream[position + 2] & 255) << 8) | ((_audioStream[position + 3] & 255)));
                position++;
                isValidHeader = IsValidHeader;
            } while (position < _audioStream.Length - 4 && !isValidHeader);

            if (!isValidHeader)
                throw new Id3Exception("Invalid header format for MP3 audio stream");

            position += 3;

            int modeIndex = ModeIndex;
            if (VersionIndex == 3)
                position += modeIndex == 3 ? 17 : 19;
            else
                position += modeIndex == 3 ? 9 : 17;

            var variableBitrateHeader = new byte[12];
            Array.Copy(_audioStream, position, variableBitrateHeader, 0, 12);
            CheckVariableBitrateHeader(variableBitrateHeader);

            return new AudioStreamProperties(Bitrate, Frequency, Duration, AudioMode);
        }

        private void CheckVariableBitrateHeader(byte[] header)
        {
            _isVariableBitrate = false;

            if (header[0] != 88 || header[1] != 105 || header[2] != 110 || header[3] != 103)
                return;

            int flags = (((header[4] & 255) << 24) | ((header[5] & 255) << 16) | ((header[6] & 255) << 8) | ((header[7] & 255)));

            if ((flags & 0x0001) == 1)
            {
                _variableFrames = (((header[8] & 255) << 24) | ((header[9] & 255) << 16) | ((header[10] & 255) << 8) | ((header[11] & 255)));
                _isVariableBitrate = true;
            } else
                _variableFrames = -1;
        }

        private bool IsValidHeader => (FrameSync & 2047) == 2047 && (VersionIndex & 3) != 1 && (LayerIndex & 3) != 0 && (BitrateIndex & 15) != 0 &&
            (BitrateIndex & 15) != 15 && (FrequencyIndex & 3) != 3 && (EmphasisIndex & 3) != 2;

        #region Audio properties
        private AudioMode AudioMode
        {
            get
            {
                switch (ModeIndex)
                {
                    case 1:
                        return AudioMode.JointStereo;
                    case 2:
                        return AudioMode.DualChannel;
                    case 3:
                        return AudioMode.SingleChannel;
                    default:
                        return AudioMode.Stereo;
                }
            }
        }

        private static readonly int[,,] BitrateLookup = {
            {
                // MPEG 2 & 2.5
                { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 }, // Layer III
                { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 }, // Layer II
                { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0 } // Layer I
            }, {
                // MPEG 1
                { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 }, // Layer III
                { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 0 }, // Layer II
                { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 0 } // Layer I
            }
        };

        private int Bitrate
        {
            get
            {
                if (_isVariableBitrate)
                {
                    double medFrameSize = (double)_audioStream.Length / _variableFrames;
                    return (int)((medFrameSize * Frequency) / (1000.0 * ((LayerIndex == 3) ? 12.0 : 144.0)));
                }
                return BitrateLookup[VersionIndex & 1, LayerIndex - 1, BitrateIndex];
            }
        }

        private TimeSpan Duration
        {
            get
            {
                int kilobitFileSize = ((8 * _audioStream.Length) / 1000);
                int seconds = (kilobitFileSize / Bitrate);
                return TimeSpan.FromSeconds(seconds);
            }
        }

        private static readonly int[,] FrequencyLookup = {
            { 32000, 16000, 8000 }, // MPEG 2.5
            { 0, 0, 0 }, // reserved
            { 22050, 24000, 16000 }, // MPEG 2
            { 44100, 48000, 32000 } // MPEG 1
        };

        private int Frequency => FrequencyLookup[VersionIndex, FrequencyIndex];
        #endregion

        #region BitHeader calculated values
        private int BitrateIndex => (int)((_bitHeader >> 12) & 15);

        private int EmphasisIndex => (int)(_bitHeader & 3);

        private int FrameCount
        {
            get
            {
                if (!_isVariableBitrate)
                {
                    double medFrameSize = (((LayerIndex == 3) ? 12 : 144) * ((1000.0 * Bitrate) / Frequency));
                    return (int)(_audioStream.Length / medFrameSize);
                }
                return _variableFrames;
            }
        }

        private int FrameSync => (int)((_bitHeader >> 21) & 2047);

        private int FrequencyIndex => (int)((_bitHeader >> 10) & 3);

        private int LayerIndex => (int)((_bitHeader >> 17) & 3);

        private int ModeIndex => (int)((_bitHeader >> 6) & 3);

        private int VersionIndex => (int)((_bitHeader >> 19) & 3);
        #endregion
    }
}