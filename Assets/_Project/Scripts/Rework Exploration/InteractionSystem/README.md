# InteractionSystem

## Estrutura
- `Runtime/Core` — contrato (`IInteractable`), contexto (`IInteractionContext`,
  `InteractionContext`), base (`InteractableBase`), sensores
  (`InteractionSensor`, `ProximityReactivationSensor`), abstração de
  instigador (`IInstigatorProvider`) e o utilitário `RequireInterfaceAttribute`.
- `Runtime/Concrete` — `ButtonInteractable`, `ChestInteractable`,
  `NpcInteractable`. Sem interfaces de sistemas externos: toda integração é
  via `UnityEvent`, configurada no Inspector.
- `Editor` — inspectors customizados (`InteractableBaseEditor`,
  `InteractionSensorEditor`) e o `PropertyDrawer` do `RequireInterface`.
- `Bridges` — adapter específico do projeto (`PlayableCharacterInstigatorProvider`),
  que traduz `PlayableCharacterController.OnCharacterEnteredInGame` para
  `IInstigatorProvider`. Fica fora de `Runtime`/`Editor` de propósito, porque
  depende do assembly do projeto (`Core.CharacterStats`).

Sem `.asmdef` — basta jogar as pastas dentro de `Assets/`.

## Como plugar um sistema externo (painel, diálogo, etc.)
`InteractableBase` expõe três `UnityEvent` no Inspector:
- `On Interaction Started` — disparado quando a interação é executada.
- `On Became Available` / `On Became Unavailable` — espelham o `AvailabilityChanged`.

Arraste qualquer componente no campo do evento e escolha qualquer método
público. Se o método tiver parâmetro (ex: `OpenPanel(string panelId)`), o
próprio Inspector deixa preencher o valor ali — nenhuma interface nova em
C# é necessária.

## Reativação
- **Botão**: reativa sozinho (`reactivateDelay`, 0 = imediato).
- **Baú**: não reativa sozinho. Adicione um `ProximityReactivationSensor` no
  mesmo GameObject, apontando `Interactable Source` para o próprio baú.
- **NPC**: não reativa sozinho. Se o seu sistema de diálogo tiver um evento
  de "fim de diálogo", arraste-o para chamar `SetAvailable(true)` no NPC
  (parâmetro fixo `true` no Inspector).

## Instigador
`InteractionSensor` e `ProximityReactivationSensor` dependem de
`IInstigatorProvider`, não de um GameObject fixo. Use
`Bridges/PlayableCharacterInstigatorProvider` para conectar ao seu
`PlayableCharacterController`, ou escreva seu próprio adapter se a fonte do
instigador mudar no futuro.

Campos marcados com `[RequireInterface]` (ex: `Instigator Provider Source`)
validam no Inspector se o objeto arrastado implementa a interface esperada,
e tentam resolver automaticamente o componente certo se você arrastar o
GameObject errado.
