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
using Id3.Frames;

namespace Id3
{
    /// <summary>
    ///     Represents the details of a frame and how it can be encoded or decoded.
    ///     Handlers use this information to process frames.
    /// </summary>
    internal sealed class FrameHandler
    {
        /// <summary>
        ///     Initializes an instance of the <see cref="FrameHandler"/> class.
        /// </summary>
        /// <param name="frameId">The ID of the frame.</param>
        /// <param name="type">Type of the <see cref="Id3Frame"/>.</param>
        /// <param name="encoder">Delegate to encode a <see cref="Id3Frame"/> into a byte array.</param>
        /// <param name="decoder">Delegate to decode a byte array into a <see cref="Id3Frame"/>.</param>
        internal FrameHandler(string frameId, Type type, Func<Id3Frame, byte[]> encoder, Func<byte[], Id3Frame> decoder)
        {
            FrameId = frameId;
            Type = type;
            Encoder = encoder;
            Decoder = decoder;
        }

        /// <summary>
        ///     The ID of the frame.
        /// </summary>
        internal string FrameId { get; }

        /// <summary>
        ///     Type of the <see cref="Id3Frame"/>.
        /// </summary>
        internal Type Type { get; }

        /// <summary>
        ///     Delegate to encode a <see cref="Id3Frame"/> into a byte array.
        /// </summary>
        internal Func<Id3Frame, byte[]> Encoder { get; }

        /// <summary>
        ///     Delegate to decode a byte array into a <see cref="Id3Frame"/>.
        /// </summary>
        internal Func<byte[], Id3Frame> Decoder { get; }
    }

    internal sealed class FrameHandlers : Collection<FrameHandler>
    {
        /// <summary>
        ///     Shortcut method to add a <see cref="FrameHandler"/> instance to the collection.
        /// </summary>
        /// <typeparam name="TFrame">The type of the <see cref="Id3Frame"/></typeparam>
        /// <param name="frameId">The ID of the frame.</param>
        /// <param name="encoder">Delegate to encode a <see cref="Id3Frame"/> into a byte array.</param>
        /// <param name="decoder">Delegate to decode a byte array into a <see cref="Id3Frame"/>.</param>
        internal void Add<TFrame>(string frameId, Func<Id3Frame, byte[]> encoder, Func<byte[], Id3Frame> decoder)
            where TFrame : Id3Frame
        {
            Add(new FrameHandler(frameId, typeof(TFrame), encoder, decoder));
        }

        /// <summary>
        ///     Returns a <see cref="FrameHandler"/> based on the specified <paramref name="frameId"/>.
        /// </summary>
        /// <param name="frameId">The ID of the frame.</param>
        /// <returns>A <see cref="FrameHandler"/> instance that matches the specified <paramref name="frameId"/>.</returns>
        internal FrameHandler this[string frameId] =>
            this.FirstOrDefault(mapping => mapping.FrameId == frameId);

        /// <summary>
        ///     Returns a <see cref="FrameHandler"/> based on the specified frame type.
        /// </summary>
        /// <param name="type">The type of the frame.</param>
        /// <returns>A <see cref="FrameHandler"/> instance that matches the specified <paramref name="type"/>.</returns>
        internal FrameHandler this[Type type] =>
            this.FirstOrDefault(mapping => mapping.Type == type);
    }
}
