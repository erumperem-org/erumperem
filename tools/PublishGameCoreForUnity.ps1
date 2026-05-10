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

$dataSrc = Join-Path $root "Game.Simulations\Data"
$dataDst = Join-Path $root "Assets\StreamingAssets\Data"
if (Test-Path $dataSrc) {
    if (-not (Test-Path $dataDst)) {
        New-Item -ItemType Directory -Path $dataDst -Force | Out-Null
    }
    foreach ($name in @("skill_trees.json", "skills.json", "passives.json")) {
        $srcFile = Join-Path $dataSrc $name
        if (Test-Path $srcFile) {
            Copy-Item $srcFile $dataDst -Force
            Write-Host "Copiado: $name -> StreamingAssets\Data"
        }
    }
}

Write-Host "Feito. Atualize assets no Unity se necessário."
