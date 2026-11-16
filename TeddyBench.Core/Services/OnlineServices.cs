using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace TeddyBench.Core
{
    public class UpdateInfo
    {
        public string LatestVersion;
        public string ReleaseName;
        public string AssetName;
        public string AssetUrl;
        /// <summary>The release page on GitHub.</summary>
        public string ReleaseUrl;
    }

    /// <summary>Checks GitHub for a newer TeddyBench release.</summary>
    public class UpdateChecker
    {
        public const string Owner = "toniebox-reverse-engineering";
        public const string Repository = "teddy";

        private readonly Func<string, Task<string>> Get;

        /// <param name="get">url -> response body. Defaults to an HTTP GET with a TeddyBench user agent.</param>
        public UpdateChecker(Func<string, Task<string>> get = null)
        {
            Get = get ?? HttpGet;
        }

        /// <summary>
        /// Returns the latest release if its tag differs from <paramref name="thisVersion"/> and its first
        /// asset is <paramref name="assetName"/> (null: any or no asset, e.g. to just link the release page);
        /// null otherwise (also when there is no version to compare).
        /// </summary>
        public async Task<UpdateInfo> CheckAsync(string thisVersion, string assetName = "TeddyBench.zip")
        {
            if (string.IsNullOrEmpty(thisVersion))
            {
                return null;
            }

            JObject latestRelease = JObject.Parse(await Get($"https://api.github.com/repos/{Owner}/{Repository}/releases/latest").ConfigureAwait(false));
            string latestVersion = (string)latestRelease["tag_name"];

            if (latestVersion == thisVersion)
            {
                return null;
            }

            JToken asset = (latestRelease["assets"] as JArray)?.FirstOrDefault();
            if (assetName != null && (asset == null || (string)asset["name"] != assetName))
            {
                return null;
            }

            return new UpdateInfo
            {
                LatestVersion = latestVersion,
                ReleaseName = (string)latestRelease["name"],
                AssetName = (string)asset?["name"],
                AssetUrl = (string)asset?["browser_download_url"],
                ReleaseUrl = (string)latestRelease["html_url"]
            };
        }

        public static async Task DownloadAsync(string url, string destinationFileName)
        {
            using (var client = CreateClient())
            using (var stream = await client.GetStreamAsync(url).ConfigureAwait(false))
            using (var file = new FileStream(destinationFileName, FileMode.Create))
            {
                await stream.CopyToAsync(file).ConfigureAwait(false);
            }
        }

        private static async Task<string> HttpGet(string url)
        {
            using (var client = CreateClient())
            {
                return await client.GetStringAsync(url).ConfigureAwait(false);
            }
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; TeddyBench/1.0)");
            return client;
        }
    }

    public class DiagStatus
    {
        public bool Success;
        public string Message;
    }

    /// <summary>Sends user-confirmed diagnostic reports to the RevvoX diagnostics endpoint.</summary>
    public class DiagnosticsClient
    {
        public const string Endpoint = "https://api.revvox.de/diag";

        private readonly string Version;

        public DiagnosticsClient(string version)
        {
            Version = version;
        }

        public async Task<DiagStatus> SendAsync(string payload, string sender, string message, string filename)
        {
            var ret = new DiagStatus { Success = false, Message = "" };

            try
            {
                using (var httpClient = new HttpClient())
                {
                    var form = new MultipartFormDataContent();

                    form.Add(new StringContent(sender ?? ""), "sender");
                    form.Add(new StringContent(payload ?? ""), "payload");
                    form.Add(new StringContent(message ?? ""), "message");
                    form.Add(new StringContent(filename ?? ""), "filename");
                    form.Add(new StringContent(Version ?? ""), "version");

                    /* ConfigureAwait(false): ReportException blocks on .Result from the UI thread */
                    HttpResponseMessage response = await httpClient.PostAsync(Endpoint, form).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    string sd = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    Logger.Log(Logger.eLogLevel.Debug, sd);

                    ret.Success = (response.StatusCode == HttpStatusCode.OK);
                    ret.Message = sd;
                }
            }
            catch (Exception ee)
            {
                Logger.Log(Logger.eLogLevel.Debug, "[Diagnostics] send failed: " + ee.Message);
            }
            return ret;
        }
    }

    /// <summary>Texts of the diagnostic reports the user can review and send.</summary>
    public static class DiagnosticReports
    {
        private const string Separator = "-----------------------------------";

        public static string Exception(string threadName, Exception e)
        {
            var str = new StringBuilder();

            str.AppendLine(" Reporting an unhandled eception");
            str.AppendLine(Separator);
            str.AppendLine("Thread: '" + threadName + "'");
            str.AppendLine("");
            str.AppendLine(Separator);
            str.AppendLine("");
            str.AppendLine("Message:");
            str.AppendLine(e.Message);
            str.AppendLine("");
            str.AppendLine(Separator);
            str.AppendLine("");
            str.AppendLine("StackTrace:");
            str.AppendLine(e.StackTrace);
            str.AppendLine("");
            str.AppendLine(Separator);
            str.AppendLine("");

            return str.ToString();
        }

        /// <summary>Tag dump as read by the reader: 8 byte UID followed by the memory.</summary>
        public static string NfcTag(byte[] data)
        {
            var str = new StringBuilder();

            str.AppendLine(" Reporting NFC tag content");
            str.AppendLine(Separator);
            str.AppendLine("UUID:        " + BitConverter.ToString(data.Take(8).ToArray()).Replace("-", ""));
            str.AppendLine("Memory:      " + BitConverter.ToString(data.Skip(8).ToArray()).Replace("-", ""));

            return str.ToString();
        }

        public static string Antenna(Proxmark3.MeasurementResult result)
        {
            string nl = Environment.NewLine;
            string content = " Reporting Proxmark3 antenna performance" + nl;
            content += "------------------------------------------" + nl;
            content += "HF:     " + result.vHF.ToString("0.00", CultureInfo.InvariantCulture) + " V" + nl;
            content += "LF125:  " + result.vLF125.ToString("0.00", CultureInfo.InvariantCulture) + " V" + nl;
            content += "LF134:  " + result.vLF134.ToString("0.00", CultureInfo.InvariantCulture) + " V" + nl;
            content += "LFfopt: " + (result.GetPeakFrequency() / 1000.0f).ToString("0.00", CultureInfo.InvariantCulture) + " kHz" + nl;
            content += "LFVopt: " + result.peakV.ToString("0.00", CultureInfo.InvariantCulture) + " V" + nl;
            return content;
        }

        /// <summary>Report about tonie files, with the tonies.json / custom name info for each.</summary>
        public static string AudioFiles(System.Collections.Generic.IList<CardEntry> entries, TonieDatabase database)
        {
            var str = new StringBuilder();

            str.AppendLine(" Reporting " + entries.Count + " files");
            str.AppendLine(Separator);

            TonieFile.TonieTools.TonieData[] all = database.All;
            foreach (var entry in entries)
            {
                TonieFile.TonieTools.DumpInfo(str, TonieFile.TonieTools.eDumpFormat.FormatText, entry.FileName, all, database.GetCustomName(entry.Hash));
            }
            return str.ToString();
        }
    }

    /// <summary>Downloads tonie pictures once and keeps them in <see cref="AppPaths.CacheDirectory"/>.</summary>
    public class ImageCache
    {
        private readonly string Directory;
        private readonly Func<string, byte[]> Download;

        public ImageCache(string directory = null, Func<string, byte[]> download = null)
        {
            Directory = directory ?? AppPaths.CacheDirectory;
            Download = download ?? HttpGetBytes;
        }

        /// <summary>The image bytes for a tonie (cached by audio hash), or null if unavailable.</summary>
        public byte[] GetImage(string url, string hash)
        {
            string cacheFileName = Path.Combine(Directory, hash + ".png");

            try
            {
                if (File.Exists(cacheFileName))
                {
                    return File.ReadAllBytes(cacheFileName);
                }
            }
            catch (Exception ex)
            {
            }

            try
            {
                byte[] data = Download(url);

                try
                {
                    System.IO.Directory.CreateDirectory(Directory);
                    File.WriteAllBytes(cacheFileName, data);
                }
                catch (Exception ex)
                {
                }
                return data;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private static byte[] HttpGetBytes(string url)
        {
            using (var client = new HttpClient())
            {
                return client.GetByteArrayAsync(url).GetAwaiter().GetResult();
            }
        }
    }
}
