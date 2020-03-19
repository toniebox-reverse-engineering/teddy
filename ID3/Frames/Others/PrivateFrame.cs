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

namespace Id3.Frames
{
    public sealed class PrivateFrame : Id3Frame
    {
        public override bool Equals(Id3Frame other)
        {
            return other is PrivateFrame privateFrame &&
                privateFrame.OwnerId == OwnerId &&
                ByteArrayHelper.AreEqual(privateFrame.Data, Data);
        }

        public byte[] Data { get; set; }

        public string OwnerId { get; set; }

        public override bool IsAssigned => Data != null && Data.Length > 0;

        public override string ToString() => OwnerId ?? base.ToString();
    }

    public sealed class PrivateFrameList : Collection<PrivateFrame>
    {
        public PrivateFrame[] ByOwnerId(string ownerId)
        {
            return this.Where(frame => frame.OwnerId.Equals(ownerId, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
    }
}