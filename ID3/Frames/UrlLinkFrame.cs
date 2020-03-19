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
using JetBrains.Annotations;

namespace Id3.Frames
{
    public abstract class UrlLinkFrame : Id3Frame
    {
        protected UrlLinkFrame()
        {
        }

        protected UrlLinkFrame([NotNull] string url)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
        }

        public override bool Equals(Id3Frame other)
        {
            return base.Equals(other) &&
                other is UrlLinkFrame urlLink &&
                Url == urlLink.Url;
        }

        public sealed override string ToString()
        {
            return IsAssigned ? Url : string.Empty;
        }

        public sealed override bool IsAssigned => !string.IsNullOrEmpty(Url);

        public string Url { get; set; }

        public static implicit operator string(UrlLinkFrame frame) => frame.Url;
    }
}
