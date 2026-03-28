using Newtonsoft.Json;
using System.Collections.Generic;

namespace TeddyBench.Avalonia.Models
{
    public class TonieMetadata
    {
        [JsonProperty("no")]
        public string No { get; set; } = string.Empty;

        [JsonProperty("model")]
        public string Model { get; set; } = string.Empty;

        [JsonProperty("audio_id")]
        public List<string> AudioId { get; set; } = new();

        [JsonProperty("hash")]
        public List<string> Hash { get; set; } = new();

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("series")]
        public string Series { get; set; } = string.Empty;

        [JsonProperty("episodes")]
        public string? Episodes { get; set; }

        [JsonProperty("tracks")]
        public List<string> Tracks { get; set; } = new();

        [JsonProperty("release")]
        public string Release { get; set; } = string.Empty;

        [JsonProperty("language")]
        public string Language { get; set; } = string.Empty;

        [JsonProperty("category")]
        public string Category { get; set; } = string.Empty;

        [JsonProperty("pic")]
        public string Pic { get; set; } = string.Empty;

        [JsonProperty("directory")]
        public string? Directory { get; set; }
    }
}