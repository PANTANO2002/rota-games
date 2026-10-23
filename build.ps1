param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$root = $PSScriptRoot
& $DotNet build (Join-Path $root 'src/RotaGame.App/RotaGame.App.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar.' }
& $DotNet run --project (Join-Path $root 'tests/RotaGame.Tests/RotaGame.Tests.csproj') -c Release -- --integration
if ($LASTEXITCODE -ne 0) { throw 'Falha nos testes.' }
& $DotNet publish (Join-Path $root 'src/RotaGame.App/RotaGame.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o (Join-Path $root 'app') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar.' }
Write-Host 'Pronto: app/RotaGame.exe'
