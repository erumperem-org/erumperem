# Core.Exploration.Enemy — Sistema de Inimigos de Exploração

Sistema de inimigos baseado em **Strategy Pattern** com pool de objetos (Unity `ObjectPool<T>`),  
navegação por **NavMesh** e comportamentos assíncronos canceláveis via `CancellationToken`.

---

## Estrutura de pastas

```
Enemies/
├── Debug/
│   └── CircleGizmos.cs              # Gizmo de anel para visualizar radii no editor
├── Enumerators/
│   └── ExplorationEnemyLevels.cs    # Enum de nível do inimigo (Low / Mid / High)
├── Resources/
│   └── Builder/
│       ├── ExplorationEnemyBuilder.cs   # Fábrica: instancia e destrói GameObjects
│       └── ExplorationEnemyPooling.cs   # Gerenciador da pool (MonoBehaviour)
├── Strategy/
│   ├── Contract/
│   │   └── IEnemyStartegy.cs        # Interfaces + todos os Context classes
│   ├── OnPoolBehavior.cs            # Comportamento de inativo (poolado)
│   ├── PatrolBehavior.cs            # Patrulha aleatória → detecta alvo → persegue
│   ├── PursuingBehavior.cs          # Perseguição ativa → alcança → carrega combate
│   └── StalkingBehavior.cs          # Mantém distância fixa do alvo
├── ExplorationEnemyController.cs    # MonoBehaviour central; troca de estratégia thread-safe
└── ExplorationEnemyData.cs          # Dados serializáveis do inimigo
```

---

## Como aplicar em uma cena

### 1. Configurar o NavMesh

- Configure o **NavMesh** da sua cena (`Window → AI → Navigation`).
- Garanta que a área percorrível esteja marcada como **Walkable**.

---

### 2. Criar o prefab do inimigo

1. Crie um GameObject (ex: cápsula ou model 3D).
2. Adicione o componente **`ExplorationEnemyController`**  
   *(o `[RequireComponent]` garante que o `NavMeshAgent` seja adicionado automaticamente)*.
3. Configure o `NavMeshAgent` conforme desejado (velocidade, `stoppingDistance`, etc.).
4. **Salve como Prefab**.

> O `CircleGizmo` é opcional: adicione ao prefab para visualizar os radii de percepção e patrulha no editor.

---

### 3. Configurar o gerenciador de pool na cena

1. Crie um GameObject vazio (ex: `EnemyManager`).
2. Adicione o componente **`ExplorationEnemyPooling`**.
3. No Inspector, preencha:

| Campo | Descrição |
|---|---|
| **Builder → Enemy Prefab** | Prefab criado no passo 2 |
| **Builder → Default Perception Radius** | Raio de detecção do alvo (padrão: `10`) |
| **Builder → Default Patrol Radius** | Raio de patrulha aleatória (padrão: `50`) |
| **Builder → NavMesh Sample Distance** | Distância máxima de busca no NavMesh ao validar spawn (padrão: `10`) |
| **Pooled Objects Parent** | Transform pai dos inimigos inativos |
| **Active Objects Parent** | Transform pai dos inimigos ativos |
| **Player** | Transform do jogador (alvo dos inimigos) |
| **Pool Default Capacity** | Capacidade inicial pré-alocada (padrão: `10`) |
| **Pool Max Size** | Máximo de instâncias simultâneas (padrão: `20`) |
| **Pool Position** | Posição para onde inativos são movidos |
| **Spawn Center** | Centro de referência para pontos de spawn aleatórios |
| **Spawn Nav Mesh Radius** | Raio de busca de pontos de spawn no NavMesh (padrão: `50`) |

4. Crie dois GameObjects filhos:
   - `PooledEnemies` → arraste para **Pooled Objects Parent**
   - `ActiveEnemies` → arraste para **Active Objects Parent**

---

### 4. Spawnar inimigos via código

```csharp
using Core.Exploration.Enemy;
using UnityEngine;

public class MyGameManager : MonoBehaviour
{
    [SerializeField] private ExplorationEnemyPooling _enemyPool;

    void Start()
    {
        // Spawna um inimigo de nível Mid
        ExplorationEnemyController enemy = _enemyPool.GetEnemy(ExplorationEnemyLevels.Mid);
    }

    void ReleaseEnemy(ExplorationEnemyController enemy)
    {
        // Devolve o inimigo para a pool (reutilizável)
        _enemyPool.ReleaseEnemy(enemy);
    }
}
```

---

### 5. Aplicar comportamentos manualmente

Os comportamentos podem ser trocados em qualquer momento via `ExplorationEnemyController.SetEnemyStartegy`:

```csharp
using Core.Exploration.Enemy;

// Patrulha aleatória
await ExplorationEnemyController.SetEnemyStartegy(
    enemy,
    new PatrolBehavior(),
    new PatrolBehaviorContext(enemy, playerTransform, perceptionRadius)
);

// Perseguição ativa
await ExplorationEnemyController.SetEnemyStartegy(
    enemy,
    new PursuingBehavior(),
    new PursuingBehaviorContext(enemy, playerTransform, perceptionRadius)
);

// Stalking (mantém distância)
await ExplorationEnemyController.SetEnemyStartegy(
    enemy,
    new StalkingBehavior(),
    new StalkingBehaviorContext(enemy, playerTransform, stalkingDistance: 8f)
);
```

---

### 6. Criar um comportamento customizado

Implemente `IReverseableEnemyStartegy` e defina seu próprio `Context`:

```csharp
using Core.Exploration.Enemy;
using System.Threading;
using System.Threading.Tasks;

// 1. Contexto com os dados que seu comportamento precisa
public class MyCustomContext : IEnemyStartegyContext
{
    public ExplorationEnemyController Enemy;
    // ... outros dados
}

// 2. Comportamento
public class MyCustomBehavior : IReverseableEnemyStartegy
{
    private CancellationTokenSource _cts;

    public async Task ExecuteBehavior(IEnemyStartegyContext context)
    {
        if (context is MyCustomContext ctx)
        {
            _cts = new CancellationTokenSource();
            // sua lógica assíncrona aqui
        }
    }

    public Task UnexecuteBehavior(IEnemyStartegyContext context)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return Task.CompletedTask;
    }

    public void CancelImmediate()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
```

---

### 7. Scripts de teste (opcional)

Adicione qualquer script da pasta `Tests/Scripts/` a um GameObject na cena e chame o método público via **UnityEvent** ou diretamente no Inspector em Play Mode:

| Script | Método | O que faz |
|---|---|---|
| `PoolSpawnTest` | `SpawnTest()` | Spawna 1 inimigo |
| `PoolReleaseTest` | `ReleaseTest()` | Libera 1 inimigo específico |
| `PoolMassiveSpawnTest` | `MassiveSpawnTest()` | Spawna N inimigos de uma vez |
| `PoolSpawnAndReleaseTest` | `SpawnAndReleaseTestFunction()` | Spawna e libera após delay |
| `PoolStressTest` | `StressTest()` | Spawna N e libera todos após delay |

---

## Fluxo de estados dos comportamentos

```
[Pool / Inativo]
      │
      │ GetEnemy()
      ▼
[PatrolBehavior] ──── detecta alvo ────► [PursuingBehavior]
      ▲                                        │
      └────── alvo sai do raio ───────────────┘
                                               │
                               alcança alvo → LoadScene("CombatScene")

[StalkingBehavior] — comportamento independente, ativado manualmente
```

---

## Dependências externas

- **Unity** 2021.3+ (recomendado 2022+)
- **Unity AI Navigation** (NavMesh)
- **UnityEngine.Pool** (disponível desde Unity 2021.1)
- `Services.DebugUtilities` — serviço de log interno do projeto (`LoggerService`)
