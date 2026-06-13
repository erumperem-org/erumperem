# Sistema de NPC Inimigo — Documentação Técnica (Refatorado)

## O que mudou (SRP)

| Problema original | Solução |
|---|---|
| `NpcEnemy` acumulava máquina de estados, comportamento, detecção, ciclo de vida e transição de cena | Dividido em `NpcEnemyStateMachine`, `NpcEnemyBehaviorRunner`, `NpcEnemyDetectionHandler` e `NpcEnemy` (orquestrador) |
| `NpcEnemy.OnContactWithPlayer()` chamava `ScenesManager.LoadScene("CombatScene")` diretamente | `NpcEnemy` dispara `OnPlayerContact`; `NpcEnemyContactHandler` decide o que fazer via `UnityEvent` |
| `EnemyCollissionTrigger` duplicava a mesma lógica de `LoadScene` | Removida — consolidada no `NpcEnemyContactHandler` |
| `NpcEnemySpawner` continha lógica de filtro e ordenação de spawn points | Extraída para `ISpawnPointSelector` (`PlayerAwareSpawnPointSelector` / `RoundRobinSpawnPointSelector`) |
| `NpcEnemyPool` calculava posições de grid junto à gestão de disponibilidade | Extraída para `IPoolStorage` (`GridPoolStorage`) |
| `NpcEnemyPool.Return(INpcEnemy)` fazia cast `as NpcEnemy` internamente (DIP) | `Return(NpcEnemy)` recebe o tipo concreto; o cast fica no Builder, que é quem conhece o tipo |
| `NpcEnemyConfig` tinha campo `Detector` morto (nunca lido pelo NpcEnemy) | Campo removido |

---

## Nova Arquitetura

```
Systems/NPC/
├── Contracts/
│   ├── INpcEnemy.cs              → Interface pública (+ evento OnPlayerContact)
│   └── NpcEnemyConfig.cs         → Dados imutáveis por ciclo de vida
│
├── Enemy/
│   ├── StateMachine/
│   │   └── NpcEnemyStateMachine.cs   → Transições de estado + eventos OnEnter*
│   ├── NpcEnemyDetectionHandler.cs   → Polling do Detector + OnDetectorEnter/Exit
│   ├── NpcEnemyBehaviorRunner.cs     → Coroutines de Wander/Chase/Monitor
│   ├── NpcEnemy.cs                   → Orquestrador (MonoBehaviour enxuto)
│   ├── NpcEnemyContactHandler.cs     → Reação ao contato com Player (UnityEvent)
│   └── NpcPursuitTarget.cs           → Handler estático global (inalterado)
│
├── Pool/
│   ├── IPoolStorage.cs               → Abstrai posicionamento físico dos NPCs inativos
│   ├── GridPoolStorage.cs            → Implementação em grade
│   └── NpcEnemyPool.cs               → Gerencia disponibilidade (Get/Return/PreWarm)
│
├── Builder/
│   └── NpcEnemyBuilder.cs            → Monta NPC: pool + config + registro no ContactHandler
│
└── Spawner/
    ├── ISpawnPointSelector.cs         → Abstrai seleção de spawn points
    ├── PlayerAwareSpawnPointSelector.cs → Filtra por visão do Player, ordena por distância
    ├── RoundRobinSpawnPointSelector.cs  → Round-robin simples
    └── NpcEnemySpawner.cs             → Controla ciclo de respawn (quando/quantos)
```

---

## Responsabilidades por classe

### `NpcEnemyStateMachine`
Gerencia transições de estado e dispara eventos `OnEnterWander`, `OnEnterChase`, `OnEnterReturnToPool`. Não sabe nada de coroutines, navegação ou detecção.

### `NpcEnemyDetectionHandler`
Gerencia o polling do `Detector` e traduz `OnDetectorEnter/Exit` em chamadas à `StateMachine`. Inclui o tratamento da shape `"Contact"` que notifica `NpcEnemy.NotifyPlayerContact()`.

### `NpcEnemyBehaviorRunner`
Executa as coroutines de comportamento (`WanderCoroutine`, `ChaseCoroutine`, `ChaseRadiusMonitorCoroutine`, `WanderLifetimeCoroutine`) em resposta aos eventos da `StateMachine`. Expõe `OnShouldReturnToPool` para notificar quando o NPC deve ser devolvido.

### `NpcEnemy`
Orquestrador: cria e conecta os três colaboradores acima, implementa o ciclo de vida `INpcEnemy` e expõe `OnPlayerContact` como evento. Não tem nenhum conhecimento de cenas ou sistemas externos.

### `NpcEnemyContactHandler`
MonoBehaviour que escuta `OnPlayerContact` de cada NPC e executa um `UnityEvent` configurável no Inspector. Substitui o `ScenesManager.LoadScene(...)` hardcoded e o `EnemyCollissionTrigger` duplicado.

### `ISpawnPointSelector` / implementações
Abstrai a seleção do próximo spawn point. O `NpcEnemySpawner` constrói o selector correto no `Awake` baseado na presença de `_playerTransform`.

### `IPoolStorage` / `GridPoolStorage`
Abstrai o posicionamento físico dos NPCs inativos. A `NpcEnemyPool` delega totalmente para o storage, sem saber do layout.

---

## Setup no Unity

### Hierarquia sugerida (inalterada)

```
[NpcEnemySystem]
├── NpcEnemyPool
├── NpcEnemyBuilder
├── NpcEnemyContactHandler    ← NOVO: configure o UnityEvent no Inspector
├── NpcEnemySpawner
└── SpawnPositionService
```

### NpcEnemyContactHandler

| Campo | Valor sugerido |
|---|---|
| On Contact (UnityEvent) | `ScenesManager.LoadSceneByName("CombatScene")` |

O `NpcEnemyBuilder` registra/desregistra cada NPC automaticamente.

### EnemyCollissionTrigger

**Remover dos prefabs.** A lógica foi consolidada no `NpcEnemyContactHandler`.

---

## Fluxo de contato com o Player (novo)

```
[DetectionPollingCoroutine]
  └─ detector.Scan()
       └─ OnDetectorEnter(playerCollider, "Contact", ...)
            └─ NpcEnemyDetectionHandler → npcEnemy.NotifyPlayerContact()
                 └─ NpcEnemy.OnPlayerContact?.Invoke(this)
                      └─ NpcEnemyContactHandler.HandleContact()
                           └─ _onContact.Invoke()  ← UnityEvent configurado
                                └─ ScenesManager.LoadSceneByName("CombatScene")
```

---

## Extensibilidade

### Adicionar novo estado (ex: Attack)
1. Adicionar `Attack` ao enum `NpcEnemyState`
2. Adicionar `event Action OnEnterAttack` na `NpcEnemyStateMachine` + método `ToAttack()`
3. Criar `RunAttack()` no `NpcEnemyBehaviorRunner`
4. Conectar em `NpcEnemy.Initialize()`: `_stateMachine.OnEnterAttack += _behaviorRunner.RunAttack`

### Adicionar nova estratégia de spawn points
Implementar `ISpawnPointSelector` e ajustar `NpcEnemySpawner.BuildSelector()`.

### Adicionar novo layout de storage da pool
Implementar `IPoolStorage` e injetar em `NpcEnemyPool`.
