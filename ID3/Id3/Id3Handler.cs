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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Id3.Frames;
using Id3.v1;
using Id3.v2;

using JetBrains.Annotations;

namespace Id3
{
    /// <summary>
    ///     Represents an ID3 handler, which can manipulate ID3 tags in an MP3 file stream.
    ///     ID3 handlers specify how to serialize/deserialize frames and write/read/delete tags from an MP3 file.
    ///     There is a derived class for each version of ID3 supported by this framework.
    /// </summary>
    internal abstract class Id3Handler
    {
        private FrameHandlers _frameHandlers;

        /// <summary>
        ///     Creates a basic tag corresponding to the version of the handler.
        /// </summary>
        /// <returns>The basic ID3 tag.</returns>
        internal Id3Tag CreateTag()
        {
            var tag = new Id3Tag {
                Version = Version,
                Family = Family
            };
            return tag;
        }

        /// <summary>
        ///     Creates a frame instance from the specified frame ID.
        ///     If there is a registered frame handler for the frame ID, it is used to instantiate the frame object. If not, an
        ///     UnknownFrame instance is created.
        /// </summary>
        /// <param name="frameId">The frame ID</param>
        /// <returns>An instance of the frame.</returns>
        internal Id3Frame GetFrameFromFrameId(string frameId)
        {
            FrameHandler handler = FrameHandlers[frameId];
            if (handler != null)
                return (Id3Frame) Activator.CreateInstance(handler.Type);
            return new UnknownFrame {
                Id = frameId
            };
        }

        /// <summary>
        ///     Retrieves the frame ID from the specified frame instance.
        /// </summary>
        /// <param name="frame">The frame instance.</param>
        /// <returns>The frame ID, or null if there is no frame handler for the specified frame instance.</returns>
        internal string GetFrameIdFromFrame(Id3Frame frame)
        {
            if (frame is UnknownFrame unknownFrame)
                return unknownFrame.Id;

            Type frameType = frame.GetType();

            FrameHandler handler = FrameHandlers[frameType];
            return handler?.FrameId;
        }

        #region Stream-manipulation overrides for the handler
        internal abstract void DeleteTag(Stream stream);
        internal abstract byte[] GetTagBytes(Stream stream);
        internal abstract bool HasTag(Stream stream);
        internal abstract Id3Tag ReadTag(Stream stream, out object additionalData);
        internal abstract bool WriteTag(Stream stream, Id3Tag tag);
        #endregion

        #region ID3 tag properties for the handler
        internal abstract Id3TagFamily Family { get; }
        internal abstract Id3Version Version { get; }
        #endregion

        /// <summary>
        ///     Override this in each derived handler to specify the valid frame types for the handler and the frame IDs to which
        ///     they correspond.
        /// </summary>
        /// <param name="mappings">
        ///     The frame handlers mapping structure. Derived classes should call the Add method for each frame
        ///     handler they want to register with this class.
        /// </param>
        protected abstract void BuildFrameHandlers(FrameHandlers mappings);

        /// <summary>
        ///     Specifies the details of each frame supported by the handler, including information on how to encode and decode
        ///     them. This structure is built by derived handlers by overridding the BuildFrameHandlers method.
        /// </summary>
        protected FrameHandlers FrameHandlers
        {
            get
            {
                if (_frameHandlers == null)
                {
                    _frameHandlers = new FrameHandlers();
                    BuildFrameHandlers(_frameHandlers);
                }

                return _frameHandlers;
            }
        }

        internal static readonly List<Id3HandlerMetadata> AvailableHandlers = new List<Id3HandlerMetadata>(4) {
            new Id3HandlerMetadata(Id3Version.V23, Id3TagFamily.Version2X, typeof(Id3V23Handler)),
            new Id3HandlerMetadata(Id3Version.V1X, Id3TagFamily.Version1X, typeof(Id3V1Handler)),
        };

        /// <summary>
        ///     Returns the ID3 tag handler for the specified tag version.
        /// </summary>
        /// <param name="version">Version of ID3 tag</param>
        /// <returns>The tag handler for the specified version or null if it is not in the collection.</returns>
        [NotNull]
        internal static Id3Handler GetHandler(Id3Version version)
        {
            Id3HandlerMetadata foundHandler = AvailableHandlers.First(ah => ah.Version == version);
            return foundHandler.Instance;
        }
    }

    internal sealed class Id3HandlerMetadata
    {
        private Id3Handler _instance;

        internal Id3HandlerMetadata(Id3Version version, Id3TagFamily family, Type type)
        {
            Version = version;
            Family = family;
            Type = type;
        }

        internal Id3Version Version { get; }

        internal Id3TagFamily Family { get; }

        internal Type Type { get; }

        internal Id3Handler Instance =>
            _instance ?? (_instance = (Id3Handler) Activator.CreateInstance(Type));
    }
}
