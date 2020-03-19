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
using System.Text.RegularExpressions;
using Id3.Resources;

namespace Id3.Frames
{
    public sealed class CopyrightFrame : TextFrame
    {
        public CopyrightFrame()
        {
        }

        public CopyrightFrame(string value) : base(value)
        {
        }

        public override string ToString()
        {
            return IsAssigned ? $"Copyright © {Value}" : string.Empty;
        }

        protected override void ValidateValue(string value)
        {
            if (!string.IsNullOrEmpty(value) && !CopyrightPrefixPattern.IsMatch(value))
                throw new ArgumentException(FrameMessages.Copyright_InvalidFormat, nameof(value));
        }

        internal override string TextValue
        {
            get => base.TextValue;
            set => base.TextValue = !string.IsNullOrEmpty(value) && !CopyrightPrefixPattern.IsMatch(value) ? null : value;
        }

        private static readonly Regex CopyrightPrefixPattern = new Regex(@"^\d{4} ");

        public static implicit operator CopyrightFrame(string value) => new CopyrightFrame(value);
    }
}