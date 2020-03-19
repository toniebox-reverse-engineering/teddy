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

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Id3.Frames
{
    //TODO: Instead of deriving from TextFrameBase<T> and having a Value property of type IList<string>,
    //have the ListTextFrame class implement IList<string> and get rid of the Value property.
    public abstract class ListTextFrame : TextFrameBase<IList<string>>
    {
        private const string Separator = "/";

        protected ListTextFrame()
        {
            Value = new List<string>();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Value = new List<string>();
        }

        public new IList<string> Value { get; private set; }

        public override bool IsAssigned => Value.Any(v => !string.IsNullOrWhiteSpace(v));

        internal sealed override string TextValue
        {
            get => string.Join(Separator, Value.Where(v => !string.IsNullOrWhiteSpace(v)));
            set
            {
                if (string.IsNullOrEmpty(value))
                    Value.Clear();
                else
                {
                    string[] breakup = value.Split(Separator[0]);
                    foreach (string s in breakup)
                    {
                        if (!string.IsNullOrWhiteSpace(s))
                            Value.Add(s);
                    }
                }
            }
        }
    }
}