using Newtonsoft.Json;

namespace TCReleaseNoteCompiler
{
    public class File
    {
        [JsonProperty(PropertyName = "before-revision")]
        public string beforerevision { get; set; }
        [JsonProperty(PropertyName = "after-revision")]
        public string afterrevision { get; set; }
        public string changeType { get; set; }
        public string file { get; set; }
        [JsonProperty(PropertyName = "relative-file")]
        public string relativefile { get; set; }
}

}
