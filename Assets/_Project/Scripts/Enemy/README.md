# Sistema de NPC Inimigo — Documentação Técnica

## Visão Geral

Sistema completo de NPC inimigo para Unity, baseado em **Coroutines controláveis**,
aproveitando 100% dos sistemas já existentes do pack:

| Sistema do Pack         | Como é usado                                      |
|-------------------------|---------------------------------------------------|
| `NavMeshService`        | Movimentação (wander + chase) via `INavMeshService` |
| `NavMeshAgentAdapter`   | Adapter do agente por NPC                         |
| `NpcMovementController` | Troca atômica de strategies (Wander / Pursuing)   |
| `WanderBehavior`        | Caminhada aleatória via NavMesh                   |
| `PursuingBehavior`      | Perseguição contínua do Player                    |
| `Detector`              | Detecção de colisores com tag "Player"            |
| `NavMeshSpawnPositionService` | Spawn em posições válidas no NavMesh        |

---

## Arquitetura — Classes e Responsabilidades

```
Systems/NPC/
├── Contracts/
│   ├── INpcEnemy.cs          → Interface pública do NPC (pool/builder só conhecem esta)
│   └── NpcEnemyConfig.cs     → Dados imutáveis por ciclo de vida (spawn, radii, callbacks)
│
├── Enemy/
│   ├── NpcEnemy.cs           → MonoBehaviour principal — máquina de estados via Coroutines
│   └── NpcPursuitTarget.cs   → Handler estático global do alvo de perseguição
│
├── Pool/
│   └── NpcEnemyPool.cs       → Pool de objetos (max 10, grade de armazenamento, sem Instantiate/Destroy)
│
├── Builder/
│   └── NpcEnemyBuilder.cs    → Factory — conecta pool + config + NPC
│
└── Spawner/
    └── NpcEnemySpawner.cs    → Dispara spawns em intervalo via Coroutine
```

---

## Estados e Coroutines

```
Activate()
    │
    ├─▶ [DetectionPollingCoroutine]  → roda sempre, faz Scan() no Detector a cada 0.15s
    │
    └─▶ [WanderCoroutine]           → estado WANDER
            │  (Player detectado → OnDetectorEnter)
            ▼
        [ChaseCoroutine]            → estado CHASE  (perseguição via PursuingBehavior)
        [ChaseRadiusMonitorCoroutine] ← paralelo → checa distância do SpawnPoint
            │  (ultrapassa ChaseRadius)
            ▼
        ReturnToPool()
            │  StopAllBehaviorCoroutines()
            │  Reset NavMeshAgent
            │  ClearTarget
            │  SetActive(false)
            ▼
        [Pool — disponível para reuso]
```

**Regra de Coroutines:**
- Cada estado tem **1 coroutine** (+ 1 paralela para monitoramento em Chase).
- Ao trocar de estado: coroutine anterior é `StopCoroutine`d imediatamente.
- `ReturnToPool` → `StopAllBehaviorCoroutines()` → zero processamento residual.

---

## Setup no Unity — Passo a Passo

### 1. Prefab do NPC Inimigo

Componentes **obrigatórios** no prefab:
- `NpcEnemy` (script gerado)
- `NpcMovementController` (do pack)
- `NavMeshAgentAdapter` (do pack)
- `NavMeshAgent` (Unity built-in)
- `Detector` (do pack) — configure a shape de detecção no Inspector
- `DetectionComponent` (do pack, requerido pelo Detector)
- `Collider` — para detecção por overlap

> **Importante:** O `Detector` usa `Detector.Scan()` manualmente.
> NÃO adicione `TickingDetector` — o `NpcEnemy` gerencia o polling via Coroutine.

### 2. Cena — Hierarquia sugerida

```
[NpcEnemySystem]
├── NpcEnemyPool          ← NpcEnemyPool.cs
│   ├── NpcEnemy_00       ← gerado automaticamente no Awake
│   ├── NpcEnemy_01
│   └── ...
│
├── NpcEnemyBuilder       ← NpcEnemyBuilder.cs
│
├── NpcEnemySpawner       ← NpcEnemySpawner.cs
│
└── SpawnPositionService  ← NavMeshSpawnPositionServiceMono (do pack)
```

### 3. Configuração dos Componentes

#### NpcEnemyPool
| Campo            | Valor sugerido                    |
|------------------|-----------------------------------|
| Npc Prefab       | Prefab do NPC (com todos os comps)|
| Pool Size        | 10                                |
| Storage Origin   | (0, -100, 0) — fora do mapa       |
| Storage Spacing  | 3                                 |

#### NpcEnemyBuilder
| Campo             | Valor sugerido |
|-------------------|----------------|
| Pool              | NpcEnemyPool   |
| Spawn Service     | NavMeshSpawnPositionServiceMono |
| Wander Radius     | 8              |
| Chase Radius      | 20             |
| Contact Distance  | 1.2            |

#### NpcEnemySpawner
| Campo          | Valor sugerido |
|----------------|----------------|
| Builder        | NpcEnemyBuilder|
| Pool           | NpcEnemyPool   |
| Spawn Interval | 5 (segundos)   |
| Batch Size     | 1              |
| Auto Start     | ✓              |
| Spawn Points   | (opcional)     |

---

## Fluxo de Execução Completo

```
[Awake — NpcEnemyPool]
  └─ Instancia 10 NPCs → SetActive(false) → posiciona na grade

[Start — NpcEnemySpawner]
  └─ StartSpawning() → inicia SpawnLoopCoroutine

[SpawnLoopCoroutine]  ←── Coroutine
  └─ WaitForSeconds(5s)
  └─ Builder.Build()
       ├─ Pool.Get()          → SetActive(true), retira da stack
       ├─ SpawnService.TryGetPosition() → ponto válido no NavMesh
       ├─ new NpcEnemyConfig(spawnPoint, wanderRadius, chaseRadius, ...)
       ├─ npc.Initialize(config)
       │    └─ Registra OnDetectorEnter/Exit
       └─ npc.Activate()
            ├─ transform.position = spawnPoint
            ├─ NavMesh.ResetAgent()
            ├─ StartDetectionPolling() → DetectionPollingCoroutine
            └─ EnterWander()
                 └─ WanderCoroutine → SetStrategy(WanderBehavior)
                                          └─ Caminha aleatoriamente no NavMesh

[DetectionPollingCoroutine]  ←── Coroutine paralela
  └─ WaitForSeconds(0.15s)
  └─ detector.Scan()
       └─ [Player encontrado] → OnDetectorEnter(playerCollider)
            └─ EnterChase(playerTransform)
                 ├─ StopCoroutine(WanderCoroutine)
                 ├─ ChaseCoroutine → SetStrategy(PursuingBehavior)
                 └─ ChaseRadiusMonitorCoroutine (paralela)
                      └─ WaitForSeconds(0.2s)
                      └─ distFromSpawn > chaseRadius → ReturnToPool()

[ReturnToPool()]
  ├─ StopAllBehaviorCoroutines()
  ├─ Desregistra eventos do Detector
  ├─ NavMesh.Stop() + ResetAgent()
  ├─ config.OnReturnToPool(this) → Pool.Return(npc)
  │    ├─ RepositionInStorage()
  │    └─ SetActive(false)
  └─ [NPC disponível na stack para reuso]
```

---

## Ciclo Completo (spawn → wander → chase → pool → respawn)

O sistema suporta múltiplos ciclos sem degradação:

1. **spawn** — NPC sai da pool, posicionado no NavMesh
2. **wander** — Caminha aleatoriamente, Detector polling ativo
3. **chase** — Player detectado, PursuingBehavior ativo
4. **return to pool** — Limite ultrapassado ou Player saiu, todas as coroutines encerradas
5. **respawn** — SpawnLoopCoroutine detecta slot disponível → repete do passo 1

---

## Extensibilidade

### Adicionar novo estado (ex: Attack)
1. Adicionar `Attack` ao enum `NpcEnemyState`
2. Criar `AttackCoroutine()` em `NpcEnemy`
3. Criar `EnterAttack()` que chama `StopStateBehaviorCoroutine()` + inicia nova coroutine
4. Chamar `EnterAttack()` de `OnContactWithPlayer()`

### Adicionar novo comportamento de movimento
1. Criar `XyzBehavior : IReversibleCharacterMovementStrategy` (padrão do pack)
2. Criar `XyzBehaviorContext : CharacterMovementContextBase`
3. Chamar `_movementController.SetStrategy(new XyzBehavior(), context)` na coroutine do estado

### Limitar pool por tipo de NPC
- Criar subclasses de `NpcEnemyPool` ou adicionar categoria ao `NpcEnemyConfig`
- O Builder escolhe a pool correta baseado no tipo solicitado

---

## Notas de Performance

| Preocupação              | Solução adotada                              |
|--------------------------|----------------------------------------------|
| Update/FixedUpdate       | ❌ Nenhum — 100% Coroutines                  |
| Instantiate/Destroy      | ❌ Apenas no Awake — pool reutiliza          |
| Coroutines órfãs         | ✅ ReturnToPool() encerra todas              |
| Loops residuais          | ✅ StopCoroutine explícito em cada transição |
| Allocations em poll      | ✅ WaitForSeconds cacheado por coroutine     |
| NavMesh estado sujo      | ✅ ResetAgent() a cada ciclo                 |
