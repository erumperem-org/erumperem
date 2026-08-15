# Generates Buck placeholder skill tree data from Wulfric clones.
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '..\Game.Simulations\Data'
$stream = Join-Path $PSScriptRoot '..\Assets\StreamingAssets\Data'

function Read-Json($path) { Get-Content -Raw -Path $path | ConvertFrom-Json }
function Write-Json($path, $obj) {
    $json = $obj | ConvertTo-Json -Depth 20
    [System.IO.File]::WriteAllText($path, $json + "`n")
}

function Remap-NodeId([string]$nodeId) {
    if ($nodeId.StartsWith('b_')) { return $nodeId }
    return "b_$nodeId"
}

function Remap-SkillRef([string]$skillId) {
    if ($skillId -like 'wulfric_innate_*') {
        return $skillId.Replace('wulfric_innate_', 'buck_innate_')
    }
    if ($skillId -match '^[fma]_t\d+_') { return "b_$skillId" }
    return $skillId
}

# skill_trees.json
$treesPath = Join-Path $root 'skill_trees.json'
$trees = @(Read-Json $treesPath)
if (-not ($trees | Where-Object { $_.characterId -eq 'buck' })) {
    $wulfric = $trees | Where-Object { $_.characterId -eq 'wulfric' } | Select-Object -First 1
    $buck = $wulfric | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $buck.characterId = 'buck'
    foreach ($tree in $buck.trees) {
        foreach ($tier in $tree.tiers) {
            foreach ($node in $tier.nodes) {
                $node.id = Remap-NodeId $node.id
                $node.requires = @($node.requires | ForEach-Object { Remap-NodeId $_ })
            }
        }
    }
    $trees += $buck
}
foreach ($dir in @($root, $stream)) { Write-Json (Join-Path $dir 'skill_trees.json') $trees }

# skills.json
$skillsPath = Join-Path $root 'skills.json'
$skills = @(Read-Json $skillsPath)
$byId = @{}
foreach ($s in $skills) { $byId[$s.id] = $s }
$existing = [System.Collections.Generic.HashSet[string]]::new([string[]]($skills | ForEach-Object { $_.id }))

function Transform-Skill($skill, [string]$newId) {
    $clone = $skill | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $clone.id = $newId
    $nameMap = @{
        'Rasgar tendão' = 'Tiro incendiário'
        'Fio candente' = 'Rajada flamejante'
        'Execução de leilão' = 'Execução do pistoleiro'
        'Remendar couraça' = 'Reforço de couro'
        'Muralha' = 'Barricada'
        'Salvaguarda' = 'Último recurso'
        'Fio da anomalia' = 'Fio do revólver'
        'Puxar o véu' = 'Puxar o gatilho'
        'Abrir o vão' = 'Abrir fogo'
        'Talho direto' = 'Disparo rápido'
        'Empurrão brutal' = 'Empurrão do coldre'
        'Postura de lobo' = 'Postura do duelista'
    }
    if ($nameMap.ContainsKey($clone.name)) { $clone.name = $nameMap[$clone.name] }
    if ($clone.baseDamage) {
        $clone.baseDamage.min = [Math]::Max(0, [int]$clone.baseDamage.min + 1)
        $clone.baseDamage.max = [Math]::Max([int]$clone.baseDamage.min, [int]$clone.baseDamage.max + 2)
    }
    foreach ($listName in @('effectsOnHit', 'comboBonus')) {
        $list = $clone.$listName
        if ($null -eq $list) { continue }
        foreach ($eff in $list) {
            if ($eff.type -eq 'ApplyDot' -and $eff.dot -eq 'Bleed') { $eff.dot = 'Burn' }
        }
    }
    return $clone
}

$innateIds = @('wulfric_innate_cleave', 'wulfric_innate_shove', 'wulfric_innate_guard')
foreach ($iid in $innateIds) {
    $bid = Remap-SkillRef $iid
    if (-not $existing.Contains($bid) -and $byId.ContainsKey($iid)) {
        $skills += Transform-Skill $byId[$iid] $bid
        [void]$existing.Add($bid)
    }
}
foreach ($s in @($skills)) {
    if ($s.id -match '^[fma]_t\d+_') {
        $bid = "b_$($s.id)"
        if (-not $existing.Contains($bid) -and $byId.ContainsKey($s.id)) {
            $skills += Transform-Skill $byId[$s.id] $bid
            [void]$existing.Add($bid)
        }
    }
}
foreach ($dir in @($root, $stream)) { Write-Json (Join-Path $dir 'skills.json') $skills }

# passives.json
$passivesPath = Join-Path $root 'passives.json'
$passives = @(Read-Json $passivesPath)
$existingP = [System.Collections.Generic.HashSet[string]]::new([string[]]($passives | ForEach-Object { $_.id }))

foreach ($p in @($passives)) {
    if ($p.id -match '^[fma]_t\d+_' -and -not $p.id.StartsWith('b_')) {
        $bp = $p | ConvertTo-Json -Depth 10 | ConvertFrom-Json
        $bp.id = "b_$($p.id)"
        foreach ($key in @('skillId', 'prerequisiteSkillId')) {
            if ($bp.PSObject.Properties.Name -contains $key -and $bp.$key) {
                $bp.$key = Remap-SkillRef $bp.$key
            }
        }
        if ($bp.dotType -eq 'Bleed') { $bp.dotType = 'Burn' }
        if (-not $existingP.Contains($bp.id)) {
            $passives += $bp
            [void]$existingP.Add($bp.id)
        }
    }
}
foreach ($dir in @($root, $stream)) { Write-Json (Join-Path $dir 'passives.json') $passives }

Write-Host "Buck data cloned. Trees=$($trees.Count) Skills=$($skills.Count) Passives=$($passives.Count)"
