using System;

namespace TCReleaseNoteCompiler
{
    public class ChangeDetail
{
    public int id { get; set; }
    public string version { get; set; }
    public string username { get; set; }
    public DateTime date { get; set; }
    public string href { get; set; }
    public string webUrl { get; set; }
    public string comment { get; set; }
    public Files files { get; set; }
    public VcsRootInstance vcsRootInstance { get; set; }
}

}
