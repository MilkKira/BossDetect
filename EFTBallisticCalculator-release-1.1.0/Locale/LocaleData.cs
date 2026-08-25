using Newtonsoft.Json;
using System.Collections.Generic;

namespace EFTBallisticCalculator.Locale
{
    public class LocaleData
    {
        [JsonProperty("Language")]
        public string Language { get; set; }

        [JsonProperty("Translate")]
        public Dictionary<string, string> Translate { get; set; }
    }
}
