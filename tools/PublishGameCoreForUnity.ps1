# Compila Game.Core para netstandard2.1 (Unity 6) e copia DLLs para Assets/_Project/Plugins/GameCore
# Raiz do repo:  powershell -File tools/PublishGameCoreForUnity.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$csproj = Join-Path $root "Game.Core\Game.Core.csproj"
$out = Join-Path $root "Assets\_Project\Plugins\GameCore"

if (-not (Test-Path $csproj)) {
    throw "Game.Core.csproj não encontrado: $csproj"
}

Write-Host "Publishing Game.Core -> $out"
dotnet publish $csproj -c Release -f netstandard2.1 /p:CopyLocalLockFileAssemblies=true -o $out
Write-Host "Feito. Atualize assets no Unity se necessário."
