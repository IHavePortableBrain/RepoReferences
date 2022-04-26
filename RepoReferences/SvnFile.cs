using System.Text.RegularExpressions;

class SvnFile
{
    public string SvnUri { get; set; }
    public string SvnName => Path.GetFileName(SvnUri);
    public string Name => Path.GetFileNameWithoutExtension(SvnName);
    public Stream Content { get; set; }
    public MatchCollection MatchCollection { get; set; }
}