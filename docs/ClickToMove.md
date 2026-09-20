# Movimento por clique na exploração

`PlayableCharactersManager` adiciona `ExplorationPointerController` no próprio objeto ao iniciar. Um componente configurado manualmente é preservado.

- Clique esquerdo no chão: calcula um caminho completo no NavMesh do agente atual.
- Mouse sobre um interagível disponível: ativa o mesmo outline de interação, inclusive antes de chegar ao alcance. O destaque por proximidade continua disponível.
- Clique esquerdo no alvo: aproxima-se e chama `PlayerDetectionSystem.TryInteract` para aquele alvo ao entrar na detecção existente.
- Teclado de movimento, Space, botão direito, pausa, bloqueio de input, perda de foco e troca do Main cancelam o comando.
- Clique na interface não comanda movimento. Alvos indisponíveis ou destruídos não são usados. Caminhos inválidos/parciais são rejeitados; ficar preso cancela após o timeout.

O movimento por clique usa o NavMeshAgent existente e Rigidbody cinemático. Ao cancelar ou chegar, retorna ao movimento por teclado. O agente segue seus custos de área e tipo de agente. O controlador expõe máscaras de raycast, distâncias de amostragem, intervalo de atualização do alvo e timeout no Inspector.

## Verificação em Play na Overworld

1. Clique em dois pontos do chão consecutivamente: o último substitui o primeiro, com animação e contorno dos obstáculos do NavMesh.
2. Durante a caminhada, mova pelo teclado e depois use botão direito: ambos devem cancelar, sem deslocamento residual.
3. Passe o mouse sobre um baú fechado distante: destaque suave. Clique: só deve abrir ao entrar no range e apenas uma vez; depois o outline some.
4. Clique num NPC distante, com outro NPC mais perto: interaja apenas com o clicado.
5. Na vila, teste Buck e Wulfric alternadamente como Main: o personagem controlado não é alvo; o outro mostra outline e abre o painel. Fora da vila, respeite a indisponibilidade existente.
6. Clique sobre botões de UI, pause durante uma caminhada e troque o Main: nenhum comando antigo deve continuar ou disparar uma interação atrasada.
7. Clique num ponto desconectado do NavMesh, esconda/destrua o alvo durante a aproximação e bloqueie o caminho: não deve haver interação remota nem espera indefinida.

Validação realizada: compilação C# incluindo o controlador novo; bloqueada pelo erro CS0121 preexistente de `AsSpan` em `CombatSceneUnitVisualBinder.cs:523`. Os cenários de Play acima ainda precisam de validação visual.
