# Authoring de skills

Criar uma skill ou passiva nova é **duplicar um ScriptableObject**, preencher o Inspector, e exportar o catálogo. Não edites JSON à mão e não abras um `switch` no simulador.

Fonte do designer: `CombatAbilityAsset` em `Assets/_Project/ScriptableObjects/Combat/` (Create → Erumperem/Combat/Ability).  
Contrato de runtime (Unity, `Game.Core`, testes, CLI): **um** JSON em `Assets/StreamingAssets/Data/` (`skills.json`, `passives.json`, `skill_trees.json`). `enemies.json` não é reescrito por este export.

## Passos

1. Project window → duplica um `CombatAbilityAsset` existente (ex. `wulfric_innate_active1`).
2. Preenche `abilityId` em **snake_case**, `displayName`, `ownerCharacterId` (`wulfric` / `buck` / `maria`, ou id de inimigo). Não uses Matsuda como kit de combate.
3. `abilityKind`: Active ou Passive. `placement`: InnateActive, TreeNode, LeaderPassive, CompanionPassive, CorruptionPassive, EnemyPassive.
4. TreeNode: `treeIndex` 1–3, `tierIndex` 1–3, `passiveIndex` 1–3 (passivas). CorruptionPassive: `corruptionMinTier`.
5. Activa: todos os campos de combate (dano, `hitCount`, follow-ups, `effectsOnHit` com AmountMax / ScaleFromToken / EffectScope). Passiva: `requiredPartyRole`, chance, caps, listas Conditions + Effects (o motor avalia-as em combate).
6. Opcional: arrasta o asset para o `CharacterCombatKitAsset` do herói (organização). O export **varre todos** os `CombatAbilityAsset` da pasta de Combat / Resources.
7. Menu **Erumperem/Combat/Export Catalog**.
8. Play Mode e `dotnet test Game.Tests/Game.Tests.csproj` leem o mesmo `StreamingAssets/Data`.

Convenções de id (kits Wulfric / Buck / Maria no catálogo):

- Inatas: `{hero}_innate_active1` … `active4`
- Árvore: `{hero}_tree{N}_tier{M}_passive{K}` e `{hero}_tree{N}_tier{M}_active`
- Role: `{hero}_leader_passive1`, `{hero}_companion_passive1`
- Corrupção: `{hero}_corruption_tier{N}_passive1`

Não cries `*_innate_passive*`. Não inventes kit Matsuda (`characterId` de progressão da Star é `maria`).

`tools/PublishGameCoreForUnity.ps1` só publica a DLL de `Game.Core`. Não copia JSON.

## Contrato runtime (`SkillDefinition`)

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

### Campos opcionais em `SkillDefinition`

| Campo | Uso |
|---|---|
| `hitCount` | Hits independentes por alvo (Unload=3, Frenzy=5). Default 1. |
| `chanceToNotEndTurn` | 0..1; em sucesso concede `BonusAction`. |
| `followUpSkillIds` | Resolve skills em sequência (Guns for all). |
| `grantsBonusActionsToAllies` | Juggling: `BonusAction` em self+aliados. |
| `accuracyPenaltyPerLivingEnemy` | Juggling −10% por inimigo vivo. |
| `bonusDamagePerOwnToken` / `bonusDamagePerOwnTokenStacks` | Shield Charge +1 por Defense. |
| `computeFromDebuffTypesOnTarget` + `damagePerDistinctDebuffType` / crit / accuracy | Strangle. |
| `canTargetDeadAllies` | Resurrection Hymn inclui cadáveres no pool. |

Accuracy da skill pode ser **> 100%** (ex. 2.0); a hit chance final clampa a 1.0 depois de tokens.

### Campos opcionais em `EffectSpec`

| Campo | Uso |
|---|---|
| `amountMax` | HealHp: roll `[potency, amountMax]`. |
| `scaleFromToken` / `scaleStacksPerSourceStack` / `scaleStacksSourceDivisor` | Stacks extras (Whip Sword, Protect The Weak). |
| `steps` em `ConsumeAllTokenStacksDealDamagePerStack` | Self-damage por stack consumido (Loss of control). |

## `effectsOnHit`

Tipos: `ApplyToken`, `ApplyDot`, `ApplyRandomDot`, `Push`, `Pull`, `ApplyStun`, `HealHp`, `HealHpPercent`, `RemoveAllDebuffTokens`, `ConsumeAllTokenStacksDealDamagePerStack`, `ConsumeAllTokenStacksHealPerStack`, `SelfDamageFlat`, `TriggerDestabilizationOnTargets`, `ApplyBonusAction`.

### Combo (removido)

`comboBonus` **já não existe**. Se reaparecer no JSON, o load **falha**. `TokenType.Combo` continua aplicável; sem payoff automático.

### Cura (`HealHp` / `HealHpPercent`)

**Desbloqueada** via `CombatHealUnlock.IsCombatHealingUnlocked = true`. O applicator cura HP (e pode reviver com `canTargetDeadAllies`). Se `IsCombatHealingUnlocked` for false, volta ao log `[FORBIDDEN]`.

## Notas de runtime (kits actuais)

- **Confusion:** MVP retarget — 33% chance de escolher inimigo válido aleatório em skills inimigas; não troca Ally↔Enemy nem Self→None.
- **Juggling / ChanceToNotEndTurn:** usam `TokenType.BonusAction` + `ShouldRetainTurnForBonusAction` (Simulate + Unity turn driver).
- **Resurrection Hymn:** revive/cura aliados mortos se `canTargetDeadAllies`; não modela cutscene.
- **Passivas:** kits de herói usam Conditions + Effects. `PassiveEffectKind` legado permanece só para conteúdo de inimigo (ex. Horse Boss summon) e testes sintéticos. Não acrescentes cases one-off.
- **Bleeding token** vs `DotType.Bleed`: kits novos usam `TokenType.Bleeding` (5% MaxHp EOT); conteúdo antigo de inimigos pode manter DoT Bleed.
- **ControlledInstability / Destabilization** não decaem EOT; só consomem / disparam.
- Horse Boss (`horse_boss_*` skills + `horse_boss_summon_fairy_on_hp_tier`) permanece no JSON; o export faz upsert e **não apaga** entradas sem SO.

A resolução de alvos é sempre `SkillTargetResolver`. O `BattleSimulator` faz loop de dano + efeitos (e `hitCount`) sem ramificar por `TargetKind`.
