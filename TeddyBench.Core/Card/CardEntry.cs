using System.IO;
using TonieFile;

namespace TeddyBench.Core
{
    /// <summary>
    /// One tonie file, either on the SD card (CONTENT/XXXXXXXX/YYYYYYYY, the path is the reversed UID)
    /// or a previously exported file ("model - AUDIOID - title") in the opened directory.
    /// </summary>
    public class CardEntry
    {
        public string FileName;
        public FileInfo FileInfo;
        /// <summary>SHA1 of the audio data (hex, upper case). Set by <see cref="TonieResolver"/>.</summary>
        public string Hash;
        /// <summary>tonies.json entry, or a placeholder with only a title for custom tonies. Set by <see cref="TonieResolver"/>.</summary>
        public TonieTools.TonieData Info;
        public string Uid;
        public uint AudioId;

        /// <summary>What to show before the file was analyzed (UID, or "model - name" for exported files).</summary>
        public string InitialText;

        public CardEntry(string filename)
        {
            FileName = filename;

            FileInfo = new FileInfo(FileName);
            Uid = TonieTools.ReverseUid(FileInfo.Directory.Name + FileInfo.Name);
            InitialText = Uid;
        }
    }
}
