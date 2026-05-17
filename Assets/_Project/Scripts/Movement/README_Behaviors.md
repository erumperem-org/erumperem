# Sistema de Behaviors de NPC

Sistema de movimentação de NPCs baseado no padrão Strategy. Uma instância do `NavMeshService` opera sobre N agentes via `NavMeshAgentAdapter`. Behaviors são stateless em relação à cena — todo estado por-NPC vive no contexto passado a cada execução.

---

## Estrutura de arquivos

```
Services/Navigation/
├── Contract/
│   └── INavMeshService.cs
├── NavMeshAgentAdapter.cs
├── NavMeshCoroutineHost.cs
├── NavMeshOperation.cs
└── NavMeshService.cs

Core/Exploration/Character/
├── Movement/
│   ├── ICharacterMovementStrategy.cs      ← interfaces e contrato de contexto
│   ├── CharacterMovementContextBase.cs    ← contexto base compartilhado
│   ├── NavMeshUtils.cs                    ← helper interno de NavMesh
│   ├── FreeBehavior.cs
│   ├── GoToPointBehavior.cs
│   ├── WanderBehavior.cs
│   ├── PatrolBehavior.cs
│   ├── PursuingBehavior.cs
│   └── StalkingBehavior.cs
└── NPC/
    ├── NpcMovementController.cs           ← controller de estratégia
    └── Presets/
        ├── FreeNpc.cs
        ├── WanderNpc.cs
        ├── PatrolNpc.cs
        ├── GoToPointNpc.cs
        ├── PursuingNpc.cs
        └── StalkingNpc.cs
```

---

## Setup obrigatório por GameObject

Todo NPC requer três componentes no mesmo GameObject:

| Componente | Papel |
|---|---|
| `NavMeshAgent` | Agente Unity (configure speed, acceleration, etc. no Inspector) |
| `NavMeshAgentAdapter` | Expositor do agente para o serviço; captura snapshot de config no Awake |
| `NpcMovementController` | Gerencia a estratégia ativa e garante trocas atômicas |

O `NpcMovementController` resolve automaticamente o `Adapter` e instancia o `NavMeshService` no `Awake` — nenhuma configuração adicional necessária.

---

## NPCs prontos (Presets)

Adicione o componente Preset ao GameObject junto com os três acima e configure no Inspector.

### `FreeNpc`
NPC parado, sem nenhuma rotina ativa. Consome zero CPU.

```
Campos no Inspector:
  characterName   string   "FreeNpc"
```

### `WanderNpc`
Caminhada aleatória a partir da posição atual. Não percebe alvos.

```
Campos no Inspector:
  characterName   string   "WanderNpc"
  wanderRadius    float    10f   — raio máximo de cada passo
```

### `PatrolNpc`
Patrulha pontos aleatórios em torno de um centro fixo. Não percebe alvos.

```
Campos no Inspector:
  characterName   string      "PatrolNpc"
  patrolCenter    Transform   null (usa posição inicial do NPC se vazio)
  patrolRadius    float       12f
```

### `GoToPointNpc`
Move para um destino fixo e para ao chegar. Ideal para posicionamento inicial de NPCs.

```
Campos no Inspector:
  characterName   string      "GoToPointNpc"
  destination     Transform   — obrigatório; sem destino inicia em FreeBehavior

API em runtime:
  void SetDestination(Vector3 point)   — envia o NPC para um novo ponto
```

### `PursuingNpc`
Persegue um alvo ativamente. Expõe eventos para sistemas externos reagirem.

```
Campos no Inspector:
  characterName     string      "PursuingNpc"
  target            Transform   — obrigatório
  perceptionRadius  float       15f   — raio além do qual o alvo é considerado perdido
  engageDistance    float       1.5f  — distância para considerar o alvo alcançado

Eventos:
  event Action OnTargetReached   — alvo alcançado (dentro de engageDistance)
  event Action OnTargetLost      — alvo saiu do raio de percepção

API em runtime:
  void SetTarget(Transform newTarget)  — define novo alvo e reinicia a perseguição
```

### `StalkingNpc`
Mantém o alvo dentro de uma banda `[minDistance, maxDistance]`. Avança se longe, recua se perto, para se dentro da banda.

```
Campos no Inspector:
  characterName     string      "StalkingNpc"
  target            Transform   — obrigatório
  perceptionRadius  float       20f
  minDistance       float       4f    — abaixo disso o NPC recua
  maxDistance       float       8f    — acima disso o NPC avança

Eventos:
  event Action OnTargetLost   — alvo saiu do raio de percepção
  event Action OnObserving    — disparado a cada tick dentro da banda

API em runtime:
  void SetTarget(Transform newTarget)
```

---

## Princípios do sistema

**Behaviors fazem apenas movimentação.** Detecção de alvo, decisões de troca de comportamento e lógica de jogo são responsabilidade de sistemas externos. Os behaviors comunicam eventos via callbacks no contexto (`OnPointReached`, `OnTargetReached`, `OnTargetLost`, `OnObserving`) — quem assina decide o que fazer.

**Trocas de estratégia são atômicas.** O `NpcMovementController` usa um `SemaphoreSlim(1,1)` internamente. Dois sistemas podem chamar `SetStrategy` simultaneamente sem corrida — o segundo aguarda o primeiro terminar o `UnexecuteBehavior` antes de prosseguir.

**Cada behavior tem seu próprio CancellationTokenSource.** Cancelar um behavior não afeta outros NPCs. `CancelImmediate()` é síncrono e seguro para uso em `OnDestroy`.

---

## Uso avançado: comportamento customizado

Para criar um NPC que reage a eventos e troca de behavior, assine os eventos do Preset e chame `SetStrategy` diretamente no controller:

```csharp
public class EnemyAI : MonoBehaviour
{
    [SerializeField] private PursuingNpc pursuingNpc;
    [SerializeField] private Transform   player;

    private NpcMovementController _controller;

    private void Awake()
    {
        _controller = GetComponent<NpcMovementController>();
    }

    private void Start()
    {
        pursuingNpc.OnTargetReached += HandleTargetReached;
        pursuingNpc.OnTargetLost    += HandleTargetLost;
    }

    private async void HandleTargetReached()
    {
        // Alvo alcançado — qualquer sistema externo decide o próximo passo
        // Ex: iniciar animação de ataque, trocar para FreeBehavior, etc.
        var ctx = new FreeBehaviorContext(
            _controller, _controller.NavMesh, _controller.Adapter,
            transform, "Enemy");

        await _controller.SetStrategy(new FreeBehavior(), ctx);
    }

    private async void HandleTargetLost()
    {
        // Alvo perdido — volta a patrulhar
        var ctx = new PatrolBehaviorContext(
            _controller, _controller.NavMesh, _controller.Adapter,
            transform, player, "Enemy",
            perceptionRadius: 15f,
            patrolCenter: transform.position,
            patrolRadius: 12f);

        await _controller.SetStrategy(new PatrolBehavior(), ctx);
    }

    private void OnDestroy()
    {
        pursuingNpc.OnTargetReached -= HandleTargetReached;
        pursuingNpc.OnTargetLost    -= HandleTargetLost;
    }
}
```

## Uso avançado: behavior totalmente custom

Implemente `IReversibleCharacterMovementStrategy` e crie um contexto que herde `CharacterMovementContextBase`:

```csharp
public sealed class MyBehavior : IReversibleCharacterMovementStrategy
{
    private CancellationTokenSource _cts;

    public async Task ExecuteBehavior(ICharacterMovementStrategyContext context)
    {
        if (context is not MyBehaviorContext ctx) return;

        _cts = new CancellationTokenSource();

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                // sua lógica de movimentação aqui
                // use ctx.NavMesh e ctx.Adapter para operar o agente
                await Task.Delay(100, _cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            ctx.NavMesh.Stop(ctx.Adapter); // sempre pare o agente no finally
        }
    }

    public async Task UnexecuteBehavior(ICharacterMovementStrategyContext context)
    {
        CancelImmediate();
        await Task.CompletedTask;
    }

    public void CancelImmediate()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}

public sealed class MyBehaviorContext : CharacterMovementContextBase
{
    public readonly float MyParam;

    public MyBehaviorContext(
        NpcMovementController controller,
        INavMeshService navMesh,
        NavMeshAgentAdapter adapter,
        Transform self,
        Transform target,
        string characterName,
        float perceptionRadius,
        float myParam)
        : base(controller, navMesh, adapter, self, target, characterName, perceptionRadius)
    {
        MyParam = myParam;
    }
}
```

Para ativar:

```csharp
var ctx = new MyBehaviorContext(_controller, _controller.NavMesh, _controller.Adapter,
    transform, target, "MyNpc", perceptionRadius: 10f, myParam: 42f);

await _controller.SetStrategy(new MyBehavior(), ctx);
```

---

## Checklist de comportamento esperado

| Situação | Comportamento |
|---|---|
| NPC destruído com behavior ativo | `OnDestroy` chama `CancelImmediate()` — sem leak |
| `SetStrategy` chamado duas vezes simultaneamente | Segundo aguarda o primeiro; sem corrida |
| Destino fora da NavMesh em `GoToPointBehavior` | Log de warning + fallback para `FreeBehavior` |
| `PursuingNpc` sem alvo no Start | Log de warning + inicia em `FreeBehavior` |
| `StalkingNpc` sem alvo no Start | Log de warning + inicia em `FreeBehavior` |
| Behavior cancelado externamente | `finally` garante `Stop` no agente em todos os behaviors |
