using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using TonieFile;

namespace TeddyBench.Core
{
    /// <summary>
    /// The tonies.json database (official tonies, looked up by audio hash) plus the user's own
    /// names for custom tonies (customTonies.json, hash -> name). Thread safe.
    /// </summary>
    public class TonieDatabase
    {
        public const string DefaultUrl = "https://api.revvox.de/tonies.json?source=TeddyBench&version=";

        private readonly object Lock = new object();
        private TonieTools.TonieData[] Tonies = new TonieTools.TonieData[0];
        private Dictionary<string, string> CustomNames = new Dictionary<string, string>();
        private readonly string JsonFile;
        private readonly string CustomJsonFile;
        private readonly Func<string, string> Download;

        /// <summary>Short status for the status bar ("| Downloading...", "| Download Failed", ...), empty when fine.</summary>
        public string Status { get; private set; } = "";

        /// <summary>Raised for errors the user may want to report (download/parse failures).</summary>
        public event Action<string, Exception> Error;

        /// <param name="download">url -> content. Defaults to an HTTP GET.</param>
        public TonieDatabase(string jsonFile, string customJsonFile, Func<string, string> download = null)
        {
            JsonFile = jsonFile;
            CustomJsonFile = customJsonFile;
            Download = download ?? HttpGet;
        }

        public TonieDatabase() : this(AppPaths.TonieJsonFile, AppPaths.CustomTonieJsonFile)
        {
        }

        public int Count
        {
            get { lock (Lock) { return Tonies.Length; } }
        }

        /// <summary>Snapshot of all entries, e.g. for TonieTools.DumpInfo.</summary>
        public TonieTools.TonieData[] All
        {
            get { lock (Lock) { return Tonies.ToArray(); } }
        }

        /// <summary>
        /// Loads the database. With <paramref name="download"/> it is fetched from <paramref name="url"/>
        /// and cached in tonies.json, otherwise the cached tonies.json is read.
        /// Always (re)loads customTonies.json.
        /// </summary>
        public void Load(bool download, string url)
        {
            string jsonContent = "";

            if (download)
            {
                try
                {
                    Status = "| Downloading...";
                    jsonContent = Download(url);
                    Status = "| Downloaded";
                }
                catch (Exception e)
                {
                    Status = "| Download Failed";
                    Error?.Invoke("Downloader", e);
                    return;
                }

                if (string.IsNullOrEmpty(jsonContent))
                {
                    return;
                }

                if (!WriteWithRetry(JsonFile, jsonContent))
                {
                    Status = "| Writing Failed - is the tonies.json accessible?";
                }
            }
            else
            {
                try
                {
                    /* the WinForms version read customTonies.json here by mistake */
                    jsonContent = File.ReadAllText(JsonFile);
                }
                catch (Exception e)
                {
                    Status = "| Reading tonies.json Failed";
                    lock (Lock)
                    {
                        Tonies = new TonieTools.TonieData[0];
                    }
                }
            }

            lock (Lock)
            {
                if (!string.IsNullOrEmpty(jsonContent))
                {
                    try
                    {
                        Status = "| Parsing...";
                        Tonies = JsonConvert.DeserializeObject<TonieTools.TonieData[]>(jsonContent) ?? new TonieTools.TonieData[0];
                        Status = "";
                    }
                    catch (Exception e)
                    {
                        Status = "| Parsing Failed";
                        Tonies = new TonieTools.TonieData[0];
                        Error?.Invoke("Parse tonies.json", e);
                    }
                }

                CustomNames = ReadCustomNames(CustomJsonFile);
            }
        }

        /// <summary>The official entry for an audio hash (hex, upper case), or null.</summary>
        public TonieTools.TonieData Find(string hash)
        {
            lock (Lock)
            {
                return Tonies.FirstOrDefault(t => t.Hash != null && t.Hash.Contains(hash));
            }
        }

        /// <summary>The user's name for an audio hash, or null.</summary>
        public string GetCustomName(string hash)
        {
            lock (Lock)
            {
                return hash != null && CustomNames.TryGetValue(hash, out string name) ? name : null;
            }
        }

        /// <summary>Sets the user's name for an audio hash and saves customTonies.json.</summary>
        public bool SetCustomName(string hash, string name)
        {
            lock (Lock)
            {
                CustomNames[hash] = name;
                try
                {
                    File.WriteAllText(CustomJsonFile, JsonConvert.SerializeObject(CustomNames, Formatting.Indented));
                    return true;
                }
                catch (Exception e)
                {
                    return false;
                }
            }
        }

        private static Dictionary<string, string> ReadCustomNames(string file)
        {
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(file)) ?? new Dictionary<string, string>();
            }
            catch (Exception e)
            {
                return new Dictionary<string, string>();
            }
        }

        private bool WriteWithRetry(string file, string content)
        {
            for (int loop = 0; loop < 5; loop++)
            {
                try
                {
                    File.WriteAllText(file, content);
                    return true;
                }
                catch (IOException e)
                {
                    Error?.Invoke("Writing tonies.json", e);
                    Thread.Sleep(100);
                }
            }
            return false;
        }

        private static string HttpGet(string url)
        {
            using (var client = new HttpClient())
            {
                return client.GetStringAsync(url).GetAwaiter().GetResult();
            }
        }
    }
}
