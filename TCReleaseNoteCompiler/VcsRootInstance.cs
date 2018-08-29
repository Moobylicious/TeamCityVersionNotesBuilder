using Newtonsoft.Json;

namespace TCReleaseNoteCompiler
{
    public class VcsRootInstance
{
    public string id { get; set; }
    [JsonProperty(PropertyName = "vc-root-id")]
        public string vcsrootid { get; set; }
        public string name { get; set; }
public string href { get; set; }
}

}
