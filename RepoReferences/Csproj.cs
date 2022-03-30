class Csproj
{
    public string SvnUri { get; set; }
    public string SvnName => Path.GetFileName(SvnUri);
    public string Name => Path.GetFileNameWithoutExtension(SvnName);
    public List<Reference> References { get; set; } = new List<Reference>();
    public Stream Content { get; set; }

    public class Reference
    {
        public string XmlTag { get; set; } //Reference, PackageReference
        public string Name { get; set; }
        public List<Csproj> Csprojs { get; set; } = new List<Csproj>();

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            var reference = obj as Reference;
            if (reference == null) return false;

            return Name.Equals(reference.Name);
        }
    }
}