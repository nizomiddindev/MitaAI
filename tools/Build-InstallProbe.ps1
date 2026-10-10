[CmdletBinding()]
param([string]$GameDirectory)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameDirectory)) {
    $GameDirectory = Join-Path $PSScriptRoot '..\MiSide_Dev'
}
$gameRoot = (Resolve-Path -LiteralPath $GameDirectory).Path
if (Get-Process -Name MiSideFull -ErrorAction SilentlyContinue) {
    throw 'Close MiSide before installing the plugin.'
}
foreach ($relative in @('MiSideFull.exe', 'BepInEx\core\BepInEx.Unity.IL2CPP.dll',
    'BepInEx\interop\Assembly-CSharp.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $gameRoot $relative))) {
        throw "Required game/loader file is missing: $relative"
    }
}
$project = Join-Path $PSScriptRoot '..\src\MitaAI.Plugin\MitaAI.Plugin.csproj'
& dotnet build $project --configuration Release --nologo ("-p:GameDirectory=" + $gameRoot)
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed; nothing was installed.' }
$buildFile = Join-Path $PSScriptRoot '..\src\MitaAI.Plugin\bin\Release\net6.0\MitaAI.Plugin.dll'
$installFolder = Join-Path $gameRoot 'BepInEx\plugins\MitaAI'
$destination = Join-Path $installFolder 'MitaAI.Plugin.dll'
$manifestPath = Join-Path $installFolder 'install-manifest.json'
if (Test-Path -LiteralPath $destination) {
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw 'An existing plugin has no MitaAI install manifest; it was not overwritten.'
    }
    $previous = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $previous.sha256) {
        throw 'The installed plugin differs from its manifest; it was not overwritten.'
    }
}
[void][IO.Directory]::CreateDirectory($installFolder)
Copy-Item -LiteralPath $buildFile -Destination $destination
$manifest = [ordered]@{
    pluginId = 'local.mitaai.probe'
    pluginVersion = [Reflection.AssemblyName]::GetAssemblyName($buildFile).Version.ToString(3)
    installedAtUtc = [DateTime]::UtcNow.ToString('o')
    file = $destination
    sha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
}
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
Write-Output "Installed: $destination"
