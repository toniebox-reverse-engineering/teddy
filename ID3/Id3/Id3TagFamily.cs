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
    /// <summary>
    ///     Specifies families of ID3 tag versions that are mutually-inclusive.
    /// </summary>
    public enum Id3TagFamily : byte
    {
        /// <summary>
        ///     Indicates ID3 tags in the v2 range (currently v2.2, v2.3 and v2.4). These tags appear at
        ///     the beginning of the MP3 file.
        /// </summary>
        Version2X,

        /// <summary>
        ///     Indicates ID3 tags in the v1 range (currently v1.0 and v1.1). These tags appear at the end
        ///     of the MP3 file.
        /// </summary>
        Version1X
    }
}
