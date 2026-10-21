param(
    [string]$GamePath = '',
    [string]$BepInExCorePath = '',
    [ValidateSet('BepInEx5','BepInEx6')]
    [string]$LoaderTarget = 'BepInEx5',
    [string]$PluginPath = ''
)
$ErrorActionPreference = 'Stop'
$env:MSBuildEnableWorkloadResolver = 'false'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $projectRoot 'ProbableCause.Tests.csproj'
# Omitted paths come from MSBuild: .local/LocalPaths.props or the repository defaults.
$properties = @("-p:LoaderTarget=$LoaderTarget")
foreach ($name in 'GamePath','BepInExCorePath') {
    $value = Get-Variable -Name $name -ValueOnly
    if ($value) { $properties += "-p:$name=" + $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($value).TrimEnd('\') }
}
& dotnet build $project --configuration Release @properties --nologo
if ($LASTEXITCODE -ne 0) { throw "Test build failed with exit code $LASTEXITCODE." }
$resolved = (& dotnet msbuild $project -getProperty:GamePath -getProperty:BepInExCorePath @properties -nologo) -join "`n"
if ($LASTEXITCODE -ne 0) { throw "Could not resolve GamePath and BepInExCorePath (exit code $LASTEXITCODE)." }
$paths = ($resolved | ConvertFrom-Json).Properties
$testArguments = @($paths.GamePath, [IO.Path]::GetFullPath($paths.BepInExCorePath))
if ($PluginPath) { $testArguments += [IO.Path]::GetFullPath($PluginPath) }
& (Join-Path $projectRoot ".local\bin\$LoaderTarget\ProbableCause.Tests\Release\ProbableCause.Tests.exe") @testArguments
if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE." }
