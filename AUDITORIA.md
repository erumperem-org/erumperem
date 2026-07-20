# Auditoria de Dívida Técnica — Erumperem

> Fase: **auditoria apenas** (nenhum ficheiro de código foi modificado). Este documento é o único artefacto produzido.
> Data: 2026-07-19 · Método: 1 orquestrador + 5 subagentes de varredura (composer-2.5-fast) com escopo delimitado por domínio; cada achado validado contra o código antes de consolidar.

---

## 1. Sumário Executivo

### 1.1 Arquitetura atual

O projeto é um jogo **Unity 6 (C#)** com uma separação em camadas **parcialmente limpa**:

| Camada | Projeto / Pasta | Papel | Dependências |
| --- | --- | --- | --- |
| **Motor de combate (puro .NET)** | `Game.Core` (`net8.0` + `netstandard2.1`) | Combate por turnos headless, data-driven (JSON), sem dependências Unity | `System.Text.Json`, `PolySharp` |
| **Simulação** | `Game.Simulations` (console) | Batches determinísticos (RNG seeded), export CSV | → `Game.Core` |
| **Testes** | `Game.Tests` | Regras, passivas, iniciativa, integração de dados | → `Game.Core` |
| **Apresentação / gameplay Unity** | `Assets/_Project/Scripts` | HUD, input, exploração, tokens, inimigos, UI, áudio | → `Game.Core` |

**Fluxo de dados:** `*.json` (skills/passivas/inimigos/árvores) → `CombatDataLoader` → `BattleFactory` monta `BattleState` mutável → `BattleSimulator` + `PassiveRuleApplier`/`CombatPassiveEventBus` resolvem turnos. Na Unity, `CombatPrototypeController` (~2200 LOC) conduz o `BattleSimulator` e publica apresentação via `CombatSessionHub` (+ um `CombatPresentationHub` secundário). A exploração orbita o singleton `ExplorationLoadContext` (DontDestroyOnLoad) com persistência fragmentada em 3 JSONs; inimigos usam pool + state machine; a UI usa FSM de botões + efeitos `ScriptableObject` com DOTween.

**Pontos de entrada:** `Game.Simulations/Program.cs` (headless); cena de combate via `CombatPrototypeController`; exploração via `PlayableCharactersManager` + `ExplorationLoadContext`.

**Avaliação arquitetural:** O núcleo `Game.Core` está bem isolado e é testável — a maior força do projeto. As fraquezas concentram-se em (a) dois **god-objects** (`BattleSimulator`, `CombatPrototypeController`), (b) o **subsistema de Tokens**, cujo pipeline de sinergias tem várias falhas de correção que anulam comportamento documentado, e (c) a camada Unity, saturada de acoplamento por *service-locator* (`FindObjectByType`, singletons) e fugas de subscrição de eventos.

### 1.2 Top problemas (ordenados por prioridade global)

1. **Pipeline de Tokens quebrado** — imunidade/cancelamento fora de ordem, stacking impede fusão aditiva, `hasFired` em `struct` nunca persiste, `ComboToken` crasha. Vários comportamentos documentados nunca executam. *(crítico, correção estrutural)*
2. **Combate de inimigo não idempotente** (`NpcEnemyDetectionHandler`) — sem guard `_combatTriggered` + 3× `FindAnyObjectByType` sem null-check → double-save/double-load e possível `NullReferenceException`. *(crítico, correção pequena)*
3. **NRE em `PlayerDetectionSystem.OnExit`** e **triggers de baú invertidos** (`ChestAreaTrigger`) — bugs de gameplay com correção trivial. *(crítico, quick-win)*
4. **Dois bugs de balanceamento no motor** — `ApplyRandomDot` aplica a chance ao quadrado; `Bleed` usa resistência de `Blight`. *(alto, correção pequena)*
5. **God-objects** `CombatPrototypeController` (~2200 LOC) e `BattleSimulator` (~1100 LOC) — violam SRP e travam evolução/teste. *(alto, refactor grande)*
6. **Fugas de subscrição de eventos** em múltiplos `MonoBehaviour` poolados/UI (torch, hubs, sliders, slots) — handlers órfãos e disparos-fantasma. *(alto, transversal)*
7. **Testes dessincronizados** com a implementação (corrupção negativa; descrições PT vs. builder EN) — CI deveria falhar. *(alto, manutenção)*
8. **Acoplamento por `FindObjectByType`/singletons** e **strings mágicas** (`"Player"`, `EffectScope`) espalhados por dezenas de ficheiros. *(médio, transversal)*
9. **DOTween sem `DOKill`/`SetLink`** em toda a `UI/**` (0 ocorrências) — tweens órfãos, contra a própria regra do projeto. *(alto, transversal)*
10. **Duplicação massiva (DRY)** — raycast de pointer (×5), subscribe+catch-up de hub (×4), handlers de botão (×4), loops NavMesh, `ResolveDefault*Path` (×4), pipeline de dano sim vs. preview.

---

## 2. Tabela priorizada de achados

Prioridade = **severidade × (baixo esforço primeiro)**. `P0` = crítico e barato (quick-win) ou crítico de gameplay; `P1` = alto impacto; `P2` = médio; `P3` = baixo/limpeza. Esforço: **XS** (poucas linhas) · **S** · **M** · **L** (refactor estrutural).

| # | Achado | Arquivo:linha | Categoria | Sev. | Esforço | Prio |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | NRE em `OnExit`: `GetComponent` após guard de null | `Player/Detection/PlayerDetectionSystem.cs:149-150` | idiomático | crítico | XS | **P0** |
| 2 | Triggers de área de baú Enter/Exit invertidos | `Interactables/Chest/Pooling/ChestAreaTrigger.cs:78-87` | idiomático | crítico | XS | **P0** |
| 3 | `ApplyRandomDot` aplica a chance 2× (≈ chance²) | `Engine/BattleSimulator.cs:623,721` | idiomático | alto | XS | **P0** |
| 4 | `Bleed` usa `BlightRes` (não existe `BleedRes`) — verificar intenção | `Engine/BattleSimulator.cs:896` | idiomático | alto | XS | **P0** |
| 5 | Combate de inimigo não idempotente + 3× find sem null-check | `Enemy/Enemy/NpcEnemyDetectionHandler.cs:118-125` | concorrência | crítico | S | **P0** |
| 6 | `ComboToken.evolution` → NRE (`finisherFactory` nunca init) | `Token/.../Tokens/ComboToken.cs:19,31-33` | idiomático | crítico | S | **P0** |
| 7 | `AudioController` chama 4× `PlayBGM` no mesmo `Start` | `Sound/AudioController.cs:7` | concorrência | alto | XS | **P0** |
| 8 | `ScavengerDetectionSystem.OnExit` chama `base.OnEnter` (copy-paste) | `Exploration/Scavenger/ScavengerDetectionSystem.cs:24` | idiomático | médio | XS | **P0** |
| 9 | Guards de combate do Horse Boss nunca aplicados (`IsCombatTriggerBlocked` morto) | `Exploration/HorseBossOverworldCombatContact.cs:67,111` | concorrência | alto | S | **P1** |
| 10 | Imunidade bloqueia token entrante antes de cancelamento correr | `Token/.../Container/TokenContainerController.cs:33-39,160-174` | concorrência | crítico | M | **P1** |
| 11 | `LinearStackData` retorna `false` → `IAdditiveSynergy` nunca corre | `Token/.../Container/TokenContainerController.cs:89-95` + `PoisonToken.cs:29-36` | concorrência | crítico | M | **P1** |
| 12 | `hasFired` em `struct` (cópia) → reversão condicional nunca reverte | `Token/Contracts/Synergies/IConditionalSynergy.cs:15-30` | concorrência | crítico | M | **P1** |
| 13 | Sinergias `async` disparadas como `Action` (fire-and-forget, exceções engolidas) | `Token/.../Container/TokenContainerController.cs:181-212` | concorrência | crítico | M | **P1** |
| 14 | `Remove*ByTypes` saltam `UnApplySynergies` (efeitos órfãos) | `Token/.../Container/TokenContainerController.cs:324-362` | concorrência | alto | M | **P1** |
| 15 | Teste espera corrupção negativa; motor ignora delta<0 | `Game.Tests/UnitTest1.cs:716-749` vs `BattleSimulator.cs:1002-1007` | acoplamento | alto | S | **P1** |
| 16 | Testes de descrição em PT vs. builder gera EN | `Game.Tests/UnitTest1.cs:984-1011` vs `SkillPlayerDescriptionBuilder.cs:130-160` | acoplamento | alto | S | **P1** |
| 17 | Currency slots: classe/nome-de-ficheiro trocados + leak de evento | `UI/Inventory/CurrencySlot.cs:5` ↔ `DeterministicInventorySlotView.cs:5,16` | DRY/concorrência | alto | S | **P1** |
| 18 | `CharacterStatsView` stub vazio (nunca escreve UI) | `UI/Stats/CharacterStatsView.cs:12` | idiomático | alto | S | **P1** |
| 19 | `CharacterSelectionCanvas` nunca preenche nome do personagem | `UI/CharacterSelectionCanvas.cs:105` | idiomático | alto | XS | **P1** |
| 20 | Editor generator destrói edições manuais (`OverwriteString`/`ClearArray`) | `Editor/Progression/SkillTreePassiveAssetGenerator.cs:75,202` | concorrência | alto | S | **P1** |
| 21 | DOTween sem `DOKill`/`SetLink` em toda a `UI/**` (tweens órfãos) | `UI/Effects/ScaleEffectSO.cs:14`, `UI/States/Base/UiState.cs:31` (transversal) | concorrência | alto | M | **P1** |
| 22 | Fuga: `NpcEnemy` subscreve torch em `Awake`, nunca `-=` | `Enemy/Enemy/NpcEnemy.cs:64` | concorrência | alto | S | **P1** |
| 23 | UI expõe `BattleSimulator`/`BattleState` do engine (viola DIP) | `Combat/CombatPrototypeController.cs:167` | acoplamento | alto | L | **P1** |
| 24 | `PlayerDetectionSystem` manipula UI privada do NPC (`._canvas._panel`) | `Player/Detection/PlayerDetectionSystem.cs:153` | acoplamento | alto | S | **P1** |
| 25 | `SaveState` de exploração `async void` sem guard de reentrada | `Exploration/ExplorationLoadContext.cs:261` | concorrência | alto | S | **P1** |
| 26 | Double-fire de `Interact` (handler `performed` + polling `Update`) | `Player/Input/PlayerInputReader.cs:51,84-87` | concorrência | alto | S | **P1** |
| 27 | `ShopLevelUpButton`/`ShopScavenger` reentrada async (débito duplo) | `UI/Market/ShopLevelUpButton.cs:63`, `Scavenger/ScavengerShop.cs:50` | concorrência | alto | S | **P1** |
| 28 | `CombatPrototypeController` god-class (~2200 LOC) | `Combat/CombatPrototypeController.cs:30` | SOLID (SRP) | alto | L | **P1** |
| 29 | `BattleSimulator` monolítico (~1100 LOC) | `Engine/BattleSimulator.cs:11` | SOLID (SRP) | alto | L | **P1** |
| 30 | Aliases de inimigos hardcoded no índice (OCP) | `Data/CombatDataLoader.cs:131-139` | SOLID (OCP) | alto | S | **P1** |
| 31 | Fallback `CorruptedFairy` hardcoded no spawn (OCP) | `Engine/EnemySpawnHelper.cs:197-233` | SOLID (OCP) | alto | M | **P1** |
| 32 | Pipeline de dano duplicado (simulador vs. preview) | `Engine/BattleSimulator.cs:942-993` vs `SkillDamagePreviewCalculator.cs:156-215` | DRY | alto | M | **P1** |
| 33 | Baú concede loot e marca `IsOpened` antes de validar inventário | `Interactables/Chest/ChestInteractable.cs:56` | concorrência | médio | S | **P2** |
| 34 | Load de inventário fire-and-forget no `Awake` (race com `Clear`) | `Player/Inventory/PlayerInventorySaveSystem.cs:81,179` | concorrência | médio | S | **P2** |
| 35 | `CombatPassiveEventBusBinder` subscreve sem unbind em close | `Combat/Passives/CombatPassiveEventBusBinder.cs:23` | concorrência | médio | S | **P2** |
| 36 | `NpcEnemySpawner` empilha coroutines de respawn sem cancelar | `Enemy/Spawner/NpcEnemySpawner.cs:160` | concorrência | médio | S | **P2** |
| 37 | `IsFinished` (getter) muta estado (`SyncDeathFlagsFromHealth`) | `Models/BattleState.cs:68-75` | concorrência | médio | S | **P2** |
| 38 | Eventos de combate não determinísticos (`Guid.NewGuid`, `UtcNow`) | `Engine/BattleSimulator.cs:1074-1077` | concorrência | médio | S | **P2** |
| 39 | Switch-on-type de feedback de interactable no player (OCP/SRP) | `Player/Detection/PlayerDetectionSystem.cs:201` | SOLID | médio | M | **P2** |
| 40 | Switch-on-enum add/remove de inventário (OCP, dois pontos) | `Player/Inventory/PlayerInventorySystem.cs:95,156` | SOLID | médio | M | **P2** |
| 41 | Switch de stacking/allocation viola OCP (`TokenStackData` vazio) | `Token/.../Container/TokenContainerController.cs:48-64,78-128` | SOLID | médio | M | **P2** |
| 42 | Contracts de Token dependem de estáticos do controller concreto (DIP) | `Token/Contracts/Synergies/IImmunitySynergy.cs:19` (+ outros) | SOLID | médio | L | **P2** |
| 43 | Sinergias `IConversion`/`IConditional` nunca ligadas ao `Tick` | `Token/.../Container/TokenContainerController.cs:368-375` | acoplamento | alto | M | **P2** |
| 44 | `RefreshDurationStackData` nunca decrementa/expira | `Token/Contracts/Stacking/RefreshDurationStackData.cs:13-19` | acoplamento | alto | M | **P2** |
| 45 | View de tokens resolve GameObject por `GetType().Name` (instância errada) | `Token/.../Container/TokenContainerView.cs:101-107` | acoplamento | alto | M | **P2** |
| 46 | `ResolveDefault*Path` copy-paste ×4 | `Data/CombatDataLoader.cs:12-79` | DRY | médio | S | **P2** |
| 47 | `CreatePlayer`/`CreateEnemy` quase duplicados (~100 linhas) | `Engine/BattleFactory.cs:126-230` | DRY | médio | M | **P2** |
| 48 | IDs de skills inatas hardcoded (buck/wulfric) (OCP) | `Engine/BattleFactory.cs:27-39` | SOLID (OCP) | médio | S | **P2** |
| 49 | `EffectScope` como strings mágicas ("AllAllies"/"Self"/…) | `Engine/BattleSimulator.cs:630,652`; `Models/Definitions.cs:26` | idiomático | médio | S | **P2** |
| 50 | Identidade de player por tag string (~18 ocorrências) | `Detection/Core/ShapeEntry.cs:128` (+ ~10 ficheiros) | DRY | médio | M | **P2** |
| 51 | Detecção genérica acoplada a `PlayableCharacter` concreto | `Detection/Core/ShapeEntry.cs:113` | acoplamento | médio | M | **P2** |
| 52 | Persistência em 3 JSONs sem transação (estado inconsistente) | `Exploration/ExplorationLoadContext.cs:894,309` | concorrência | médio | M | **P2** |
| 53 | Raycast pointer→`CombatCapsuleTag` duplicado (×5, distâncias divergentes) | `Combat/CombatSkillButtonBarUIManager.cs:147` (+4) | DRY | médio | M | **P2** |
| 54 | Subscribe+catch-up de session-hub duplicado (×4) | `Combat/HUD/CombatHoverHealthBarBinder.cs:152` (+3) | DRY | médio | M | **P2** |
| 55 | Handlers de pointer de botão duplicados (×4) | `UI/Components/Button/.../ChangePanelButtonController.cs:7` (+3) | DRY | médio | M | **P2** |
| 56 | Dois event buses paralelos (Session + Presentation) | `Combat/CombatSceneViewBinder.cs:146` | acoplamento | médio | M | **P2** |
| 57 | Loop NavMesh duplicado (Wander/Patrol/GoToPoint) | `Movement/Behaviors/WanderBehavior.cs:55` (+2) | DRY | médio | S | **P2** |
| 58 | `Camera.main` em `Update`/`LateUpdate` sem cache (transversal) | `Combat/Tokens/DiegeticTokenStripWorldFollower.cs:149` (+3) | idiomático | médio | S | **P2** |
| 59 | `FindObjectByType` por instância poolada / em `OnEnable` (transversal) | `Enemy/.../NpcEnemyView.cs:49`, `Combat/CombatCapsuleAnimatorBridge.cs:31` (+vários) | acoplamento | médio | M | **P2** |
| 60 | `PlaySoundEffectSO` bypassa mixer/`AudioManager` | `UI/Effects/PlaySoundEffectSO.cs:12` | idiomático | médio | S | **P2** |
| 61 | `CharacterHoverAudio` usa input legacy (`OnMouseEnter`) | `Sound/CharacterHoverAudio.cs:8` | idiomático | médio | S | **P2** |
| 62 | `PanelInputController` bypassa `InputManager` (teclas hardcoded) | `Input/ActivateObjectByInput.cs:145` | acoplamento | médio | S | **P2** |
| 63 | `AudioManager` god-object (BGM+SFX+mixer+PlayerPrefs) + `instance` global | `Sound/AudioManager.cs:7,42` | SOLID/acoplamento | médio | L | **P2** |
| 64 | Vários singletons mutáveis globais (`CorruptionManager`, `PlayerProgressionService`) | `Combat/CorruptionManager.cs:16`, `Progression/PlayerProgressionService.cs:20` | idiomático | médio | M | **P2** |
| 65 | `EstimateDamage` da AI ignora mitigação/multiplicadores | `Engine/BattleSimulator.cs:922-939` | acoplamento | médio | S | **P2** |
| 66 | `GetTierModifiers` usa `First()` sem fallback (exceção em tier inválido) | `Config/CombatBalanceConfig.cs:27-29` | idiomático | médio | S | **P2** |
| 67 | Classes de alocação com prefixo `I` (não são interfaces) | `Token/Contracts/Allocation/IOnHitTokenAllocation.cs:10` | idiomático | médio | S | **P2** |
| 68 | `IOnEventTokenAllocation.OnSubscribe` é dead code (alocação declarativa incompleta) | `Token/Contracts/Allocation/IOnEventTokenAllocation.cs:12-17` | acoplamento | médio | M | **P2** |
| 69 | Múltiplos `SaveAsync`/`ClearSave` async sem serialização/await | `Player/Inventory/PlayerInventorySaveSystem.cs:93,118`; `ExplorationDataManagement.cs:60` | concorrência | médio | S | **P2** |
| 70 | Campos públicos mutáveis em vez de `[SerializeField] private` (transversal) | `Combat/SaveLifeSystem.cs:14`, `EncounterSpawner.cs:6`, `Playable/*`, `Currency/*`, `Token/TokenModel.cs` | idiomático | baixo | S | **P3** |
| 71 | Spin manual em `Update`/`LateUpdate` vs. DOTween (marcadores/TMP) | `Combat/CombatSceneViewBinder.cs:265`, `UI/TmpAuthoredTextEffectDriver.cs:25` | idiomático | baixo | S | **P3** |
| 72 | `FindNode` duplicado (progressão vs. simulação) | `Progression/SkillTreeLookup.cs:9-34` vs `SimulationSkillTreeSetup.cs:70-87` | DRY | baixo | S | **P3** |
| 73 | `Presentation` (traduções EN/strings UI) dentro de `Game.Core` | `Presentation/SkillPlayerDescriptionBuilder.cs:13` | acoplamento | baixo | M | **P3** |
| 74 | `PassiveRuleApplier` → `Game.Core.Engine` (dep. de camada) | `Passives/PassiveRuleApplier.cs:4-5,63-70` | acoplamento | baixo | M | **P3** |
| 75 | `OccupiedRanks` aloca `int[]` a cada acesso | `Models/CombatantComponents.cs:27-28` | idiomático | baixo | XS | **P3** |
| 76 | `HealDebugTrace.OnLog` estático mutável (global) | `Diagnostics/HealDebugTrace.cs:9` | concorrência | baixo | XS | **P3** |
| 77 | `--enemies` valida ficheiro mas descarta resultado (feature incompleta) | `Game.Simulations/Program.cs:87-90` | idiomático | baixo | XS | **P3** |
| 78 | Subclasses de currency vazias só por raridade (OCP/data) | `Currency/RareAnomalousArtifact.cs:7` (+2) | SOLID | baixo | S | **P3** |
| 79 | Duplicação de transição de material entre receivers | `Player/Detection/PlayableDetectionReceiver.cs:67` vs `InteractableDetectionReceiver.cs:52-68` | DRY | baixo | S | **P3** |
| 80 | Stubs/código morto (`ChestContentHandler`, `TokenView.cs` vazio, `CanvasDebugger` fade) | `Interactables/Chest/Content/ChestContentHandler.cs:5`; `Token/.../TokenView.cs`; `Resources/Debug/Canvas/CanvasDebugger.cs:263` | DRY | baixo | S | **P3** |
| 81 | Buffer estático de overlap partilhado (truncagem silenciosa >256) | `Detection/Core/DetectionScanner.cs:32` | concorrência | baixo | S | **P3** |
| 82 | Nome-de-ficheiro ≠ tipo (`ActivateObjectByInput.cs` → `PanelInputController`) | `Input/ActivateObjectByInput.cs:7` | idiomático | baixo | XS | **P3** |
| 83 | Iniciativa: inimigos mortos ainda rolam dados (assimetria vs. aliados) | `Engine/InitiativeResolver.cs:19-37` | idiomático | baixo | S | **P3** |
| 84 | Various null-guards ausentes em UI subscrevendo em `Awake`/`OnEnable` | `UI/Market/PlayerCurrencyView.cs:15`, `Storageable/View/InventoryItemView.cs:24`, `UI/CharacterSelectionCanvas.cs:49`, `Sound/AudioManager.cs:129` | idiomático | baixo | S | **P3** |

---

## 3. Detalhe dos achados P0 (crítico / quick-win)

### P0-1 · NRE em `PlayerDetectionSystem.OnExit`
- **Arquivo:linha:** `Assets/_Project/Scripts/Player/Detection/PlayerDetectionSystem.cs:149-150`
- **Categoria:** idiomático · **Severidade:** crítico — crash garantido ao sair de um collider cujo `interactable` não resolve.
- **Trecho:**

```csharp
var interactable = ResolveInteractable(col);
if (interactable != null) _available.Remove(interactable);
var characterSelectionNpc = interactable.GetComponent<CharacterSelectionNpc>(); // NRE se null
```

- **Por quê:** A linha 149 admite explicitamente `interactable == null`, mas a linha 150 desreferencia-o na mesma. **Validado no código.** Correção: mover a chamada para dentro do `if` (esforço XS).

### P0-2 · Triggers de área de baú invertidos
- **Arquivo:linha:** `Assets/_Project/Scripts/Interactables/Chest/Pooling/ChestAreaTrigger.cs:78-87`
- **Categoria:** idiomático · **Severidade:** crítico — spawn/return de baús ocorre ao contrário.
- **Trecho:** `OnTriggerEnter → _spawner.OnAreaExited()` (log: "Devolvendo baús à pool") e `OnTriggerExit → _spawner.OnAreaEntered()` (log: "Populando área").
- **Por quê:** **Validado**: as próprias mensagens de log contradizem a ação executada. Trocar as duas chamadas (esforço XS).

### P0-3 · `ApplyRandomDot` aplica a chance ao quadrado
- **Arquivo:linha:** `Assets/../Game.Core/Engine/BattleSimulator.cs:623` e `:721`
- **Categoria:** idiomático · **Severidade:** alto — probabilidade efetiva ≈ `chance²`.
- **Trecho:** o `foreach (var effect in effects)` já filtra `if (_random.NextDouble() > effect.Chance) continue;` (623); depois `case EffectType.ApplyRandomDot` volta a testar `if (_random.NextDouble() > effect.Chance) break;` (721).
- **Por quê:** **Validado no código.** Só `ApplyRandomDot` tem o duplo-gate; `ApplyDot`/`ApplyToken` são testados uma vez. Remover o teste interno (esforço XS).

### P0-4 · `Bleed` usa resistência de `Blight` — *verificar intenção de design*
- **Arquivo:linha:** `Game.Core/Engine/BattleSimulator.cs:896`
- **Categoria:** idiomático · **Severidade:** alto (ajustada de "crítico") — distorce balanceamento, mas **não** é crash.
- **Trecho:** `DotType.Bleed => target.Resistances.BlightRes,`
- **Por quê:** **Validado:** `ResistanceComponent` tem `Burn/Blight/Move/Stun/Deathblow`, **sem `BleedRes`**. Portanto mapear `Bleed → BlightRes` é uma escolha (herdar Blight) ou copy-paste. Requer **decisão de design**: (a) adicionar `BleedRes`, ou (b) `Bleed => 0` (sem resistência), ou (c) confirmar que herdar Blight é intencional. *Rebaixado de crítico porque não há campo em falta — é ambiguidade, não bug de compilação.*

### P0-5 · Combate de inimigo não idempotente
- **Arquivo:linha:** `Assets/_Project/Scripts/Enemy/Enemy/NpcEnemyDetectionHandler.cs:118-125`
- **Categoria:** concorrência · **Severidade:** crítico.
- **Trecho:**

```csharp
GameObject.FindAnyObjectByType<ExplorationLoadContext>().SaveState();
GameObject.FindAnyObjectByType<ExplorationCorruptionSystem>().SaveState();
GameObject.FindAnyObjectByType<PlayerInventorySaveSystem>().SaveAsync();
SceneManager.LoadScene("CombatScene");
```

- **Por quê:** **Validado:** ao contrário de `StaticExplorationEnemyContact` (que tem `_combatTriggered`/`IsCombatTriggerBlocked()`), aqui não há guard — dois inimigos em contacto no mesmo frame causam double-save/double-load; os três `FindAnyObjectByType` sem null-check podem lançar `NullReferenceException`. Alinhar com o padrão idempotente do contacto estático (esforço S).

### P0-6 · `ComboToken.evolution` → `NullReferenceException`
- **Arquivo:linha:** `Assets/_Project/Scripts/Token/Concrete Implementations/Tokens/ComboToken.cs:19,31-33`
- **Categoria:** idiomático · **Severidade:** crítico — `finisherFactory` nunca inicializado no ctor público; ao atingir `evolutionThreshold`, `finisherFactory(...)` crasha.

### P0-7 · `AudioController` sobrescreve 3 de 4 BGM no `Start`
- **Arquivo:linha:** `Assets/_Project/Scripts/Sound/AudioController.cs:7`
- **Categoria:** concorrência · **Severidade:** alto — só a última chamada `PlayBGM` fica audível; comportamento não determinístico com os triggers de cena.

### P0-8 · `ScavengerDetectionSystem.OnExit` chama `base.OnEnter`
- **Arquivo:linha:** `Assets/_Project/Scripts/Exploration/Scavenger/ScavengerDetectionSystem.cs:24`
- **Categoria:** idiomático · **Severidade:** médio — copy-paste; o estado/log de saída da base nunca corre. Correção XS (`base.OnDetectionExit`).

> Os detalhes completos (trecho + justificação) dos achados P1–P3 estão nas tabelas da §2 e nos padrões transversais da §4; cada linha cita `arquivo:linha` exato para consulta direta.

---

## 4. Padrões transversais (deduplicados entre escopos)

Estes temas repetem-se em muitos ficheiros; tratar como **iniciativas** em vez de correções pontuais:

1. **Service-locator / `FindObjectByType`** (achados 5, 9, 24, 59) — dezenas de call-sites (poolados e em `OnEnable`) resolvem dependências por busca global, muitas vezes sem null-check. → Injetar via builder/spawner/binder uma única vez.
2. **Fugas de subscrição de eventos** (17, 22, 35, e vários em UI/áudio) — `+=` em `Awake`/`Bind` sem `-=` em `OnDisable`/`OnDestroy`, agravado por pooling. → Padrão consistente subscribe/unsubscribe simétrico.
3. **DOTween sem `DOKill`/`SetLink`** (21, 71) — **0 ocorrências** em `UI/**`; tweens competem com estado final e ficam órfãos. Viola a regra `dotween-juice` do projeto.
4. **`async void` sem guard de reentrada nem `await`** (13, 25, 26, 27, 69) — saves, spread de tokens e botões async permitem double-execução e escritas parciais.
5. **Strings mágicas** (4, 49, 50) — tags `"Player"`/`"Npc"` (~18×), `EffectScope`, nomes de BGM, paths de textura por convenção. → Constantes/enums centrais.
6. **Duplicação estrutural (DRY)** (32, 46, 47, 53, 54, 55, 57, 72, 79) — raycast pointer (×5), subscribe+catch-up de hub (×4), handlers de botão (×4), pipeline de dano sim/preview, `ResolveDefault*Path` (×4).
7. **God-objects** (28, 29, 63) — `CombatPrototypeController`, `BattleSimulator`, `AudioManager` concentram responsabilidades múltiplas.
8. **Campos públicos mutáveis** (70) — inconsistente com o padrão `[SerializeField] private` do resto do projeto.

---

## 5. Falsos positivos descartados / severidades ajustadas

Como orquestrador, validei os achados críticos contra o código-fonte. Ajustes aplicados:

- **`Bleed → BlightRes`**: **rebaixado de crítico → alto** e marcado "verificar intenção" — não há `BleedRes`, logo é decisão de design (não campo em falta / não crash). (achado 4)
- **`JsonSerializerOptions` estático** e **`HealDebugTrace.OnLog`**: mantidos em **baixo** — seguros hoje em `.NET`/single-thread; só relevantes se surgirem simulações paralelas. (achado 76)
- **`NpcEnemyPool.Return` double-return**: mantido em **baixo** — já loga warning e a race é improvável na main thread; incluído com nota "verificar". (não promovido)
- **Nenhum achado foi descartado como falso positivo puro** — a amostragem validada (`PlayerDetectionSystem:150`, `ChestAreaTrigger`, `ApplyRandomDot`, `NpcEnemyDetectionHandler`, currency slots trocados, ausência de `DOKill`) confirmou-se toda verdadeira, o que dá boa confiança na precisão dos restantes achados não-amostrados.

---

## 6. Cobertura

| Escopo | Ficheiros | Achados brutos |
| --- | --- | --- |
| `Game.Core` + `Game.Simulations` + `Game.Tests` | 38 | 24 |
| `Token/**` | 82 | ~28 |
| Combat/Enemy/Progression/Characters/Playable | ~100 | ~35 |
| Player/Movement/Exploration/Interactables/Currency | ~60 | ~25 |
| UI/Sound/Input/Editor | ~70 | ~40 |

Excluído do escopo: `Assets/TextMesh Pro/**` (terceiros), `Library/`, `obj/`, `bin/`.

---

*Fim da fase de auditoria. Sugestões de arquitetura-alvo e planos de remediação serão produzidos apenas se solicitados numa fase seguinte.*
