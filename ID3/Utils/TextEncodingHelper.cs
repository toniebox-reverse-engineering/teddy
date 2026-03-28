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

using System.Diagnostics;
using System.Text;

namespace Id3
{
    internal static class TextEncodingHelper
    {
        //Gets the default encoding, which is ISO-8859-1
        internal static Encoding GetDefaultEncoding()
        {
            return Encoding.GetEncoding("iso-8859-1");
        }

        internal static string GetDefaultString(byte[] bytes, int start, int count)
        {
            return GetDefaultEncoding().GetString(bytes, start, count);
        }

        internal static Encoding GetEncoding(Id3TextEncoding encodingType)
        {
            if (encodingType == Id3TextEncoding.Iso8859_1)
                return Encoding.GetEncoding("iso-8859-1");
            if (encodingType == Id3TextEncoding.Unicode)
                return Encoding.Unicode;
            if (encodingType == Id3TextEncoding.UnicodeBE)
                return Encoding.BigEndianUnicode;
            if (encodingType == Id3TextEncoding.UTF8)
                return Encoding.UTF8;
            Debug.Assert(false, "Invalid Encoding type specified");
            return null;
        }

        internal static string GetString(byte[] bytes, int start, int count, Id3TextEncoding encodingType)
        {
            Encoding encoding = GetEncoding(encodingType);
            string str = encoding.GetString(bytes, start, count);

            if (encodingType == Id3TextEncoding.Unicode || encodingType == Id3TextEncoding.UnicodeBE)
            {
                if (str[0] == '\xFFFE' || str[0] == '\xFEFF')
                    str = str.Remove(0, 1);
            }

            return str;
        }

        internal static string[] GetSplitStrings(byte[] bytes, int start, int count, Id3TextEncoding encodingType)
        {
            byte[][] splitBytes = ByteArrayHelper.SplitBySequence(bytes, start, count, GetSplitterBytes(encodingType));
            if (splitBytes.Length == 0)
                return new[] { string.Empty };

            var strings = new string[splitBytes.Length];
            for (int splitByteIdx = 0; splitByteIdx < splitBytes.Length; splitByteIdx++)
                strings[splitByteIdx] = GetString(splitBytes[splitByteIdx], 0, splitBytes[splitByteIdx].Length, encodingType);
            return strings;
        }

        internal static byte[] GetSplitterBytes(Id3TextEncoding encodingType)
        {
            var splitterBytes = new byte[GetSplitterLength(encodingType)];
            return splitterBytes;
        }

        private static int GetSplitterLength(Id3TextEncoding encodingType)
        {
            if (encodingType == Id3TextEncoding.Iso8859_1)
                return 1;
            if (encodingType == Id3TextEncoding.UTF8)
                return 1;
            if (encodingType == Id3TextEncoding.Unicode)
                return 2;
            if (encodingType == Id3TextEncoding.UnicodeBE)
                return 2;
            Debug.Assert(false, "Invalid encoding type specified");
            return -1;
        }
    }
}