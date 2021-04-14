[CmdletBinding()]
param (
    [Parameter()]
    [ValidateSet("InfoTrack.NSwag.MSBuild")]
    [string]
    $BuildTarget = "InfoTrack.NSwag.MSBuild",
    [Parameter()]
    [string]
    $Configuration = "Release"
)

class SetupStep{
    [string]$Name
    [Scriptblock]$Instruction
}
$BuildSteps = New-Object Collections.Generic.List[SetupStep]

function Main {
    switch ($BuildTarget) {
        "InfoTrack.NSwag.MSBuild" { Target-MSBuild }
    }

    $total = $BuildSteps.Count
    $i = 0
    Write-Output "Starting build ($total steps) 🕑"
    try{
        while ($i -lt $total) {
            $step = $BuildSteps[$i]
            Write-Output "[$($i+1)/$total] $($step.Name)"
            Invoke-Command $step.Instruction | Out-Null
            $i++
        }
        Write-Output "Finished build 🎉"
    }
    catch{
        Write-Output "Error during build $_"
    }
}

function Target-MSBuild {
    $BuildSteps.Add((New-Step "Build NSwag.Console Framework" (Generate-Build-Command "src/NSwag.Console/NSwag.Console.csproj" -BuildTarget "net461")))
    $BuildSteps.Add((New-Step "Build NSwag.Console Framework x86" (Generate-Build-Command "src/NSwag.Console.x86/NSwag.Console.x86.csproj" -BuildTarget "net461")))
    $BuildSteps.Add((New-Step "Build NSwag.Console Core 3.1" (Generate-Publish-Command "src/NSwag.ConsoleCore/NSwag.ConsoleCore.csproj" -BuildTarget "netcoreapp3.1")))
    $BuildSteps.Add((New-Step "Build NSwag.Console Core 5.0" (Generate-Publish-Command "src/NSwag.ConsoleCore/NSwag.ConsoleCore.csproj" -BuildTarget "net5.0")))
    $BuildSteps.Add((New-Step "Package NuGet" {nuget pack "src/InfoTrack.NSwag.MSBuild/InfoTrack.NSwag.MSBuild.nuspec" -output "artifacts/"}))
}

function Generate-Build-Command {
    param (
        [string]$Project,
        [string]$BuildConfiguration = $Configuration,
        [string]$BuildTarget
    )
    Write-Debug "Build command `"dotnet build $Project -c $BuildConfiguration -f $BuildTarget`""
    return [scriptblock]::Create("dotnet build $Project -c $BuildConfiguration -f $BuildTarget")
}

function Generate-Publish-Command {
    param (
        [string]$Project,
        [string]$BuildConfiguration = $Configuration,
        [string]$BuildTarget
    )
    Write-Debug "Publish command `"dotnet publish $Project -c $BuildConfiguration -f $BuildTarget`""
    return [scriptblock]::Create("dotnet publish $Project -c $BuildConfiguration -f $BuildTarget")
}

function New-Step {
    param (
        [string]$Name,
        [scriptblock]$Instruction
    )
    $step = [SetupStep]::new()
    $step.Name = $Name
    $step.Instruction = $Instruction
    return $step
}

Main