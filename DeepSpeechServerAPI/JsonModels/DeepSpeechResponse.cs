using Newtonsoft.Json;
using System.Collections.Generic;

namespace DeepSpeechServerAPI.JsonModels
{
    public class DeepSpeechResponse
    {
        [JsonProperty("transcripts")]
        public List<Transcript> Transcripts { get; set; }
    }

    public class Transcript
    {
        [JsonProperty("confidence")]
        public string Confidence { get; set; }

        [JsonProperty("words")]
        public List<WordInfo> WordInfos { get; set; }
    }

    public class WordInfo
    {
        [JsonProperty("word")]
        public string Word { get; set; }
        [JsonProperty("start_time")]
        public string StartTime { get; set; }
        [JsonProperty("duration")]
        public string Duration { get; set; }
    }
}
