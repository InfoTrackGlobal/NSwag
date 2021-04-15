#addin "nuget:?package=Cake.FileHelpers"

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var progetApiKey = "svc-build:" + (EnvironmentVariable("proget_password") ?? "");

var solutionFileName = "../src/NSwag.sln";
GitVersion versionInfo;
IEnumerable<string> redirectedStandardOutput;

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

// Dynamically set build version from Git
Task("Version")
  .Does(() =>
  {
    versionInfo = GitVersion(new GitVersionSettings
    {
      UpdateAssemblyInfo = false,
      ToolPath = "/usr/local/bin/gitversion",
      OutputType = GitVersionOutput.Json
    });

    if (TeamCity.IsRunningOnTeamCity)
    {
      TeamCity.SetBuildNumber(versionInfo.NuGetVersion);
    }

    Information("InformationalVersion: {0}", versionInfo.InformationalVersion);
    Information("Nuget v1 version: {0}", versionInfo.NuGetVersion);
    Information("Nuget v2 version: {0}", versionInfo.NuGetVersionV2);
    Information("SemVer: {0}", versionInfo.SemVer);
    Information("LegacySemVer: {0}", versionInfo.LegacySemVer);
    Information("FullSemVer: {0}", versionInfo.FullSemVer);
    Information("Branch: {0}", versionInfo.BranchName);
  });


Task("Clean")
.IsDependentOn("Version")
    .Does(() =>
    {
        // Clean local test results
        DeleteFiles("**/*.testlog");

        CleanDirectories($"../**/bin");
        CleanDirectories($"../**/obj");
        CleanDirectories($"../artifacts");
    });

Task("Restore")  
.IsDependentOn("Clean")
.Does(() =>
{
    DotNetCoreRestore(solutionFileName);
});

Task("Build")
.IsDependentOn("Restore")
.Does(() => {
    var buildSettings = new DotNetCoreBuildSettings {
        Configuration = "Release",
        Framework = "net461",
        ArgumentCustomization = (args) => args
            .Append($"/p:AssemblyVersion={versionInfo.NuGetVersion}")
            .Append($"/p:PackageVersion={versionInfo.NuGetVersion}")
            .Append($"/p:FileVersion={versionInfo.NuGetVersion}")
            .Append($"/p:InformationalVersion={versionInfo.NuGetVersion}")
            .Append($"/p:IncludeSymbols=true"),
        MSBuildSettings = new DotNetCoreMSBuildSettings {
            MaxCpuCount = 1,
            ArgumentCustomization = args => args
                .Append($"/p:UseSharedCompilation=false")
      }
    };
    var publishSettings = new DotNetCorePublishSettings {
        Configuration = "Release",
        ArgumentCustomization = (args) => args
            .Append($"/p:AssemblyVersion={versionInfo.NuGetVersion}")
            .Append($"/p:PackageVersion={versionInfo.NuGetVersion}")
            .Append($"/p:FileVersion={versionInfo.NuGetVersion}")
            .Append($"/p:InformationalVersion={versionInfo.NuGetVersion}")
            .Append($"/p:IncludeSymbols=true"),
        MSBuildSettings = new DotNetCoreMSBuildSettings {
            MaxCpuCount = 1,
            ArgumentCustomization = args => args
                .Append($"/p:UseSharedCompilation=false")
      }
    };

    DotNetCoreBuild("../src/NSwag.Console/NSwag.Console.csproj", buildSettings);
    DotNetCoreBuild("../src/NSwag.Console.x86/NSwag.Console.x86.csproj", buildSettings);
    publishSettings.Framework = "netcoreapp3.1";
    DotNetCorePublish("../src/NSwag.ConsoleCore/NSwag.ConsoleCore.csproj", publishSettings);
    publishSettings.Framework = "net5.0";
    DotNetCorePublish("../src/NSwag.ConsoleCore/NSwag.ConsoleCore.csproj", publishSettings);

    NuGetPack("../src/InfoTrack.NSwag.MSBuild/InfoTrack.NSwag.MSBuild.nuspec", new NuGetPackSettings {
            Symbols = false,
            NoPackageAnalysis = true,
            OutputDirectory = "../artifacts"
        });
});

Task("Push")
    .IsDependentOn("Build")
    .Does(() =>
    {
        var packages = GetFiles($"../artifacts/*.nupkg");

        NuGetPush(packages, new NuGetPushSettings {
            Source = "https://proget.infotrack.com.au/nuget/global",
            ApiKey = progetApiKey,
            Version = versionInfo.NuGetVersion
        });
    });

// Create GitHub Release and Tag
Task("GitHubRelease")
  .IsDependentOn("Push")
  .Does(() =>
  {
    StartProcess("/usr/local/bin/auto",
      new ProcessSettings {
        Arguments = "release --use-version v" + versionInfo.NuGetVersion,
        RedirectStandardOutput = true
      },
      out redirectedStandardOutput
    );
    var autorelease = string.Join("\n", redirectedStandardOutput);
    Information("{0}", autorelease);
    FileWriteText("./release_output", autorelease);    
  });

Task("Default")
    .IsDependentOn("GitHubRelease");

Task("Local")
    .IsDependentOn("Build");

RunTarget(target);
