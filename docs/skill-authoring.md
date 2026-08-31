# Authoring de skills

Criar uma skill nova é sobretudo **editar JSON** + passar na validação do loader. Não abras um `switch` no simulador nem copies ramos de `TargetKind`.

Fonte canónica: `Game.Simulations/Data/skills.json`  
Cópia Unity: `Assets/StreamingAssets/Data/skills.json` (via `tools/PublishGameCoreForUnity.ps1`)

## Contrato

### `targetKind` — quem se seleciona / quem recebe dano primário

| Valor | Comportamento |
|---|---|
| `OneEnemy` | Um inimigo clicado. |
| `UpToThreeEnemies` | O inimigo selecionado **+ até 2 outros inimigos vivos válidos** na ordem de apresentação (esquerda→direita / `FrontRank` crescente), **sem segundo clique**. |
| `AllEnemies` | Todos os inimigos vivos válidos. |
| `Self` | Sempre o actor; a seleção (mesmo um inimigo) é ignorada. |
| `OneAlly` | Aliado clicado; se a seleção for inválida, cai no actor. |
| `SelfOrAlly` | Aceita self ou aliado vivo; **rejeita inimigo**. Sem seleção → actor. |
| `SelfAndAlly` | Os dois (todos os vivos do mesmo lado). |

Filtros de pool inimigo: mortos fora; **Taunt** restringe o pool aos tauntadores; **Stealth** não é selecionável.

### `effectScope` — quem recebe **este** efeito relativamente ao hit

Distinto de `targetKind`. O dano primário segue `targetKind`; cada entrada em `effectsOnHit` escolhe o destinatário do efeito.

| Valor | Destinatário |
|---|---|
| `Default` | O alvo primário do hit (cada um, em skills de área). |
| `Self` | O caster, mesmo que o hit seja noutro combatente. |
| `AllAllies` | Todos os vivos do lado do caster. |
| `AllEnemies` | Todos os vivos do lado oposto. |

Scopes não-`Default` aplicam-se **uma vez** por skill (no primeiro hit que acertar), para não duplicar Block/DoT em área.

### Três exemplos

1. **Postura de lobo** (análogo a Raise Shield): `targetKind: Self`, tokens Block+Taunt com `effectScope: Default` → o caster.
2. **Muralha**: `targetKind: Self`; Taunt+BlockPlus em `Default` (self); Block com `effectScope: AllAllies` (o grupo).
3. **Iron Maiden** (padrão): `targetKind: UpToThreeEnemies`; dano e efeitos `Default` nos até 3 inimigos do hit. Sem segundo clique.

## `effectsOnHit`

Única lista de efeitos. Tipos: `ApplyToken`, `ApplyDot`, `ApplyRandomDot`, `Push`, `Pull`, `ApplyStun`, `HealHp`, `HealHpPercent`.

### Combo (removido)

`comboBonus` **já não existe**. Se reaparecer no JSON, o load **falha**. Efeitos extra vão para `effectsOnHit`. `TokenType.Combo` continua a existir como token aplicável (ex.: Para-raio, teias); não há payoff automático de combo na skill.

### Cura (`HealHp` / `HealHpPercent`)

O tipo existe no contrato. **Em combate o applicator está FORBIDDEN** (não altera HP; log `[FORBIDDEN]`). Gancho futuro: `CombatHealUnlock.IsCombatHealingUnlocked` + `CombatHealUnlock.ApplyHealHpToRecipient`. Desbloqueio previsto: Main na vila após 3s. Não ligues cura no applicator nesta fase.

### Fora desta PR

`ranks` / `cooldown` / `selfMove` **não existem** no JSON nem no modelo actual. Não os reintroduziste; se precisares deles, é trabalho futuro — o loader não os lê.

## Passos para uma skill nova

1. Acrescenta um objecto em `Game.Simulations/Data/skills.json` (`id`, `name`, `element`, `type`, `targetKind`, `baseDamage`, `baseCritChance`, `accuracy`, `effectsOnHit`, opcional `corruptionCost`, `chanceToUse`, `selfHpPercentBelow`).
2. Escolhe `targetKind` da tabela acima. Não inventes strings.
3. Para cada efeito, define `effectScope` se não for o alvo do hit (`Default`).
4. Corre `dotnet test Game.Tests/Game.Tests.csproj`. O loader rejeita `comboBonus`, `targetKind` obsoleto (`Enemy`/`Ally`) e `effectScope` desconhecido.
5. Publica para Unity: `powershell -ExecutionPolicy Bypass -File tools/PublishGameCoreForUnity.ps1`.
6. Opcional: regenera ScriptableObjects (`Erumperem/Generate Skill Tree + Passive Assets From JSON`). O inspector de `SkillTreeNodeAsset` usa os mesmos `SkillTargetKind`; o campo ComboBonus foi removido.

A resolução de alvos é sempre `SkillTargetResolver` (player, IA, preview, HUD). O `BattleSimulator` faz loop de dano + efeitos em cada alvo primário, **sem** ramificar por `TargetKind`.
