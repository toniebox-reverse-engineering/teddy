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

namespace Id3.Frames
{
    public sealed class UnknownFrame : Id3Frame
    {
        public override bool Equals(Id3Frame other)
        {
            if (base.Equals(other))
                return true;
            if (!(other is UnknownFrame unknownFrame))
                return false;
            if (Id != unknownFrame.Id)
                return false;
            return ByteArrayHelper.AreEqual(Data, unknownFrame.Data);
        }

        public override string ToString() => Id ?? base.ToString();

        public string Id { get; set; }

        public byte[] Data { get; internal set; }

        public override bool IsAssigned => Data != null && Data.Length > 0;
    }
}