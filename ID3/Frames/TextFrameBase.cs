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

namespace Id3.Frames
{
    /// <summary>
    ///     Represents an ID3 frame that contains textual data
    /// </summary>
    public abstract class TextFrameBase : Id3Frame
    {
        public sealed override bool Equals(Id3Frame other)
        {
            return other is TextFrameBase text &&
                text.TextValue == TextValue;
        }

        public sealed override int GetHashCode() =>
            TextValue.GetHashCode();

        public override string ToString() =>
            IsAssigned ? TextValue : string.Empty;

        public Id3TextEncoding EncodingType { get; set; }

        public override bool IsAssigned =>
            !string.IsNullOrEmpty(TextValue);

        /// <summary>
        ///     Textual representation of the frame value. This is for internal usage only; derived classes should override
        ///     the getters and setters to get and set the natively-typed value in the
        ///     <see cref="TextFrameBase{TValue}.Value" /> property.
        /// </summary>
        internal abstract string TextValue { get; set; }
    }

    [DebuggerDisplay("{" + nameof(Value) + "}")]
    public abstract class TextFrameBase<TValue> : TextFrameBase
    {
        private TValue _value;

        protected TextFrameBase()
        {
        }

        protected TextFrameBase(TValue value)
        {
            Value = value;
        }

        /// <summary>
        ///     Natively-typed value of the frame. Derived classes will override the <see cref="TextFrameBase.TextValue" /> to get
        ///     and set this value.
        /// </summary>
        public TValue Value
        {
            get => _value;
            set
            {
                ValidateValue(value);
                _value = value;
            }
        }

        /// <summary>
        ///     Deriving classes can override this method to validate the native value being set.
        ///     <para />
        ///     If the value is invalid, the method should throw an exception.
        /// </summary>
        /// <param name="value">The native value being set.</param>
        /// <exception cref="Id3Exception">Thrown if the specified native value is invalid.</exception>
        /// <remarks>
        ///     Note that in a lot of cases, a native value of null or something that translates to an empty string is considered
        ///     valid. In such cases, the frame may be unassigned, but the value should still be allowed.
        /// </remarks>
        protected virtual void ValidateValue(TValue value)
        {
        }

        public static implicit operator TValue(TextFrameBase<TValue> frame)
        {
            return frame.Value;
        }
    }
}
