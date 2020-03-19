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
    public sealed class CustomUrlLinkFrame : UrlLinkFrame
    {
        public CustomUrlLinkFrame()
        {
        }

        public CustomUrlLinkFrame([NotNull] string url) : base(url)
        {
        }

        public CustomUrlLinkFrame([NotNull] string url, [NotNull] string description) : base(url)
        {
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        public string Description { get; set; }

        public Id3TextEncoding EncodingType { get; set; }

        public static implicit operator CustomUrlLinkFrame(string url) => new CustomUrlLinkFrame(url);
    }
}