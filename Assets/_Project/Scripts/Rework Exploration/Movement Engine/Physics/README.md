# Physics Movement Service

Serviço de movimentação por física (Rigidbody) para Unity, desenhado para ser
**plugado em qualquer controlador de input** — teclado, gamepad, IA, replay de
rede, cutscene — sem que o motor de movimentação precise saber quem está no
comando.

---

## 1. Ideia central

Normalmente, um script de "PlayerController" mistura duas responsabilidades:

1. Ler input (teclado, gamepad...)
2. Aplicar física de movimentação (velocidade, aceleração, chão, colisões)

Esse projeto separa as duas em camadas independentes:

```
┌─────────────────────────┐        ┌──────────────────────────────┐
│   Controlador (input)    │  ───▶  │   PhysicsMovementService       │
│  KeyboardMovementCtrl.   │        │   (motor de física)            │
│  SimpleAIMovementCtrl.   │        │                                 │
│  ...o seu próprio         │        │  SetMoveDirection()             │
└─────────────────────────┘        │  SetSprinting()                 │
                                     │  SetValidator()                 │
                                     └──────────────────────────────┘
                                                   │
                                                   ▼
                                        ┌────────────────────┐
                                        │  IMovementValidator  │  (opcional)
                                        │  WallSlideValidator   │
                                        │  HardStopValidator    │
                                        │  ...o seu próprio      │
                                        └────────────────────┘
```

O `PhysicsMovementService` **não conhece** teclado, câmera, IA ou animação.
Ele só entende uma API de comandos. Qualquer script pode chamar essa API —
inclusive vários controladores diferentes trocados em runtime (ex: personagem
controlado por IA que vira jogável ao entrar em um veículo/possessão).

---

## 2. Arquivos

| Arquivo | O que é |
|---|---|
| `MovementSettings.cs` | ScriptableObject com todo o tuning (velocidade, aceleração, rotação, detecção de chão e de obstáculos). Reutilizável entre vários personagens/prefabs. |
| `PhysicsMovementService.cs` | O motor. Componente `MonoBehaviour` que exige um `Rigidbody`. Contém toda a lógica de física. |
| `Validation/IMovementValidator.cs` | Interface da camada opcional de validação de movimento. |
| `Validation/WallSlideValidator.cs` | Implementação: desliza ao longo de obstáculos. |
| `Validation/HardStopValidator.cs` | Implementação: para seco ao encostar em um obstáculo. |
| `Controllers/KeyboardMovementController.cs` | Exemplo de controlador: WASD + Shift, relativo à câmera. |
| `Controllers/SimpleAIMovementController.cs` | Exemplo de controlador: anda até um `Transform` alvo (sem input humano). |

---

## 3. Como o motor funciona por dentro

A cada `FixedUpdate`, o `PhysicsMovementService` executa, nesta ordem:

1. **`CheckGround()`** — faz um `SphereCast` para baixo e atualiza
   `IsGrounded` / `GroundNormal`. Essa informação já fica disponível como
   propriedade pública (útil para animação, câmera, ou para uma futura
   extensão de pulo), mas hoje **não altera** a lógica de aceleração — não há
   diferenciação de aceleração no chão vs. no ar nesta versão.
2. **`ApplyValidation()`** — se houver um `IMovementValidator` ativo, a
   direção desejada (definida por `SetMoveDirection`) é corrigida antes de
   virar velocidade. Se não houver validador, a direção passa direto.
3. **`HandleHorizontalMovement()`** — calcula a velocidade alvo
   (`walkSpeed` ou `sprintSpeed`, escalada pela magnitude da direção — o que
   já dá suporte natural a analógico de gamepad) e interpola a velocidade
   atual até ela via `Vector3.MoveTowards`, usando `acceleration` ao ganhar
   velocidade e `deceleration` ao perder. O eixo Y da velocidade nunca é
   tocado aqui — gravidade e quedas ficam por conta do `Rigidbody` padrão.
4. **`HandleRotation()`** — se `rotateTowardsMovement` estiver ativo, gira o
   Rigidbody suavemente (`Quaternion.Slerp`) na direção validada do
   movimento.

### Por que a interpolação evita "grudar"/"deslizar" estranho

Em vez de setar a velocidade final diretamente (`velocity = direção * speed`),
o serviço sempre caminha da velocidade atual até a alvo a uma taxa fixa
(`acceleration`/`deceleration`, em unidades/s²). Isso evita mudanças de
direção instantâneas e não-físicas, e permite ajustar a "sensação" do
personagem (mais responsivo vs. mais "escorregadio") só mexendo em dois
números no `MovementSettings`.

---

## 4. Camada de validação de movimento (opcional)

### O problema que ela resolve

Mesmo com colisão física normal, um Rigidbody movido por velocidade contínua
(como fazemos aqui) tende a **ficar empurrando indefinidamente** contra um
colisor quando o input aponta para dentro dele — o personagem não atravessa a
parede, mas o serviço recalcula a mesma velocidade "para dentro da parede" a
cada frame. Isso pode gerar jitter, empurrar rigidbodies leves para sempre, ou
prender o personagem em quinas.

### Como resolver

O `IMovementValidator` intercepta a **direção desejada** antes dela virar
velocidade, e a corrige com base em um `SphereCast` à frente do personagem.

```csharp
public interface IMovementValidator
{
    Vector3 Validate(Vector3 desiredDirection, Vector3 origin,
                      float castRadius, float checkDistance, LayerMask obstacleMask);
}
```

Duas implementações já vêm prontas:

- **`WallSlideValidator`** — projeta a direção no plano da normal do
  obstáculo detectado (`Vector3.ProjectOnPlane`). Só remove a componente que
  aponta para dentro do obstáculo; a componente tangencial é preservada, então
  andar em diagonal contra uma parede resulta em deslizar ao longo dela.
- **`HardStopValidator`** — zera completamente a direção quando detecta
  qualquer obstáculo à frente dentro de `obstacleCheckDistance`. Sem
  deslizar: o personagem simplesmente para.

### Como ela é plugada no serviço

```csharp
[SerializeField] private ValidatorPreset validatorPreset = ValidatorPreset.WallSlide;
[SerializeReference] private IMovementValidator validator;
```

Duas formas de configurar, para cobrir tanto uso via Inspector quanto via
código:

1. **Pelo Inspector** — escolha um valor no dropdown `Validator Preset`
   (`None`, `WallSlide`, `HardStop`, `Custom`). No `Awake()`, se nenhum
   validador tiver sido atribuído via código ainda, o serviço instancia
   automaticamente a implementação correspondente ao preset escolhido.
   `Custom` não instancia nada — use para os casos em que você vai injetar
   via código (próximo item).
2. **Via código, em runtime** —
   ```csharp
   movement.SetValidator(new WallSlideValidator());
   // ou
   movement.SetValidator(null); // desativa a validação
   // ou a sua própria implementação:
   movement.SetValidator(new MyCustomValidator());
   ```
   Isso permite, por exemplo, um controlador de IA usar `HardStopValidator`
   (mais previsível para pathfinding) enquanto o jogador usa
   `WallSlideValidator` (mais natural para controle manual) — no mesmo
   serviço, sem duplicar código.

> **Nota de compatibilidade:** `[SerializeReference]` em campos de interface
> ganhou um bom seletor de tipos no Inspector nativo a partir do Unity
> 2021.2+. Em versões mais antigas o campo pode não aparecer editável no
> Inspector — nesse caso, use sempre o preset (`ValidatorPreset`) ou atribua o
> validador via `SetValidator()` no `Awake`/`Start` do seu controlador.

### Criando seu próprio validador

Basta implementar a interface:

```csharp
[System.Serializable]
public class PushBackValidator : IMovementValidator
{
    public Vector3 Validate(Vector3 desiredDirection, Vector3 origin,
                             float castRadius, float checkDistance, LayerMask obstacleMask)
    {
        // sua lógica aqui — ex: empurrar o personagem de volta,
        // reduzir a velocidade gradualmente perto de obstáculos, etc.
        return desiredDirection;
    }
}
```

---

## 5. Como usar

### 5.1 Configuração da cena

1. No personagem, adicione um `Rigidbody`:
   - `Freeze Rotation` não precisa ser marcado manualmente — o serviço já
     seta `rb.freezeRotation = true` no `Awake()` e controla a rotação
     manualmente via `HandleRotation()`.
   - Ajuste `Collision Detection` para `Continuous Dynamic` se o personagem
     se mover rápido, para evitar atravessar colisores finos.
   - Configure `Interpolate` como `Interpolate` se notar tremor visual (o
     movimento acontece no `FixedUpdate`, então interpolação suaviza o
     render entre passos de física).
2. Adicione um `Collider` (Capsule é o mais comum para personagens).
3. Adicione o componente `PhysicsMovementService`.
4. Crie um asset de `MovementSettings`
   (`Assets > Create > Movement > Physics Movement Settings`) e arraste no
   campo `Settings` do serviço. Ajuste os valores conforme o personagem.
5. Escolha um `Validator Preset` (ou `None` se não quiser essa camada).
6. Adicione o controlador de sua escolha (`KeyboardMovementController`,
   `SimpleAIMovementController`, ou o seu próprio) no mesmo GameObject.

### 5.2 Criando o seu próprio controlador

Qualquer script pode operar o serviço — o único requisito é chamar a API
pública dele:

```csharp
public class MeuControlador : MonoBehaviour
{
    [SerializeField] private PhysicsMovementService movement;

    private void Update()
    {
        Vector3 direcaoDesejada = ...; // world-space, magnitude 0–1
        movement.SetMoveDirection(direcaoDesejada);
        movement.SetSprinting(algumaCondicao);
    }
}
```

Importante: `SetMoveDirection` espera a direção **já em world-space**. Se o
seu controlador depende de câmera (terceira pessoa, top-down, isométrico...),
faça essa conversão dentro do próprio controlador — veja
`KeyboardMovementController` como referência. Isso mantém o serviço
totalmente agnóstico de câmera.

### 5.3 API pública do `PhysicsMovementService`

**Comandos:**
| Método | Descrição |
|---|---|
| `SetMoveDirection(Vector3 worldDirection)` | Define a direção de movimento, em world-space. Magnitude 0–1 = intensidade. |
| `SetSprinting(bool value)` | Ativa/desativa sprint. |
| `SetValidator(IMovementValidator validator)` | Troca a estratégia de validação em runtime (`null` desativa). |

**Leitura:**
| Propriedade | Descrição |
|---|---|
| `IsGrounded` | Se o personagem está tocando o chão (via SphereCast). |
| `GroundNormal` | Normal da superfície sob o personagem. |
| `HorizontalVelocity` | Velocidade atual no plano XZ. |
| `CurrentSpeed` | Magnitude de `HorizontalVelocity`. |

---

## 6. Limitações conhecidas do v1 (e por onde estender)

Este escopo foi definido deliberadamente enxuto. Pontos deixados de fora de
propósito, e como encaixá-los depois sem quebrar a API existente:

- **Sem pulo.** `IsGrounded`/`GroundNormal` já existem justamente para
  suportar isso depois (coyote time, jump buffer, controle no ar) sem mexer
  na assinatura pública do serviço — só adicionaria `Jump()` e lógica interna
  nova no `FixedUpdate`.
- **Sem eventos.** Hoje tudo é consultado via propriedades (`IsGrounded`,
  `CurrentSpeed`), não há `event Action OnGroundedChanged` etc. Se quiser
  plugar animação/câmera reagindo a mudanças, dá para adicionar eventos no
  serviço sem afetar os controladores existentes.
- **Sem tratamento especial de rampas/slopes.** O `CheckGround` já retorna a
  normal do chão (`GroundNormal`), mas ela não é usada para projetar a
  velocidade sobre a rampa ainda — hoje o Rigidbody lida com isso via física
  padrão.
- **Validação de obstáculo é só horizontal e "para frente".** O
  `SphereCast` do validador usa a direção de movimento atual como direção do
  cast, a uma altura fixa (`obstacleCastHeight`). Para obstáculos muito baixos
  ou muito altos em relação a essa altura, ajuste o valor no
  `MovementSettings` ou implemente um validador customizado com múltiplos
  casts em alturas diferentes.

---

## 7. Notas de compatibilidade

- O código usa `rb.velocity`, compatível com todas as versões de Unity até o
  momento (inclusive Unity 6, onde a propriedade foi renomeada para
  `linearVelocity` e `velocity` passou a gerar um aviso de obsolescência, mas
  continua funcional). Se estiver no Unity 6+ e quiser eliminar o aviso,
  troque todas as ocorrências de `rb.velocity` por `rb.linearVelocity`.
- Testado conceitualmente para Rigidbody 3D (`UnityEngine.Rigidbody`). Para
  um projeto 2D, seria necessário adaptar para `Rigidbody2D` (trocar `Vector3`
  por `Vector2` nos eixos relevantes e `SphereCast` por `CircleCast`).
