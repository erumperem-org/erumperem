# NavMeshService

Serviço stateless de navegação para Unity. Uma única instância opera sobre N agentes via `NavMeshAgentAdapter`, eliminando O(n) MonoBehaviours de serviço, condições de corrida e acoplamento entre lógica de navegação e GameObjects.

---

## Instalação

Copie os arquivos para o seu projeto mantendo o namespace `Services.Navigation`:

```
Assets/
└── Resources/
    └── NavMesh/
        ├── Contract/
        │   └── INavMeshService.cs
        ├── NavMeshAgentAdapter.cs
        ├── NavMeshCoroutineHost.cs
        ├── NavMeshOperation.cs
        └── NavMeshService.cs
```

---

## Setup

### 1. Adicione o adapter ao GameObject do agente

`NavMeshAgentAdapter` requer um `NavMeshAgent` no mesmo GameObject.

```csharp
// Apenas adicione o componente — sem configuração adicional.
// O adapter captura um snapshot das configurações do agente no Awake.
[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour { ... }
```

No Inspector: **Add Component → NavMeshAgentAdapter**.

### 2. Instancie o serviço

O serviço é uma classe C# comum — sem MonoBehaviour, sem ScriptableObject.
Instancie onde fizer sentido para o seu projeto (instalador de DI, GameManager, etc.).

```csharp
INavMeshService navMesh = new NavMeshService();
```

O `NavMeshCoroutineHost` é criado automaticamente na primeira instância do serviço.

---

## Uso básico

### Mover para um destino (síncrono)

```csharp
bool ok = navMesh.MoveTo(adapter, destination);
```

### Mover para um destino (assíncrono)

```csharp
using var op = navMesh.MoveToAsync(adapter, destination, timeout: 15f, cancellationToken);
bool reached = await op.Task;
```

### Seguir um alvo

```csharp
using var op = navMesh.FollowTargetAsync(adapter, target.transform, stopDistance: 2f);
await op.Task; // resolve quando chega à distância de parada ou é cancelado
```

### Parar e retomar

```csharp
navMesh.Stop(adapter);   // para imediatamente
navMesh.Resume(adapter); // retoma a movimentação
```

### Pausar preservando o destino

```csharp
navMesh.PauseNavigation(adapter);  // para, guarda o destino atual
navMesh.ResumeNavigation(adapter); // retoma para o mesmo destino
```

---

## NavMeshOperation — handle de operação

Toda operação assíncrona retorna um `NavMeshOperation`. Cada caller tem o seu próprio handle — cancelar um não afeta os demais.

```csharp
// Task<bool>: true = chegou, false = cancelado/timeout
var op = navMesh.MoveToAsync(adapter, destination);
bool reached = await op.Task;

// Cancelar manualmente
op.Cancel();

// Dispose também cancela
op.Dispose();

// Verificar se ainda está em andamento
bool running = op.IsRunning;
```

Prefira `using` para garantir o cancelamento automático:

```csharp
using var op = navMesh.MoveToAsync(adapter, destination);
bool reached = await op.Task;
```

---

## Exemplos completos com o package UniTask

### Patrulha entre pontos

```csharp
public class PatrolBehaviour : MonoBehaviour
{
    [SerializeField] private NavMeshAgentAdapter _adapter;
    [SerializeField] private Transform[] _waypoints;

    private INavMeshService _navMesh;
    private CancellationTokenSource _cts;

    private void Start()
    {
        _navMesh = ServiceLocator.Get<INavMeshService>();
        _cts = new CancellationTokenSource();
        PatrolLoop(_cts.Token).Forget();
    }

    private async UniTaskVoid PatrolLoop(CancellationToken ct)
    {
        int index = 0;

        while (!ct.IsCancellationRequested)
        {
            var target = _waypoints[index].position;

            using var op = _navMesh.MoveToAsync(_adapter, target, cancellationToken: ct);
            await op.Task;

            index = (index + 1) % _waypoints.Length;
        }
    }

    private void OnDestroy() => _cts.Cancel();
}
```

### Perseguição com distância de parada

```csharp
public class ChaseBehaviour : MonoBehaviour
{
    [SerializeField] private NavMeshAgentAdapter _adapter;

    private INavMeshService _navMesh;
    private NavMeshOperation _chaseOp;

    public void StartChase(Transform target)
    {
        _chaseOp?.Dispose(); // cancela perseguição anterior, se houver
        _chaseOp = _navMesh.FollowTargetAsync(_adapter, target, stopDistance: 1.5f);
    }

    public void StopChase()
    {
        _chaseOp?.Dispose();
        _navMesh.Stop(_adapter);
    }

    private void OnDestroy() => _chaseOp?.Dispose();
}
```

### Verificar alcançabilidade antes de mover

```csharp
if (_navMesh.CanReach(_adapter, destination))
{
    using var op = _navMesh.MoveToAsync(_adapter, destination);
    await op.Task;
}
else
{
    Debug.Log("Destino inalcançável.");
}
```

### Rotacionar para um alvo antes de agir

```csharp
using var faceOp = _navMesh.FaceTargetAsync(_adapter, target, rotationSpeed: 180f);
await faceOp.Task;

// agente está virado para o alvo — dispare, ataque, etc.
```

### Teleporte para o ponto mais próximo da NavMesh

```csharp
// Útil para reposicionar agentes que caíram fora da NavMesh
bool warped = _navMesh.TeleportToNearestNavMeshPoint(_adapter, spawnPosition, maxDistance: 3f);
```

---

## Referência rápida

### Movimentação

| Método | Descrição |
|--------|-----------|
| `MoveTo(adapter, destination)` | Move imediatamente; retorna `bool` |
| `MoveToAsync(adapter, destination, timeout, ct)` | Move; retorna `NavMeshOperation` |
| `FollowTargetAsync(adapter, target, stopDistance, interval, ct)` | Segue alvo continuamente |
| `Stop(adapter)` | Para imediatamente |
| `Resume(adapter)` | Retoma após Stop |
| `PauseNavigation(adapter)` | Pausa preservando destino |
| `ResumeNavigation(adapter)` | Retoma após Pause |

### Posicionamento

| Método | Descrição |
|--------|-----------|
| `Warp(adapter, position)` | Teleporte sem cálculo de caminho |
| `TeleportToNearestNavMeshPoint(adapter, position, maxDistance)` | Teleporte para o ponto NavMesh mais próximo |

### Espera assíncrona

| Método | Descrição |
|--------|-----------|
| `WaitUntilReachedAsync(adapter, timeout, ct)` | Aguarda chegada ao destino |
| `WaitUntilStoppedAsync(adapter, timeout, ct)` | Aguarda parada completa |

### Rotação

| Método | Descrição |
|--------|-----------|
| `FaceTargetAsync(adapter, target, speed, ct)` | Rotaciona em direção a um Transform |
| `FaceDirectionAsync(adapter, direction, speed, ct)` | Rotaciona em direção a um vetor |

### Caminhos e queries

| Método | Descrição |
|--------|-----------|
| `CalculatePath(adapter, destination)` | Calcula caminho sem mover |
| `GetPathLength(path)` | Comprimento total de um NavMeshPath |
| `GetPathCorners(adapter)` | Corners do caminho atual |
| `CanReach(adapter, destination)` | Verifica alcançabilidade |
| `ValidateDestination(destination, radius)` | Verifica se ponto está na NavMesh |
| `SamplePosition(position, out result, maxDistance)` | Ponto mais próximo na NavMesh |
| `Raycast(from, to, out hit)` | `true` se trajeto está livre |

### Estado do agente

| Método | Descrição |
|--------|-----------|
| `IsMoving(adapter)` | Está em movimento |
| `IsStopped(adapter)` | Está completamente parado |
| `HasReachedDestination(adapter)` | Chegou ao destino |
| `IsPending(adapter)` | Cálculo de caminho em andamento |
| `IsOnNavMesh(adapter)` | Está sobre NavMesh válida |
| `GetRemainingDistance(adapter)` | Distância restante até o destino |

### Configuração em runtime

| Método | Descrição |
|--------|-----------|
| `SetSpeed(adapter, speed)` | Velocidade máxima |
| `SetAngularSpeed(adapter, angularSpeed)` | Velocidade de rotação |
| `SetAcceleration(adapter, acceleration)` | Aceleração |
| `SetStoppingDistance(adapter, distance)` | Distância de parada |
| `EnableAutoBraking(adapter, enabled)` | Desaceleração automática |
| `EnableObstacleAvoidance(adapter, enabled)` | Desvio de obstáculos |
| `SetAreaMask(adapter, areaMask)` | Áreas navegáveis |
| `ResetAgent(adapter)` | Restaura configurações do snapshot inicial |

---

## Notas

- **`NavMeshCoroutineHost`** é criado automaticamente e sobrevive entre cenas. Não é necessário adicioná-lo à cena manualmente.
- **`NavMeshAgentAdapter`** captura um snapshot das configurações do agente no `Awake`. `ResetAgent` restaura exatamente esses valores.
- **Múltiplos callers** sobre o mesmo agente são suportados: cada `NavMeshOperation` tem seu próprio `CancellationTokenSource`. Cancelar um handle não interfere nos demais.
- **Descarte de operações**: sempre chame `Dispose()` ou use `using` para evitar coroutines órfãs.
