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

using System.Collections.ObjectModel;
using System.IO;

namespace Id3.Frames
{
    public sealed class PictureFrame : Id3Frame
    {
        public PictureFrame()
        {
            PictureType = PictureType.FrontCover;
        }

        public override bool Equals(Id3Frame other)
        {
            return other is PictureFrame picture &&
                PictureType == picture.PictureType;
        }

        public void LoadImage(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);
            PictureData = bytes;
        }

        public void LoadImage(string filePath)
        {
            PictureData = File.ReadAllBytes(filePath);
        }

        public void SaveImage(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(PictureData, 0, PictureData.Length);
        }

        public void SaveImage(string filePath)
        {
            File.WriteAllBytes(filePath, PictureData);
        }

        public string GetExtension()
        {
            if (string.IsNullOrEmpty(MimeType))
                return "jpg";
            string[] parts = MimeType.Split('/');
            if (parts.Length < 2 || string.IsNullOrEmpty(parts[1]))
                return "jpg";
            return parts[1];
        }

        public override bool IsAssigned => PictureData != null && PictureData.Length > 0;

        public string Description { get; set; }

        public Id3TextEncoding EncodingType { get; set; }

        public string MimeType { get; set; }

        public byte[] PictureData { get; set; }

        public PictureType PictureType { get; set; }
    }

    public sealed class PictureFrameList : Collection<PictureFrame>
    {
    }

    public enum PictureType : byte
    {
        Other = 0x00,
        FileIcon = 0x01,
        OtherFileIcon = 0x02,
        FrontCover = 0x03,
        BackCover = 0x04,
        LeafletPage = 0x05,
        Media = 0x06,
        LeadArtistPerformerSoloist = 0x07,
        ArtistOrPerformer = 0x08,
        Conductor = 0x09,
        BandOrOrchestra = 0x0A,
        Composer = 0x0B,
        LyricistOrTextWriter = 0x0C,
        RecordingLocation = 0x0D,
        DuringRecording = 0x0E,
        DuringPerformance = 0x0F,
        MovieOrVideoScreenCapture = 0x10,
        ABrightColouredFish = 0x11,
        Illustration = 0x12,
        BandOrArtistLogotype = 0x13,
        PublisherOrStudioLogotype = 0x14
    }
}