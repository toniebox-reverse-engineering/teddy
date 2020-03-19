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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Id3.Frames;

namespace Id3
{
    /// <summary>
    ///     Represents an ID3 tag.
    ///     <para />
    ///     This class is agnostic of any ID3 tag version. It contains all the possible properties that can be assigned across
    ///     all ID3 tag versions.
    /// </summary>
    public sealed class Id3Tag : IEnumerable<Id3Frame>, IComparable<Id3Tag>, IEquatable<Id3Tag>
    {
        public Id3Tag()
        {
            _frames = new Dictionary<Type, object>(50);
        }

        [OnDeserializing]
        internal void OnDeserializing(StreamingContext context)
        {
            _frames = new Dictionary<Type, object>(50);
        }

        /// <summary>
        ///     Converts an ID3 tag to another version after resolving the differences between the two versions. The resultant tag
        ///     will have all the frames from the source tag, but those frames not recognized in the new version will be treated as
        ///     UnknownFrame objects.
        ///     Similarly, frames recognized in the output tag version, but not in the source version are converted accordingly.
        /// </summary>
        /// <param name="version">Version of the tag to convert to.</param>
        /// <returns>The converted tag of the specified version, or null if there were any errors.</returns>
        public Id3Tag ConvertTo(Id3Version version)
        {
            //If the requested version is the same as this version, just return the same instance.
            if (Version == version)
                return this;

            //Get the ID3 tag handlers for the destination and create a empty tag
            var destinationHandler = Id3Handler.GetHandler(version);
            Id3Tag destinationTag = destinationHandler.CreateTag();

            foreach (Id3Frame sourceFrame in this)
            {
                if (sourceFrame is UnknownFrame unknownFrame)
                {
                    string frameId = unknownFrame.Id;
                    Id3Frame destinationFrame = destinationHandler.GetFrameFromFrameId(frameId);
                    destinationTag.AddUntypedFrame(destinationFrame);
                }
                else
                    destinationTag.AddUntypedFrame(sourceFrame);
            }

            return destinationTag;
        }

        #region Metadata properties
        //TODO: Since Id3Tag is supposed to be version-agnostic, should it contain these properties?

        /// <summary>
        ///     Version family of the ID3 tag - 1.x or 2.x
        /// </summary>
        public Id3TagFamily Family { get; internal set; }

        /// <summary>
        ///     Version of the ID3 tag
        /// </summary>
        public Id3Version Version { get; internal set; }
        #endregion

        #region Frame operations
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<Id3Frame>)this).GetEnumerator();
        }

        IEnumerator<Id3Frame> IEnumerable<Id3Frame>.GetEnumerator()
        {
            foreach (KeyValuePair<Type, object> kvp in Frames)
            {
                if (kvp.Value is IList list)
                {
                    foreach (Id3Frame frame in list)
                        yield return frame;
                }
                else
                    yield return (Id3Frame)kvp.Value;
            }
        }

        /// <summary>
        ///     Removes all unassigned frames from the tag.
        /// </summary>
        public void Cleanup()
        {
            //Build a list of keys from the Frames dictionary to delete
            var keysToDelete = new List<Type>(Frames.Count);

            Parallel.ForEach(Frames, (kvp, state) =>
            {
                //If the item value is a list, remove any item that is not assigned
                if (kvp.Value is IList frameList)
                {
                    for (int i = frameList.Count - 1; i >= 0; i--)
                    {
                        var frame = (Id3Frame)frameList[i];
                        if (!frame.IsAssigned)
                            frameList.RemoveAt(i);
                    }

                    //If the list is empty, mark the item for removal
                    if (frameList.Count == 0)
                        keysToDelete.Add(kvp.Key);
                }
                else
                {
                    //Get the item value as a frame and mark it for removal if it is unassigned
                    var frame = (Id3Frame)kvp.Value;
                    if (!frame.IsAssigned)
                        keysToDelete.Add(kvp.Key);
                }
            });

            foreach (Type keyToDelete in keysToDelete)
                Frames.Remove(keyToDelete);
        }

        /// <summary>
        ///     Removes all frames from the tag.
        /// </summary>
        /// <returns>The number of frames removed.</returns>
        public int Clear()
        {
            int clearedCount = this.Count();
            Frames.Clear();
            return clearedCount;
        }

        public bool Contains<TFrame>(Expression<Func<Id3Tag, TFrame>> frameProperty)
            where TFrame : Id3Frame
        {
            if (frameProperty == null)
                throw new ArgumentNullException(nameof(frameProperty));

            var lambda = (LambdaExpression)frameProperty;
            var memberExpression = (MemberExpression)lambda.Body;
            var property = (PropertyInfo)memberExpression.Member;
            return this.Any(f => f.GetType() == property.PropertyType && f.IsAssigned);
        }

        /// <summary>
        ///     Returns the total number of frames in this tag.
        /// </summary>
        /// <param name="onlyAssignedFrames">If true, counts only assigned frames.</param>
        /// <returns>Total number of frames in the tag.</returns>
        public int GetCount(bool onlyAssignedFrames = true)
        {
            int count = 0;
            foreach (KeyValuePair<Type, object> kvp in Frames)
            {
                if (kvp.Value is IList list)
                    count += onlyAssignedFrames ? list.Cast<Id3Frame>().Count(frame => frame.IsAssigned) : list.Count;
                else
                    count += onlyAssignedFrames ? (((Id3Frame)kvp.Value).IsAssigned ? 1 : 0) : 1;
            }
            return count;
        }

        /// <summary>
        ///     Removes any frames of the specified type from the tag.
        /// </summary>
        /// <typeparam name="TFrame">Type of frame to remove</typeparam>
        /// <returns>True, if matching frames were removed, otherwise false.</returns>
        public bool Remove<TFrame>()
            where TFrame : Id3Frame
        {
            Type frameType = typeof(TFrame);

            if (!Frames.ContainsKey(frameType))
                return false;

            Frames.Remove(frameType);
            return true;
        }

        /// <summary>
        ///     Removes all frames of a specific type from the tag. A predicate can be optionally specified to control the frames
        ///     that are removed.
        /// </summary>
        /// <typeparam name="TFrame">Type of frame to remove.</typeparam>
        /// <param name="predicate">Optional predicate to control the frames that are removed</param>
        /// <returns>The number of frames removed.</returns>
        public int RemoveWhere<TFrame>(Func<TFrame, bool> predicate)
            where TFrame : Id3Frame
        {
            Type frameType = typeof(TFrame);

            if (!Frames.ContainsKey(frameType))
                return 0;

            object frameObj = Frames[frameType];
            int removalCount = 0;
            if (frameObj is IList list)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (predicate((TFrame)list[i]))
                    {
                        list.RemoveAt(i);
                        removalCount++;
                    }
                }

                if (list.Count == 0)
                    Frames.Remove(frameType);
            }
            else
            {
                var frame = (TFrame)frameObj;
                if (predicate(frame))
                {
                    Frames.Remove(frameType);
                    removalCount++;
                }
            }

            return removalCount;
        }
        #endregion

        #region Frame properties
        public AlbumFrame Album
        {
            get => GetSingleFrame<AlbumFrame>();
            set => SetSingleFrame(value);
        }

        public ArtistsFrame Artists
        {
            get => GetSingleFrame<ArtistsFrame>();
            set => SetSingleFrame(value);
        }

        public ArtistUrlFrameList ArtistUrls => GetMultipleFrames<ArtistUrlFrame, ArtistUrlFrameList>();

        public AudioFileUrlFrame AudioFileUrl
        {
            get => GetSingleFrame<AudioFileUrlFrame>();
            set => SetSingleFrame(value);
        }

        public AudioSourceUrlFrame AudioSourceUrl
        {
            get => GetSingleFrame<AudioSourceUrlFrame>();
            set => SetSingleFrame(value);
        }

        public BandFrame Band
        {
            get => GetSingleFrame<BandFrame>();
            set => SetSingleFrame(value);
        }

        public BeatsPerMinuteFrame BeatsPerMinute
        {
            get => GetSingleFrame<BeatsPerMinuteFrame>();
            set => SetSingleFrame(value);
        }

        public CommentFrameList Comments => GetMultipleFrames<CommentFrame, CommentFrameList>();

        public CommercialUrlFrameList CommercialUrls => GetMultipleFrames<CommercialUrlFrame, CommercialUrlFrameList>();

        public ComposersFrame Composers
        {
            get => GetSingleFrame<ComposersFrame>();
            set => SetSingleFrame(value);
        }

        public ConductorFrame Conductor
        {
            get => GetSingleFrame<ConductorFrame>();
            set => SetSingleFrame(value);
        }

        public ContentGroupDescriptionFrame ContentGroupDescription
        {
            get => GetSingleFrame<ContentGroupDescriptionFrame>();
            set => SetSingleFrame(value);
        }

        public CopyrightFrame Copyright
        {
            get => GetSingleFrame<CopyrightFrame>();
            set => SetSingleFrame(value);
        }

        public CopyrightUrlFrame CopyrightUrl
        {
            get => GetSingleFrame<CopyrightUrlFrame>();
            set => SetSingleFrame(value);
        }

        public CustomTextFrameList CustomTexts => GetMultipleFrames<CustomTextFrame, CustomTextFrameList>();

        public EncoderFrame Encoder
        {
            get => GetSingleFrame<EncoderFrame>();
            set => SetSingleFrame(value);
        }

        public EncodingSettingsFrame EncodingSettings
        {
            get => GetSingleFrame<EncodingSettingsFrame>();
            set => SetSingleFrame(value);
        }

        public FileOwnerFrame FileOwner
        {
            get => GetSingleFrame<FileOwnerFrame>();
            set => SetSingleFrame(value);
        }

        public FileTypeFrame FileType
        {
            get => GetSingleFrame<FileTypeFrame>();
            set => SetSingleFrame(value);
        }

        public GenreFrame Genre
        {
            get => GetSingleFrame<GenreFrame>();
            set => SetSingleFrame(value);
        }

        public LengthFrame Length
        {
            get => GetSingleFrame<LengthFrame>();
            set => SetSingleFrame(value);
        }

        public LyricistsFrame Lyricists
        {
            get => GetSingleFrame<LyricistsFrame>();
            set => SetSingleFrame(value);
        }

        public LyricsFrameList Lyrics => GetMultipleFrames<LyricsFrame, LyricsFrameList>();

        public PaymentUrlFrame PaymentUrl
        {
            get => GetSingleFrame<PaymentUrlFrame>();
            set => SetSingleFrame(value);
        }

        public PublisherFrame Publisher
        {
            get => GetSingleFrame<PublisherFrame>();
            set => SetSingleFrame(value);
        }

        public PictureFrameList Pictures => GetMultipleFrames<PictureFrame, PictureFrameList>();

        public PrivateFrameList PrivateData => GetMultipleFrames<PrivateFrame, PrivateFrameList>();

        public RecordingDateFrame RecordingDate
        {
            get => GetSingleFrame<RecordingDateFrame>();
            set => SetSingleFrame(value);
        }

        public SubtitleFrame Subtitle
        {
            get => GetSingleFrame<SubtitleFrame>();
            set => SetSingleFrame(value);
        }

        public TitleFrame Title
        {
            get => GetSingleFrame<TitleFrame>();
            set => SetSingleFrame(value);
        }

        public TrackFrame Track
        {
            get => GetSingleFrame<TrackFrame>();
            set => SetSingleFrame(value);
        }

        public YearFrame Year
        {
            get => GetSingleFrame<YearFrame>();
            set => SetSingleFrame(value);
        }
        #endregion

        #region Frame internals
        /// <summary>
        ///     Collection of frames, keyed by the frame type.
        /// </summary>
        private Dictionary<Type, object> _frames;

        private Dictionary<Type, object> Frames =>
            _frames ?? (_frames = new Dictionary<Type, object>(50));

        /// <summary>
        ///     List of all multiple instance frame types and factory functions to create instances of their collection classes.
        /// </summary>
        private static readonly Dictionary<Type, Func<IList>> MultiInstanceFrameTypes =
            new Dictionary<Type, Func<IList>>
            {
                [typeof(ArtistUrlFrame)] = () => new ArtistUrlFrameList(),
                [typeof(CommentFrame)] = () => new CommentFrameList(),
                [typeof(CommercialUrlFrame)] = () => new CommercialUrlFrameList(),
                [typeof(CustomTextFrame)] = () => new CustomTextFrameList(),
                [typeof(LyricsFrame)] = () => new LyricsFrameList(),
                [typeof(PictureFrame)] = () => new PictureFrameList(),
                [typeof(PrivateFrame)] = () => new PrivateFrameList()
            };

        /// <summary>
        ///     Adds an <see cref="Id3Frame"/> instance to the Frames collection. Since this is not a concrete frame type, the
        ///     method needs to do a bit of work to figure out how to add it to the Frames collection.
        ///     This method is meant to be called by <see cref="Id3Handler"/> instances when they are reading the ID3 data and
        ///     populating this object.
        /// </summary>
        /// <param name="frame">The <see cref="Id3Frame" /> instance to add.</param>
        internal void AddUntypedFrame(Id3Frame frame)
        {
            Type frameType = frame.GetType();
            bool containsKey = Frames.ContainsKey(frameType);
            if (MultiInstanceFrameTypes.ContainsKey(frameType))
            {
                IList list;
                if (containsKey)
                    list = (IList)Frames[frameType];
                else
                {
                    list = MultiInstanceFrameTypes[frameType]();
                    Frames.Add(frameType, list);
                }

                list.Add(frame);
            }
            else
            {
                //If the frame is a single-instance frame, simply add or update it in the Frames collection.
                if (containsKey)
                    Frames[frameType] = frame;
                else
                    Frames.Add(frameType, frame);
            }
        }

        private TFrame GetSingleFrame<TFrame>()
            where TFrame : Id3Frame, new()
        {
            if (Frames.TryGetValue(typeof(TFrame), out object frameObj))
                return (TFrame)frameObj;
            var frame = new TFrame();
            Frames.Add(typeof(TFrame), frame);
            return frame;
        }

        private void SetSingleFrame<TFrame>(TFrame frame)
            where TFrame : Id3Frame
        {
            Type frameType = typeof(TFrame);
            bool containsKey = Frames.ContainsKey(frameType);
            if (frame == null)
            {
                if (containsKey)
                    Frames.Remove(frameType);
            }
            else
            {
                if (containsKey)
                    Frames[frameType] = frame;
                else
                    Frames.Add(frameType, frame);
            }
        }

        private TFrameList GetMultipleFrames<TFrame, TFrameList>()
            where TFrame : Id3Frame
            where TFrameList : IList<TFrame>, new()
        {
            if (Frames.TryGetValue(typeof(TFrame), out object frameListObj))
                return (TFrameList)frameListObj;
            var framesList = new TFrameList();
            Frames.Add(typeof(TFrame), framesList);
            return framesList;
        }
        #endregion

        #region IComparable<Id3Tag> and IEquatable<Id3Tag> implementations
        /// <summary>
        ///     Compares two tags based on their version details.
        /// </summary>
        /// <param name="other">The tag instance to compare against.</param>
        /// <returns>A signed number that indicates the relative values of this instance and another instance of Id3Tag.</returns>
        public int CompareTo(Id3Tag other)
        {
            if (other == null)
                return 1;
            return Version.CompareTo(other.Version);
        }

        public bool Equals(Id3Tag other)
        {
            if (other == null)
                return false;
            return Version == other.Version;
        }
        #endregion
    }
}
