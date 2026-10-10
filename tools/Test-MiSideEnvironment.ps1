[CmdletBinding()]
param(
    [string]$GameDirectory,
    [string]$OutputPath,
    [switch]$CheckDownloads
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameDirectory)) {
    $GameDirectory = Join-Path $PSScriptRoot '..\MiSide_Dev'
}
$gameRoot = (Resolve-Path -LiteralPath $GameDirectory).Path.TrimEnd('\')

function Get-PeMachine {
    param([string]$Path)
    $stream = [IO.File]::OpenRead($Path)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        if ($reader.ReadUInt16() -ne 0x5A4D) { throw "Invalid DOS header: $Path" }
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        if ($peOffset -lt 0 -or $peOffset + 6 -gt $stream.Length) { throw 'Invalid PE offset.' }
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x4550) { throw "Invalid PE header: $Path" }
        $machine = $reader.ReadUInt16()
        switch ($machine) {
            0x8664 { return 'x64' }
            0x014C { return 'x86' }
            default { return ('0x{0:X4}' -f $machine) }
        }
    }
    finally { $reader.Dispose() }
}

function Assert-MetadataTable {
    param([string]$Name, [int]$Offset, [int]$Size, [int]$Stride)
    if ($Offset -lt 0 -or $Size -lt 0 -or [long]$Offset + $Size -gt $metadata.Length) {
        throw "Metadata table is outside the file: $Name"
    }
    if ($Size % $Stride -ne 0) { throw "Unexpected record size in metadata table: $Name" }
}

function Get-MetadataString {
    param([int]$Index)
    if ($Index -lt 0 -or $Index -ge $stringsSize) { throw 'Invalid metadata string index.' }
    $start = $stringsOffset + $Index
    $end = $start
    while ($end -lt $stringsOffset + $stringsSize -and $metadata[$end] -ne 0) { $end++ }
    if ($end -eq $stringsOffset + $stringsSize) { throw 'Unterminated metadata string.' }
    return [Text.Encoding]::UTF8.GetString($metadata, $start, $end - $start)
}

$filePaths = [ordered]@{
    executable = (Join-Path $gameRoot 'MiSideFull.exe')
    unityPlayer = (Join-Path $gameRoot 'UnityPlayer.dll')
    gameAssembly = (Join-Path $gameRoot 'GameAssembly.dll')
    metadata = (Join-Path $gameRoot 'MiSideFull_Data\il2cpp_data\Metadata\global-metadata.dat')
}
$fingerprints = [ordered]@{}
foreach ($entry in $filePaths.GetEnumerator()) {
    $file = Get-Item -LiteralPath $entry.Value
    $fingerprints[$entry.Key] = [ordered]@{
        path = $file.FullName
        bytes = $file.Length
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$metadata = [IO.File]::ReadAllBytes($filePaths.metadata)
if ($metadata.Length -lt 176) { throw 'The IL2CPP metadata header is truncated.' }
$magic = [BitConverter]::ToUInt32($metadata, 0)
$metadataVersion = [BitConverter]::ToInt32($metadata, 4)
if ($magic -ne [Convert]::ToUInt32('FAB11BAF', 16)) {
    throw 'The standard IL2CPP metadata signature was not found.'
}
if ($metadataVersion -ne 29) {
    throw "This probe reads metadata version 29 only; observed version $metadataVersion."
}

# Version 29 table layouts are documented in Il2CppDumper's MetadataClass.cs.
$stringsOffset = [BitConverter]::ToInt32($metadata, 24)
$stringsSize = [BitConverter]::ToInt32($metadata, 28)
$methodsOffset = [BitConverter]::ToInt32($metadata, 48)
$methodsSize = [BitConverter]::ToInt32($metadata, 52)
$fieldsOffset = [BitConverter]::ToInt32($metadata, 96)
$fieldsSize = [BitConverter]::ToInt32($metadata, 100)
$typesOffset = [BitConverter]::ToInt32($metadata, 160)
$typesSize = [BitConverter]::ToInt32($metadata, 164)
Assert-MetadataTable 'strings' $stringsOffset $stringsSize 1
Assert-MetadataTable 'methods' $methodsOffset $methodsSize 32
Assert-MetadataTable 'fields' $fieldsOffset $fieldsSize 12
Assert-MetadataTable 'types' $typesOffset $typesSize 88

$wantedTypes = @('Dialogue_3DText', 'MitaPerson', 'PlayerMove', 'PlayerPerson',
    'Character_Look', 'Location34_Communication')
$types = [ordered]@{}
for ($offset = $typesOffset; $offset -lt $typesOffset + $typesSize; $offset += 88) {
    $name = Get-MetadataString ([BitConverter]::ToInt32($metadata, $offset))
    if ($name -notin $wantedTypes) { continue }
    $namespace = Get-MetadataString ([BitConverter]::ToInt32($metadata, $offset + 4))
    if ($namespace -ne '') { continue }
    if ($types.Contains($name)) { throw "Ambiguous game type: $name" }
    $fieldStart = [BitConverter]::ToInt32($metadata, $offset + 32)
    $methodStart = [BitConverter]::ToInt32($metadata, $offset + 36)
    $methodCount = [BitConverter]::ToUInt16($metadata, $offset + 64)
    $fieldCount = [BitConverter]::ToUInt16($metadata, $offset + 68)
    if ($fieldCount -gt 0 -and ($fieldStart -lt 0 -or
        ([long]$fieldStart + $fieldCount) * 12 -gt $fieldsSize)) { throw 'Invalid field range.' }
    if ($methodCount -gt 0 -and ($methodStart -lt 0 -or
        ([long]$methodStart + $methodCount) * 32 -gt $methodsSize)) { throw 'Invalid method range.' }
    $fieldNames = @(for ($i = 0; $i -lt $fieldCount; $i++) {
        $record = $fieldsOffset + ($fieldStart + $i) * 12
        Get-MetadataString ([BitConverter]::ToInt32($metadata, $record))
    })
    $methods = @(for ($i = 0; $i -lt $methodCount; $i++) {
        $record = $methodsOffset + ($methodStart + $i) * 32
        $declaringType = [BitConverter]::ToInt32($metadata, $record + 4)
        if ($declaringType -ne ($offset - $typesOffset) / 88) { throw 'Method ownership mismatch.' }
        [ordered]@{
            name = (Get-MetadataString ([BitConverter]::ToInt32($metadata, $record)))
            parameterCount = [BitConverter]::ToUInt16($metadata, $record + 30)
        }
    })
    $types[$name] = [ordered]@{
        namespace = $namespace
        fields = $fieldNames
        methods = $methods
    }
}

$requiredFields = @('textPrint', 'speaker', 'timeShow', 'indexString', 'nextText',
    'eventFinish', 'eventFinishOk', 'eventFinishPrint', 'dontVoice')
$missing = [Collections.Generic.List[string]]::new()
foreach ($name in @('Dialogue_3DText', 'MitaPerson', 'PlayerMove')) {
    if (-not $types.Contains($name)) { $missing.Add($name) }
}
foreach ($name in $requiredFields) {
    if (-not $types.Contains('Dialogue_3DText') -or
        $name -notin $types['Dialogue_3DText'].fields) { $missing.Add("Dialogue_3DText.$name") }
}
foreach ($name in @('dontMove', 'stopMouseMove', 'blockInteractive')) {
    if (-not $types.Contains('PlayerMove') -or
        $name -notin $types['PlayerMove'].fields) { $missing.Add("PlayerMove.$name") }
}

$unityInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($filePaths.unityPlayer)
$unityVersion = [regex]::Match($unityInfo.ProductVersion, '\d+\.\d+\.\d+(?:[abfp]\d+)?').Value
$normalizedUnityVersion = [regex]::Match($unityVersion, '\d+\.\d+\.\d+').Value
$versionFile = Join-Path $gameRoot 'Data\Version.txt'
$gameVersion = if (Test-Path -LiteralPath $versionFile) {
    (Get-Content -LiteralPath $versionFile -Raw).Trim()
} else { $null }
$architectures = [ordered]@{}
foreach ($key in @('executable', 'unityPlayer', 'gameAssembly')) {
    $architectures[$key] = Get-PeMachine $filePaths[$key]
}
$allX64 = @($architectures.Values | Where-Object { $_ -ne 'x64' }).Count -eq 0
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$sdks = if ($null -ne $dotnet) { @(& $dotnet.Source --list-sdks) } else { @() }
if ($null -ne $dotnet -and $LASTEXITCODE -ne 0) { throw 'The .NET SDK inventory failed.' }

$downloadChecks = @()
if ($CheckDownloads) {
    $urls = @(
        "https://unity.bepinex.dev/libraries/$normalizedUnityVersion.zip",
        'https://github.com/BepInEx/BepInEx/releases/download/v6.0.0-pre.2/BepInEx-Unity.IL2CPP-win-x64-6.0.0-pre.2.zip'
    )
    $downloadChecks = @(foreach ($url in $urls) {
        try {
            $response = Invoke-WebRequest -Uri $url -Method Head -TimeoutSec 20 -UseBasicParsing
            [ordered]@{ url = $url; reachable = $true; statusCode = [int]$response.StatusCode }
        }
        catch {
            [ordered]@{ url = $url; reachable = $false; error = $_.Exception.Message }
        }
    })
}

$compatibleSdkPresent = @($sdks | Where-Object {
    [version](($_ -split ' ')[0]) -ge [version]'6.0'
}).Count -gt 0
$staticReady = $missing.Count -eq 0 -and $allX64 -and $compatibleSdkPresent
$report = [ordered]@{
    schemaVersion = 1
    inspectedAtUtc = [DateTime]::UtcNow.ToString('o')
    status = $(if ($staticReady) { 'READY_FOR_LOADER_PROBE' } else { 'STATIC_CHECKS_FAILED' })
    game = [ordered]@{
        directory = $gameRoot
        versionLabel = $gameVersion
        unityVersion = $unityVersion
        metadataVersion = $metadataVersion
        metadataTypeCount = ($typesSize / 88)
        architectures = $architectures
    }
    fingerprints = $fingerprints
    installedDotnetSdks = $sdks
    loader = [ordered]@{
        bepinexCorePresent = (Test-Path -LiteralPath (Join-Path $gameRoot 'BepInEx\core\BepInEx.Unity.IL2CPP.dll'))
        bepinexGameInteropPresent = (Test-Path -LiteralPath (Join-Path $gameRoot 'BepInEx\interop\Assembly-CSharp.dll'))
        melonLoaderDirectoryPresent = (Test-Path -LiteralPath (Join-Path $gameRoot 'MelonLoader'))
        winhttpProxyPresent = (Test-Path -LiteralPath (Join-Path $gameRoot 'winhttp.dll'))
    }
    types = $types
    missingRequiredMembers = @($missing.ToArray())
    downloadChecks = $downloadChecks
    runtimeVerified = $false
    runtimeProbeStatus = 'NOT_RUN'
    limitations = @(
        'Metadata members were parsed by declaring type; native method execution was not tested.',
        'Scene objects, prefab availability, dialogue effects, and input ownership require an in-game probe.',
        'HTTP HEAD success proves reachability only, not archive integrity or loader compatibility.'
    )
    metadataFormatReference = 'https://github.com/Perfare/Il2CppDumper/blob/4741d46ba9cd6159c5d853eb9d6fc48b4bfa2b1a/Il2CppDumper/Il2Cpp/MetadataClass.cs'
}

$json = $report | ConvertTo-Json -Depth 10
if ($OutputPath) {
    $absoluteOutput = [IO.Path]::GetFullPath($OutputPath)
    if ($absoluteOutput.Equals($gameRoot, [StringComparison]::OrdinalIgnoreCase) -or
        $absoluteOutput.StartsWith($gameRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Write the report outside the game directory.'
    }
    $parent = [IO.Path]::GetDirectoryName($absoluteOutput)
    [void][IO.Directory]::CreateDirectory($parent)
    [IO.File]::WriteAllText($absoluteOutput, $json, [Text.UTF8Encoding]::new($false))
}
$json
if (-not $staticReady) { exit 1 }
