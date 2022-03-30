// See https://aka.ms/new-console-template for more information
using SharpSvn;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

const string ReferenceGroup = "reference";
Regex ProjectFileRegex = new Regex(@"\.csproj\z");
Regex ReferenceRegex = new Regex(@"<Reference Include=""(?<reference>(\w|\.)+)"); //(,|"")
Regex PackageReferenceRegex = new Regex(@"<PackageReference Include=""(?<reference>(\w|\.)+)");

var userName = args[0];
var password = args[1];
var svnPath = args[2];

using SvnClient client = new SvnClient();
//client.Authentication.Clear();
client.Authentication.UserNamePasswordHandlers += new EventHandler<SharpSvn.Security.SvnUserNamePasswordEventArgs>(
delegate (Object s, SharpSvn.Security.SvnUserNamePasswordEventArgs ee)
{
    ee.UserName = userName;
    ee.Password = password;
});

client.Authentication.SslServerTrustHandlers += new EventHandler<SharpSvn.Security.SvnSslServerTrustEventArgs>(
delegate (Object ssender, SharpSvn.Security.SvnSslServerTrustEventArgs se)
{
    //Look at the rest of the arguments of E whether you wish to accept

    //If accept:
    se.AcceptedFailures = se.Failures;
    se.Save = true;//Save acceptance to authentication store
});

var path = Environment.CurrentDirectory + @"\repo"; // Path.Combine(
var projectNames = new HashSet<string>();
var projects = new List<Csproj>();
var listArgs = new SvnListArgs()
{
    Depth = SvnDepth.Infinity,
};
var target = new SvnUriTarget(svnPath);
client.List(target, listArgs, SvnListHandler); //the slowest part
void SvnListHandler(object? sender, SvnListEventArgs e)
{
    if (ProjectFileRegex.IsMatch(e.Name))
    {
        //Console.WriteLine($"Matched: {e.Name}");
        var project = new Csproj
        {
            SvnUri = e.Uri.AbsoluteUri,
            Content = new MemoryStream(),
        };
        if (projectNames.Add(project.Name))
        {
            projects.Add(project);
        }
        else
        {
            Console.WriteLine($"Project name {project.Name} repeated.");
            //client.GetInfo() todo compare new project info with existing, save newest to dict
            //can not use client in event handler since only one simultaneous command for client
            //latests by ProjName(not ok since different projects have common project names but acceptable) or by ProjectIdFromContent
            //just open second client and get info
        }
    }
    else
    {
        //Console.WriteLine($"Not matched: {e.Name}");
    }
}
Console.WriteLine();

Console.WriteLine("Begin svn content load.");
foreach (var project in projects)
{
    client.Write(new SvnUriTarget(project.SvnUri), project.Content, out var _);
}

Console.WriteLine("Begin parse svn content.");
var references = new ConcurrentDictionary<int, Csproj.Reference>();
Parallel.ForEach(projects, project =>
{
    project.Content.Seek(0, SeekOrigin.Begin);
    using var streamReader = new StreamReader(project.Content);
    var contentString = streamReader.ReadToEnd(); //async
    streamReader.Dispose();
    var referenceMatches = ReferenceRegex.Matches(contentString);
    var packageReferenceMatches = PackageReferenceRegex.Matches(contentString);
    project.References.AddRange(referenceMatches.Select(x => new Csproj.Reference
    {
        XmlTag = "Reference",
        Name = x.Groups[ReferenceGroup].Value,
        Csprojs = new List<Csproj>() { project }
    }));
    project.References.AddRange(packageReferenceMatches.Select(x => new Csproj.Reference
    {
        XmlTag = "PackageReference",
        Name = x.Groups[ReferenceGroup].Value,
        Csprojs = new List<Csproj>() { project }
    }));
    foreach (var reference in project.References)
    {
        if (references.TryGetValue(reference.GetHashCode(), out var existing))
        {
            existing.Csprojs.Add(project);
        }
        else
        {
            references.TryAdd(reference.GetHashCode(), reference);
        }
    }
});

foreach (var reference in references.Select(x => x.Value).OrderBy(x => x.Name))
{
    var projectsString = string.Join(",", reference.Csprojs.Select(x => x.Name)); 
    Console.WriteLine($"{reference.Name}\t{projectsString}");
}
