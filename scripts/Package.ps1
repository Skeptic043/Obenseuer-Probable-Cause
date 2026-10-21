param(
    [string]$GamePath = '',
    [string]$BepInEx5CorePath = '',
    [string]$BepInEx6CorePath = ''
)
$ErrorActionPreference = 'Stop'
$env:MSBuildEnableWorkloadResolver = 'false'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Get-RelativePath([string]$root, [string]$path) {
    $prefix = [IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
    $full = [IO.Path]::GetFullPath($path)
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "$path is outside $root." }
    return $full.Substring($prefix.Length).Replace('\','/')
}

function Get-SortedNames([string[]]$names) {
    $sorted = [string[]]@($names | ForEach-Object { $_.Replace('\','/') })
    [Array]::Sort($sorted, [StringComparer]::Ordinal)
    return ,$sorted
}

# Entries use forward slashes and a fixed ordinal order on Windows PowerShell 5.1 and PowerShell 7.
function Write-Zip([string]$path, [hashtable]$entries) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    $archive = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in (Get-SortedNames @($entries.Keys))) {
            $source = $entries[$name]
            $entry = $archive.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [DateTimeOffset](Get-Item -LiteralPath $source).LastWriteTime
            $output = $entry.Open()
            try {
                $reader = [IO.File]::OpenRead($source)
                try { $reader.CopyTo($output) } finally { $reader.Dispose() }
            }
            finally { $output.Dispose() }
        }
    }
    finally { $archive.Dispose() }
}

function Get-ZipEntryNames([string]$path) {
    $archive = [IO.Compression.ZipFile]::OpenRead($path)
    try { return ,[string[]]@($archive.Entries | ForEach-Object { $_.FullName }) }
    finally { $archive.Dispose() }
}

function Get-TopFolder([string]$path) {
    $full = [IO.Path]::GetFullPath($path)
    $root = [IO.Path]::GetPathRoot($full)
    $first = $full.Substring($root.Length).Split('\')[0]
    if ($first) { return (Join-Path $root $first) }
}

function Resolve-Paths([string]$loaderTarget, [string]$corePath) {
    $properties = @("-p:LoaderTarget=$loaderTarget")
    if ($GamePath) { $properties += '-p:GamePath=' + $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($GamePath).TrimEnd('\') }
    if ($corePath) { $properties += '-p:BepInExCorePath=' + $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($corePath).TrimEnd('\') }
    $resolved = (& dotnet msbuild (Join-Path $projectRoot 'ProbableCause.csproj') -getProperty:GamePath -getProperty:BepInExCorePath @properties -nologo) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "Could not resolve $loaderTarget paths (exit code $LASTEXITCODE)." }
    return ($resolved | ConvertFrom-Json).Properties
}

# Resolve omitted paths as the builds do, then pass them explicitly to every check.
$bep5Paths = Resolve-Paths 'BepInEx5' $BepInEx5CorePath
$bep6Paths = Resolve-Paths 'BepInEx6' $BepInEx6CorePath
if (-not $bep5Paths.GamePath) { throw 'GamePath is not set. Pass -GamePath or create .local/LocalPaths.props.' }
$gamePath = [IO.Path]::GetFullPath($bep5Paths.GamePath).TrimEnd('\')
if ($gamePath -ne [IO.Path]::GetFullPath($bep6Paths.GamePath).TrimEnd('\')) { throw 'Loader targets resolved different GamePath values.' }
$bep5CorePath = [IO.Path]::GetFullPath($bep5Paths.BepInExCorePath).TrimEnd('\')
$bep6CorePath = [IO.Path]::GetFullPath($bep6Paths.BepInExCorePath).TrimEnd('\')

foreach ($target in @(@('BepInEx5',$bep5CorePath), @('BepInEx6',$bep6CorePath))) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -GamePath $gamePath -BepInExCorePath $target[1] -LoaderTarget $target[0]
    & (Join-Path $PSScriptRoot 'Test.ps1') -GamePath $gamePath -BepInExCorePath $target[1] -LoaderTarget $target[0]
}
$bep5Dll = Join-Path $projectRoot '.local\bin\BepInEx5\ProbableCause\Release\ProbableCause.dll'
$bep6Dll = Join-Path $projectRoot '.local\bin\BepInEx6\ProbableCause\Release\ProbableCause.dll'
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($bep5Dll).Version
$version = '{0}.{1}.{2}' -f $assemblyVersion.Major,$assemblyVersion.Minor,$assemblyVersion.Build
if ([Reflection.AssemblyName]::GetAssemblyName($bep6Dll).Version -ne $assemblyVersion) { throw 'BepInEx 5 and BepInEx 6 builds have different assembly versions.' }

Add-Type -Path (Join-Path $bep5CorePath 'Mono.Cecil.dll')
function Get-AssemblyReferenceNames([string]$path) {
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
    try { return ,[string[]]@($assembly.MainModule.AssemblyReferences | ForEach-Object { $_.Name }) }
    finally { $assembly.Dispose() }
}
$bep5References = Get-AssemblyReferenceNames $bep5Dll
$bep6References = Get-AssemblyReferenceNames $bep6Dll
if ($bep5References -notcontains 'BepInEx' -or $bep5References -contains 'BepInEx.Core' -or $bep5References -contains 'BepInEx.Unity.Mono') {
    throw 'The BepInEx 5 candidate has the wrong loader references.'
}
if ($bep6References -contains 'BepInEx' -or $bep6References -notcontains 'BepInEx.Core' -or $bep6References -notcontains 'BepInEx.Unity.Mono') {
    throw 'The BepInEx 6 candidate has the wrong loader references.'
}

$checkRoot = Join-Path $projectRoot ('.local\package-checks\' + [Guid]::NewGuid().ToString('N'))
$sourceStage = Join-Path $checkRoot 'source-staged'
$dist = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $checkRoot,$sourceStage,$dist -Force | Out-Null
$stagedZip = Join-Path $checkRoot "ProbableCause-$version.zip"
$universalZip = Join-Path $dist "ProbableCause-$version.zip"

# Explicit allowlists. Ignore rules alone are not a package policy.
$universalEntries = @{
    'BepInEx/plugins/ProbableCause/ProbableCause.dll' = $bep6Dll
    'BepInEx/plugins/ProbableCause/ProbableCause.BepInEx5.dll' = $bep5Dll
    'BepInEx/plugins/ProbableCause/README.md' = Join-Path $projectRoot 'README.md'
    'BepInEx/plugins/ProbableCause/CHANGELOG.md' = Join-Path $projectRoot 'CHANGELOG.md'
    'BepInEx/plugins/ProbableCause/LICENSE' = Join-Path $projectRoot 'LICENSE'
}
$sourceFiles = @('.gitignore','.gitattributes','README.md','BUILDING.md','CHANGELOG.md','LICENSE',
    'Directory.Build.props','ProbableCause.csproj','ProbableCause.Tests.csproj','nuget.config',
    'scripts/Build.ps1','scripts/Test.ps1','scripts/Package.ps1')
$sourceFiles += @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src'),(Join-Path $projectRoot 'tests') -Recurse -File -Filter '*.cs' |
    ForEach-Object { Get-RelativePath $projectRoot $_.FullName })
if (@($sourceFiles | Where-Object { $_ -like '.local/*' }).Count -ne 0) { throw 'Source allowlist includes .local content.' }
foreach ($relative in $sourceFiles) {
    $source = Join-Path $projectRoot $relative
    $destination = Join-Path $sourceStage $relative
    $parent = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    Copy-Item -LiteralPath $source -Destination $destination
}

Write-Zip $stagedZip $universalEntries

$expectedFiles = Get-SortedNames @($universalEntries.Keys)
$names = Get-ZipEntryNames $stagedZip
if (@($names | Where-Object { $_.Contains('\') }).Count -ne 0) { throw "$stagedZip has backslash entry names." }
if (($names -join "`n") -cne ($expectedFiles -join "`n")) { throw "$stagedZip entries differ from the explicit allowlist or order." }
if ($names.Count -ne 5) { throw "$stagedZip must contain exactly five files." }

$universalExtract = Join-Path $checkRoot 'universal-extracted'
[IO.Compression.ZipFile]::ExtractToDirectory($stagedZip, $universalExtract)
$actualPluginFiles = Get-SortedNames @(Get-ChildItem -LiteralPath $universalExtract -Recurse -File -Force | ForEach-Object { Get-RelativePath $universalExtract $_.FullName })
if (($actualPluginFiles -join "`n") -cne ($expectedFiles -join "`n")) { throw "$universalExtract differs from its explicit five-file allowlist." }
$packagedDlls = @(Get-ChildItem -LiteralPath $universalExtract -Recurse -File -Filter '*.dll')
if ($packagedDlls.Count -ne 2) { throw "$universalExtract must contain exactly two Probable Cause DLLs." }
$pluginFolder = Join-Path $universalExtract 'BepInEx\plugins\ProbableCause'
foreach ($check in @(@($bep5Dll,'ProbableCause.BepInEx5.dll'), @($bep6Dll,'ProbableCause.dll'))) {
    $extractedDll = Join-Path $pluginFolder $check[1]
    if ((Get-FileHash -LiteralPath $check[0]).Hash -ne (Get-FileHash -LiteralPath $extractedDll).Hash) {
        throw "$extractedDll differs from its verified build."
    }
}
foreach ($target in @(@('BepInEx5',$bep5CorePath), @('BepInEx6',$bep6CorePath))) {
    & (Join-Path $PSScriptRoot 'Test.ps1') -GamePath $gamePath -BepInExCorePath $target[1] -LoaderTarget $target[0] -PluginPath $pluginFolder
}

# Scan extracted release files for checkout/reference roots and the account name.
$markers = @(@($projectRoot,$gamePath,$bep5CorePath,$bep6CorePath) | ForEach-Object { Get-TopFolder $_ }) + @($env:USERNAME) |
    Where-Object { $_ -and $_.Length -ge 3 } | Sort-Object -Unique
$markers = @($markers) + @($markers | ForEach-Object { $_.Replace('\','/') }) | Sort-Object -Unique
$latin1 = [Text.Encoding]::GetEncoding(28591)
foreach ($file in @(Get-ChildItem -LiteralPath $universalExtract -Recurse -File -Force)) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    foreach ($text in @($latin1.GetString($bytes), [Text.Encoding]::Unicode.GetString($bytes))) {
        foreach ($marker in $markers) {
            if ($text.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { throw "Personal path marker found in packaged file $($file.FullName)." }
        }
    }
}

# The internal allowlisted source stage is never distributed. It must reproduce both tested candidates.
foreach ($target in @(@('BepInEx5',$bep5CorePath,$bep5Dll), @('BepInEx6',$bep6CorePath,$bep6Dll))) {
    & (Join-Path $sourceStage 'scripts\Build.ps1') -GamePath $gamePath -BepInExCorePath $target[1] -LoaderTarget $target[0]
    & (Join-Path $sourceStage 'scripts\Test.ps1') -GamePath $gamePath -BepInExCorePath $target[1] -LoaderTarget $target[0] -PluginPath $pluginFolder
    $rebuiltDll = Join-Path $sourceStage ".local\bin\$($target[0])\ProbableCause\Release\ProbableCause.dll"
    if ((Get-FileHash -LiteralPath $target[2]).Hash -ne (Get-FileHash -LiteralPath $rebuiltDll).Hash) {
        throw "Fresh source rebuild for $($target[0]) differs from the candidate DLL."
    }
}
$null = Get-RelativePath $checkRoot $sourceStage
Remove-Item -LiteralPath $sourceStage -Recurse -Force
$unexpectedVersionPackages = @(Get-ChildItem -LiteralPath $dist -File -Filter "ProbableCause-$version*.zip" | Where-Object {
    [IO.Path]::GetFullPath($_.FullName) -ne [IO.Path]::GetFullPath($universalZip)
})
if ($unexpectedVersionPackages.Count -ne 0) {
    throw "dist contains another Probable Cause $version player package: $($unexpectedVersionPackages.FullName -join ', ')"
}
Copy-Item -LiteralPath $stagedZip -Destination $universalZip -Force

$record = [ordered]@{
    checkedAt = [DateTime]::UtcNow.ToString('o')
    universalZip = $universalZip
    universalZipSha256 = (Get-FileHash -LiteralPath $universalZip).Hash
    bepInEx5DllSha256 = (Get-FileHash -LiteralPath $bep5Dll).Hash
    bepInEx6DllSha256 = (Get-FileHash -LiteralPath $bep6Dll).Hash
    assemblyVersion = $assemblyVersion.ToString()
    bepInEx5References = Get-SortedNames $bep5References
    bepInEx6References = Get-SortedNames $bep6References
    files = $expectedFiles
    internalSourceFiles = Get-SortedNames $sourceFiles
    internalSourceStageRemoved = $true
    personalPathMarkersChecked = @($markers).Count
    personalPathScanClean = $true
    loaderDiscoveryExactlyOnePerTarget = $true
    bepInEx5SimulatedCachedUpgradePassed = $true
    freshSourceRebuildIdentical = $true
    gameInstallationChanged = $false
    liveGameplayTest = $false
}
$record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $checkRoot 'validation.json') -Encoding utf8
Write-Output "Personal path scan clean ($(@($markers).Count) markers)."
Write-Output "Universal candidate package validated: $universalZip"
Write-Output "Universal ZIP SHA256: $($record.universalZipSha256)"
Write-Output "BepInEx 5 DLL SHA256: $($record.bepInEx5DllSha256)"
Write-Output "BepInEx 6 DLL SHA256: $($record.bepInEx6DllSha256)"
Write-Output "Validation record: $(Join-Path $checkRoot 'validation.json')"
