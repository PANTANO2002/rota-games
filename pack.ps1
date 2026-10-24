param(
    [string]$DotNet = 'dotnet',
    [string]$VpkDll = '',
    [string]$Version = '0.9.0',
    [string]$OutputDir = (Join-Path $PSScriptRoot 'Releases')
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$root = $PSScriptRoot
$releasePath = [IO.Path]::GetFullPath($OutputDir)
$publishPath = Join-Path $root 'app'
New-Item -ItemType Directory -Force -Path $releasePath | Out-Null
& $DotNet publish (Join-Path $root 'src/RotaGame.App/RotaGame.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None "-p:Version=$Version" -o $publishPath --nologo
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar.' }
Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination $publishPath -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'THIRD_PARTY.md') -Destination $publishPath -Force
# NuGet metadata and the Windows file properties use the requested signature.
$packArgs = @('pack', '--packId', 'RotaGame', '--packVersion', $Version, '--packDir', $publishPath, '--mainExe', 'RotaGame.exe', '--packTitle', 'RotaGame', '--packAuthors', 'PANT_CORE', '--icon', (Join-Path $root 'src/RotaGame.App/Assets/RotaGame.ico'), '--releaseNotes', (Join-Path $root 'docs/RELEASE-NOTES.md'), '--runtime', 'win-x64', '--channel', 'win', '--delta', 'None', '--outputDir', $releasePath)
if ($VpkDll) { & $DotNet $VpkDll @packArgs } else { & vpk @packArgs }
if ($LASTEXITCODE -ne 0) { throw 'Falha ao empacotar com Velopack.' }
$coreSetup = Join-Path $releasePath 'RotaGame-win-Setup.exe'
if (!(Test-Path -LiteralPath $coreSetup)) { throw 'Instalador base não encontrado.' }
$hash = (Get-FileHash -LiteralPath $coreSetup -Algorithm SHA256).Hash
$generated = Join-Path $releasePath 'Bootstrap.PackageInfo.cs'
@"
using System.Reflection;
[assembly: AssemblyTitle("RotaGame Setup")]
[assembly: AssemblyProduct("RotaGame")]
[assembly: AssemblyCompany("PANT_CORE")]
[assembly: AssemblyCopyright("Copyright © 2026 PANT_CORE")]
[assembly: AssemblyVersion("$Version.0")]
[assembly: AssemblyFileVersion("$Version.0")]
internal static class PackageInfo { internal const string Sha256 = "$hash"; internal const string Version = "$Version"; }
"@ | Set-Content -LiteralPath $generated -Encoding UTF8
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$bootstrap = Join-Path $releasePath 'RotaGame-Setup.exe'
& $compiler /nologo /target:winexe /optimize+ /platform:x64 "/win32manifest:$(Join-Path $root 'installer/app.manifest')" "/win32icon:$(Join-Path $root 'src/RotaGame.App/Assets/RotaGame.ico')" /reference:System.Windows.Forms.dll "/resource:$coreSetup,RotaGame.Setup" "/out:$bootstrap" (Join-Path $root 'installer/Bootstrap.cs') $generated
if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar o instalador administrativo.' }
# Keep public payloads explicit. assets.win.json describes the internal base
# setup, so do not publish that helper manifest or use vpk upload on this folder.
$publicNames = @('RotaGame-Setup.exe', 'RotaGame-win-Portable.zip', ('RotaGame-' + $Version + '-full.nupkg'), 'releases.win.json')
$lines = foreach ($name in $publicNames) {
    $path = Join-Path $releasePath $name
    if (!(Test-Path -LiteralPath $path)) { throw "Arquivo esperado ausente: $name" }
    ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()) + '  ' + $name
}
$lines | Set-Content -LiteralPath (Join-Path $releasePath 'SHA256SUMS.txt') -Encoding UTF8
Write-Host "Pacotes prontos em $releasePath (PANT_CORE). Publique apenas os quatro arquivos listados em SHA256SUMS.txt e o próprio SHA256SUMS.txt."
