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

## Hero kits MVP notes

- **Confusion:** MVP retarget — 33% chance de escolher inimigo válido aleatório em skills inimigas; não troca Ally↔Enemy nem Self→None.
- **Juggling / ChanceToNotEndTurn:** usam `TokenType.BonusAction` + `ShouldRetainTurnForBonusAction` (Simulate + Unity turn driver).
- **Resurrection Hymn:** revive/cura aliados mortos se `canTargetDeadAllies`; não modela cutscene.
- **Passivas de árvore novas:** best-effort com `PassiveEffectKind` existentes; muitas entradas GDD (leader/companion/corruption, “+25% Defense tokens”) não têm kind dedicado — ver `passives.json` ids `w_us_*`, `b_ar_*`, `m_lf_*`, etc.
- **Bleeding token** vs `DotType.Bleed`: kits novos usam `TokenType.Bleeding` (5% MaxHp EOT); conteúdo antigo de inimigos pode manter DoT Bleed.
- **ControlledInstability / Destabilization** não decaem EOT; só consomem / disparam.
- Skills legado (`wulfric_innate_*`, `f_t*_a1`, …) permanecem no JSON para testes/inimigos; hotbar de protótipo usa innates novos via `BattleFactory`.

## Passos para uma skill nova

1. Acrescenta em `Game.Simulations/Data/skills.json`.
2. Escolhe `targetKind` da tabela. Não inventes strings.
3. Define `effectScope` se não for o alvo do hit.
4. `dotnet test Game.Tests/Game.Tests.csproj`.
5. `powershell -ExecutionPolicy Bypass -File tools/PublishGameCoreForUnity.ps1`.
6. Opcional: regenera ScriptableObjects (`Erumperem/Generate Skill Tree + Passive Assets From JSON`).

A resolução de alvos é sempre `SkillTargetResolver`. O `BattleSimulator` faz loop de dano + efeitos (e `hitCount`) sem ramificar por `TargetKind`.
