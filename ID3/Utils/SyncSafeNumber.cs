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

namespace Id3
{
    internal static class SyncSafeNumber
    {
        internal static int DecodeNormal(byte[] bytes, int start, int count)
        {
            int size = 0, shift = 0;
            for (int byteIdx = start + count - 1; byteIdx >= start; byteIdx--)
            {
                size += bytes[byteIdx] << shift;
                shift += 8;
            }
            return size;
        }

        internal static int DecodeSafe(byte[] bytes, int start, int count)
        {
            int size = 0, shift = 0;
            for (int byteIdx = start + count - 1; byteIdx >= start; byteIdx--)
            {
                size += bytes[byteIdx] << shift;
                shift += 7;
            }
            return size;
        }

        internal static byte[] EncodeNormal(int size)
        {
            var bytes = new byte[4];
            bytes[3] = (byte)(size & 0xFF);
            bytes[2] = (byte)((size >> 8) & 0xFF);
            bytes[1] = (byte)((size >> 16) & 0xFF);
            bytes[0] = (byte)((size >> 24) & 0xFF);
            return bytes;
        }

        internal static byte[] EncodeSafe(int size)
        {
            var bytes = new byte[4];
            bytes[3] = (byte)(size & 0x7F);
            bytes[2] = (byte)((size >> 7) & 0x7F);
            bytes[1] = (byte)((size >> 14) & 0x7F);
            bytes[0] = (byte)((size >> 21) & 0x7F);
            return bytes;
        }
    }
}