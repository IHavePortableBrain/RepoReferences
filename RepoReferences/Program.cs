// See https://aka.ms/new-console-template for more information
using SharpSvn;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

const string ReferenceGroup = "reference";
Regex SearchRegex = new Regex(@"(?<reference>SafetyPayLogger)", RegexOptions.IgnoreCase); //(,|"")SafetyPayLogger

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
var svnResourceByName = new ConcurrentDictionary<string, SvnResource>();
var listArgs = new SvnListArgs()
{
    Depth = SvnDepth.Infinity,
};
var target = new SvnUriTarget(svnPath);
//cmd> svn list -R  https://svn.safetypay.com/svn/SafetyPayApps/ | findstr /e ".csproj" > "C:\Users\Xiaomi\Desktop\trash\SafetyPayApps.csproj.txt"
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
    var resource = new SvnResource
    {
        SvnUri = svnUri,
        Content = new MemoryStream(),
    };
    svnResourceByName.AddOrUpdate(
        resource.Name,
        resource,
        (key, old) =>
        {
            svnResourceByName[resource.Name] = resource;
            return resource;
        });
}

SvnInfoArgs GetSvnInfoArgs()
{
    return new SvnInfoArgs
    {
        Depth = SvnDepth.Files,
        IncludeExternals = false,
        ThrowOnError = false,
    };
}

Console.WriteLine($"{DateTime.Now} Begin parse svn content.");
Parallel.ForEach(svnResourceByName.Values, parallelOptions, resource =>
{
    int tryNumber = 1;
    bool isSuccess;
    do
    {
        try
        {
            var writeClient = GetSvnClient();
            writeClient.Write(new SvnUriTarget(resource.SvnUri), resource.Content, out var _);
            resource.Content.Seek(0, SeekOrigin.Begin);
            using var streamReader = new StreamReader(resource.Content);
            var contentString = streamReader.ReadToEnd(); //async
            streamReader.Dispose();
            resource.IsMatch = SearchRegex.IsMatch(contentString);
            isSuccess = true;
        }
        catch (global::System.Exception)
        {
            tryNumber++;
            isSuccess = false;
        }
    }
    while (!isSuccess && tryNumber < 3);
});

foreach (var resource in svnResourceByName.Values.Where(x => x.IsMatch))
{
    Console.WriteLine($"Match \t{resource.SvnUri}");
}

do
{
    Console.WriteLine($"{DateTime.Now} End. Press f...");
}
while (Console.ReadKey().Key != ConsoleKey.F);