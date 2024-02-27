// description: Provides an update notification message if an update is
// availiable
// author: @therealshodan

#r "nuget: NuGet.Protocol, 5.9.0"

using System.Reflection;
using System.Threading;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol;

// If we start this in a task then we don't slow down the process of giving the
// user a shell
_ = Task.Run(async () => 
{
    var updateCheckFile = new FileInfo(Path.Combine(Shell.HomeDirectory, ".nsh", ".updatecheck"));
    if (updateCheckFile.Exists && DateTime.UtcNow - updateCheckFile.LastAccessTime < TimeSpan.FromDays(1))
    {
        return;
    }

    try
    {
        var cache = new SourceCacheContext();
        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>();
        var versions = await resource.GetAllVersionsAsync("dotnet-shell", cache, NullLogger.Instance, CancellationToken.None);
        var latest = versions.Where(x => !x.IsPrerelease).OrderByDescending(x => x.Version).First();
        var latestVersion = versions.Where(x => !x.IsPrerelease).OrderByDescending(x => x.Version).First();
        var currentVersion = Assembly.GetEntryAssembly().GetName().Version;

        if (!currentVersion.ToString().StartsWith(latestVersion.ToString()))
        {
            StatusMessage(string.Format("New version ({0}) available, run 'dotnet tool update -g dotnet-shell' to upgrade from {1}", latestVersion, currentVersion), "VersionCheck");
        }
    }
    catch (Exception ex)
    {
        StatusMessage(ex.Message, "VersionCheck");
    }

    await File.AppendAllTextAsync(updateCheckFile.FullName, ".");
});
