﻿// See https://aka.ms/new-console-template for more information
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
var projectUrisFilePath = args.ElementAtOrDefault(3);

var authEventHandler = new EventHandler<SharpSvn.Security.SvnUserNamePasswordEventArgs>(
delegate (object s, SharpSvn.Security.SvnUserNamePasswordEventArgs ee)
{
    ee.UserName = userName;
    ee.Password = password;
});
var sslEventHandler = new EventHandler<SharpSvn.Security.SvnSslServerTrustEventArgs>(
delegate (object ssender, SharpSvn.Security.SvnSslServerTrustEventArgs se)
{
    se.AcceptedFailures = se.Failures;
    se.Save = true;//Save acceptance to authentication store
});
using var client = GetSvnClient();
SvnClient GetSvnClient()
{
    var client = new SvnClient();
    client.Authentication.UserNamePasswordHandlers += authEventHandler;
    client.Authentication.SslServerTrustHandlers += sslEventHandler;
    return client;
}

//var path = Environment.CurrentDirectory + @"\repo";
var projectByName = new ConcurrentDictionary<string, Csproj>();
var listArgs = new SvnListArgs()
{
    Depth = SvnDepth.Infinity,
};
var target = new SvnUriTarget(svnPath);
//cmd> svn list -R  https://svn.safetypay.com/svn/SafetyPayMain/ | find ".csproj" > "D:\safetypay\trunks\localProjects\RepoReferences\RepoReferences\projectUris.txt"
if (string.IsNullOrWhiteSpace(projectUrisFilePath))
{
    Console.WriteLine($"{DateTime.UtcNow} Start list svn projects.");
    client.List(target, listArgs, HandleSvnListEvent);
}
else
{
    var uris = GetProjSvnUrisFromFile(svnPath, projectUrisFilePath);
    Console.WriteLine($"{DateTime.UtcNow} Start HandleSvnUri from file.");
    Parallel.ForEach(uris, uri => HandleSvnUri(uri));
}
string[] GetProjSvnUrisFromFile(string svnRoot, string projectUrisFilePath)
{
    Console.WriteLine($"{DateTime.UtcNow} Start GetProjSvnUrisFromFile.");
    var uris = File.ReadAllLines(projectUrisFilePath);
    for (int i = 0; i < uris.Length; i++)
    {
        uris[i] = new Uri(new Uri(svnRoot), uris[i]).AbsoluteUri;
    }

    return uris;
}
void HandleSvnListEvent(object? sender, SvnListEventArgs e)
{
    HandleSvnUri(e.Uri.AbsoluteUri);
}
void HandleSvnUri(string svnUri)
{
    if (svnUri.EndsWith(".csproj")) //ProjectFileRegex.IsMatch(svnUri)
    {
        //Console.WriteLine($"Matched: {e.Name}");
        var project = new Csproj
        {
            SvnUri = svnUri,
            Content = new MemoryStream(),
        };
        projectByName.AddOrUpdate(
            project.Name,
            project,
            (key, old) =>
            {
                var oldTarget = new SvnUriTarget(old.SvnUri);
                var newTarget = new SvnUriTarget(project.SvnUri);
                using var infoClient1 = GetSvnClient();
                using var infoClient2 = GetSvnClient();
                SvnInfoEventArgs? oldSvnInfoEventArgs = null;
                SvnInfoEventArgs? newSvnInfoEventArgs = null;
                var t1 = Task.Run(() => infoClient1.GetInfo(oldTarget, out oldSvnInfoEventArgs));
                var t2 = Task.Run(() => infoClient2.GetInfo(newTarget, out newSvnInfoEventArgs));
                Task.WaitAll(t1, t2);
                if (newSvnInfoEventArgs!.LastChangeTime > oldSvnInfoEventArgs!.LastChangeTime)
                {
                    //Console.WriteLine($"{DateTime.UtcNow} Project {project.SvnUri} {newSvnInfoEventArgs.LastChangeTime} is newer than {old.SvnUri} {oldSvnInfoEventArgs.LastChangeTime}. Replacing");
                    projectByName[project.Name] = project;
                }
                return project;
            });
    }
    else
    {
        //Console.WriteLine($"Not matched: {e.Name}");
    }
}

Console.WriteLine($"{DateTime.UtcNow} Begin parse svn content.");
var references = new ConcurrentDictionary<int, Csproj.Reference>();
Parallel.ForEach(projectByName.Values, project =>
{
    var writeClient = GetSvnClient();
    writeClient.Write(new SvnUriTarget(project.SvnUri), project.Content, out var _);
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
