# PlayableCharacters

Sistema de múltiplos personagens jogáveis com 3 papéis (Em Jogo,
Companheiro, Resting). Sempre existe exatamente um personagem Em Jogo e um
Companheiro; todo o resto do roster fica em Resting - essa invariante é
garantida pelas próprias operações de troca, nunca por escolha direta do
player sobre "quem vai para Resting".

---

## 1. Dependências

| Dependência | De onde vem | Para quê |
|---|---|---|
| `PhysicsMovementService`, `MovementSettings` | Projeto base de movimentação por física | Motor de movimento de todo personagem |
| `ObstacleAvoidanceValidator` | Pacote `ChaserAI` | Desvio de obstáculo do Companheiro ao seguir |
| `CharacterSaveData`, `CharacterSaveRecord`, `CharacterPersistenceService` | Pacote `CharacterPersistence` | Leitura/escrita do save |
| New Input System (`UnityEngine.InputSystem`) | Pacote do Unity | Input do papel Em Jogo |

---

## 2. IMPORTANTE: origem do `characterId`

Hoje `characterId` é um campo de texto livre, preenchido manualmente no
Inspector de cada `PlayableCharacter`. **Isso é um placeholder.** Quando
existir uma fonte externa de identidade de personagens (banco de dados,
serviço de conta, etc.), esse campo deve passar a ser preenchido/validado a
partir dela, e a comparação de igualdade de strings usada hoje (`==`) deve
ser revisada — não há qualquer garantia de unicidade além da disciplina do
usuário arrastando os campos no Inspector.

---

## 3. Papéis e invariante

| Papel | Componente ativo | Tag |
|---|---|---|
| Em Jogo | `PlayerInputMovementController` | `Player` |
| Companheiro | `CompanionFollowController` | `NPC` |
| Resting | `RestingMovementController` | `NPC` |

Apenas um dos três componentes fica `enabled = true` por vez, controlado
exclusivamente por `PlayableCharacter.SetState` — e `SetState` só é chamado
pelo `PlayableCharacterController`, nunca diretamente por outro sistema.

---

## 4. Operações de troca

O player nunca escolhe "quem vai para Resting" diretamente — só dispara uma
das três operações, cada uma preservando a invariante sozinha:

| Operação | Efeito |
|---|---|
| `PromoteToCompanionOperation(id)` | Personagem `id` (Resting) → Companheiro. Companheiro anterior → Resting. Em Jogo não muda. |
| `PromoteToInGameOperation(id)` | Personagem `id` (Resting) → Em Jogo. Em Jogo anterior → Resting. Companheiro continua o mesmo, mas passa a seguir o novo Em Jogo. |
| `SwapInGameAndCompanionOperation()` | Troca direta entre os dois papéis ativos. Ninguém vai para Resting. |

```csharp
// Exemplo de disparo, a partir de qualquer sistema (UI, diálogo, etc.):
playableCharacterController.ExecuteOperation(new PromoteToInGameOperation("hero-02"));
```

Adicionar uma nova operação no futuro = uma nova classe implementando
`ICharacterSwitchOperation`, sem tocar em `PlayableCharacterController`.

---

## 5. Eventos de papel

`PlayableCharacterController` expõe três eventos, disparados **no momento
da decisão** (não da chegada física):

```csharp
OnCharacterEnteredInGame    // Action<PlayableCharacter>
OnCharacterEnteredCompanion // Action<PlayableCharacter>
OnCharacterEnteredResting   // Action<PlayableCharacter>
```

Esses eventos são o ponto de integração futuro com outros sistemas — por
exemplo, um serviço que atualiza o `target` dos `ChaserAI`/`Hub` sempre que
o personagem Em Jogo muda. Essa integração não está implementada aqui, por
enquanto.

Além disso, cada `PlayableCharacter` individualmente expõe
`OnArrivedAtRestingPoint` — disparado só quando o personagem chega
fisicamente ao ponto de descanso (uso local, ex: sistemas de interação no
mesmo personagem).

---

## 6. Companheiro: comportamento de seguir

Sem raio de percepção — o alvo nunca é "perdido". Dois comportamentos:

- **Parada**: distância ≤ `stopDistance` → para.
- **Correr para alcançar**: histerese entre duas distâncias para evitar
  ficar entrando/saindo de sprint na borda de um único limiar.
  - Distância ≥ `catchUpTriggerDistance` → liga sprint.
  - Só desliga quando a distância cai a ≤ `catchUpRecoverDistance`
    (deve ser menor que a de trigger).

O desvio de obstáculo usa o mesmo `ObstacleAvoidanceValidator` do pacote
`ChaserAI`.

---

## 7. Resting

Um ponto de descanso próprio por personagem (`Transform` arrastado no
Inspector de cada `PlayableCharacter`). Ao entrar em Resting, o personagem
caminha até o ponto (igual ao Investigating do `ChaserAI`); ao chegar,
desliga a própria rotina de movimentação e dispara
`OnArrivedAtRestingPoint`.

---

## 8. Save / Load

- **Load**: automático, disparado em `PlayableCharacterController.Start()`
  via `LoadAndApplyCharactersAsync()` (`async void` intencional — é um
  entry point de ciclo de vida, não algo aguardado por outro código; um
  wrapper futuro de save-game pode chamar esse mesmo método).
- **Sem arquivo de save**: nenhuma posição/rotação é tocada — cada
  personagem permanece onde já está posicionado na cena. Os papéis vêm de
  `initialInGameCharacterId`/`initialCompanionCharacterId`, configurados no
  Inspector do `PlayableCharacterController`.
- **Com arquivo de save**: `InGameCharacterId`/`CompanionCharacterId` do
  arquivo são a fonte da verdade dos papéis (ver README do
  `CharacterPersistence`); cada personagem é teleportado para a posição/
  rotação salva antes de receber seu papel.
- **Save**: `PlayableCharacterController.SaveAsync()` é uma API pública,
  **sem gatilho automático definido ainda**. Chame de onde fizer sentido no
  seu fluxo de jogo (checkpoint, saída de área segura, encerramento de
  sessão, etc.) — isso ainda precisa ser decidido e amarrado.

---

## 9. Como usar

1. Crie um `PlayableCharacterSettings` (`Assets > Create > Movement >
   Playable Character Settings`) e ajuste os valores.
2. Para cada personagem: `Rigidbody` + `Collider` + `PhysicsMovementService`
   (com `MovementSettings`) + os três controladores
   (`PlayerInputMovementController`, `CompanionFollowController`,
   `RestingMovementController`) + `PlayableCharacter` no topo, com
   `characterId`, `settings`, `restingPoint` e os três controladores
   arrastados.
3. Configure `moveAction`/`sprintAction` (`InputActionReference`) e
   `cameraTransform` no `PlayerInputMovementController` de cada personagem.
4. Crie um GameObject único com `PlayableCharacterController`, arraste
   todos os `PlayableCharacter` da cena no roster, e defina
   `initialInGameCharacterId`/`initialCompanionCharacterId` (usados só na
   ausência de save).
5. Rode a cena. Use o Inspector do `PlayableCharacterController` (em Play
   Mode) para testar as três operações com um id de teste, e forçar
   Save/Load manualmente.

---

## 10. Notas de compatibilidade

- `CompanionFollowController.Initialize()` chama `movement.SetValidator(...)`,
  sobrescrevendo qualquer `ValidatorPreset` escolhido no Inspector do
  `PhysicsMovementService` daquele personagem — mesmo caveat já documentado
  no `ChaserAI`.
- Os três controladores de comportamento buscam o `PhysicsMovementService`
  via `GetComponent` dentro de `Initialize()`/uso, não no próprio `Awake()`,
  porque o Unity não garante ordem entre `Awake()` de componentes
  diferentes no mesmo GameObject, e `PlayableCharacter.Awake()` chama
  `Initialize()` desses controladores.
