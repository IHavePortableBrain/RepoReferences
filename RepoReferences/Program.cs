using SharpSvn;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

const string ReferenceGroup = "reference";
Regex LookupFilesRegex = new Regex(@"\.(aspx|ascx|js|ts)\z");
Regex SearchContentRegex = new Regex(@"knockout-2\.3\.0\.js");

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
var parallelOptions = new ParallelOptions()
{
    MaxDegreeOfParallelism = -1,// Environment.ProcessorCount
};
//var path = Environment.CurrentDirectory + @"\repo";
var svnFileByName = new ConcurrentDictionary<string, SvnFile>();
var listArgs = new SvnListArgs()
{
    Depth = SvnDepth.Infinity,
};
var target = new SvnUriTarget(svnPath);
//cmd> svn list -R  https://svn.safetypay.com/svn/SafetyPayMain/ | find ".csproj" > "D:\safetypay\trunks\localProjects\RepoReferences\RepoReferences\projectUris.txt"
if (string.IsNullOrWhiteSpace(projectUrisFilePath))
{
    Console.WriteLine($"{DateTime.Now} Start list svn projects from repo.");
    client.GetList(target, listArgs, out var svnListEventArgsList);
    Console.WriteLine($"{DateTime.Now} Start HandleSvnListEvent from repo.");
    Parallel.ForEach(svnListEventArgsList, parallelOptions, svnListEventArgs => HandleSvnListEvent(null, svnListEventArgs));
    //client.List(target, listArgs, HandleSvnListEvent);
}
else
{
    Console.WriteLine($"{DateTime.Now} Start list svn projects from file.");
    var uris = GetProjSvnUrisFromFile(svnPath, projectUrisFilePath);
    Console.WriteLine($"{DateTime.Now} Start HandleSvnUri from file.");
    Parallel.ForEach(uris, parallelOptions, uri => HandleSvnUri(uri));
}
string[] GetProjSvnUrisFromFile(string svnRoot, string projectUrisFilePath)
{
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
    if (LookupFilesRegex.IsMatch(svnUri))
    {
        var project = new SvnFile
        {
            SvnUri = svnUri,
            Content = new MemoryStream(),
        };
        svnFileByName.AddOrUpdate(
            project.Name,
            project,
            (key, old) =>
            {
                {
                    svnFileByName[project.Name] = project;
                }
                return project;
            });
    }
}

Console.WriteLine($"{DateTime.Now} Begin parse svn content.");
Parallel.ForEach(svnFileByName.Values, parallelOptions, svnFile =>
{
    var writeClient = GetSvnClient();
    writeClient.Write(new SvnUriTarget(svnFile.SvnUri), svnFile.Content, out var _);
    svnFile.Content.Seek(0, SeekOrigin.Begin);
    using var streamReader = new StreamReader(svnFile.Content);
    var contentString = streamReader.ReadToEnd(); //async
    streamReader.Dispose();
    var contentMatches = SearchContentRegex.Matches(contentString);
    svnFile.MatchCollection = contentMatches;
    if (svnFile.MatchCollection != null && svnFile.MatchCollection.Any())
    {
        Console.WriteLine($"Matches found {svnFile.SvnUri}\t at index {string.Join(" ", svnFile.MatchCollection.Select(x => x.Index))}");
    }
});

do
{
    Console.WriteLine($"{DateTime.Now} End. Press f...");
}
while (Console.ReadKey().Key != ConsoleKey.F);