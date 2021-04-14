#addin "nuget:?package=Cake.FileHelpers"

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var progetApiKey = "svc-build:" + (EnvironmentVariable("proget_password") ?? "");

var solutionFileName = "../src/NSwag.sln";

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(ctx => {
});

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
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
            ApiKey = progetApiKey
        });
    });

Task("Default")
    .IsDependentOn("Push");

Task("Local")
    .IsDependentOn("Build");

RunTarget(target);
