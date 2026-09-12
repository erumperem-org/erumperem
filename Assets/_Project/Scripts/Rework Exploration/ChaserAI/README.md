# EnemyOrchestration

Camada acima da IA de cada inimigo individual. Agrega uma ou mais áreas
seguras (`Hub`, do pacote `AreaZones`) e decide, de forma única e
centralizada, se o alvo está "disponível para caça" no momento — repassando
essa decisão para qualquer inimigo interessado, sem depender do tipo
concreto de IA usado por eles.

---

## 1. Dependências

| Dependência | De onde vem | Para quê |
|---|---|---|
| `Hub` | Pacote `AreaZones` | Fonte dos eventos de entrada/saída do alvo em cada área segura |

**Não depende do pacote `ChaserAI`**, nem de qualquer outro tipo de inimigo.
A comunicação com os inimigos acontece via `UnityEvent`, conectado pelo
Inspector — não por referência de código. Isso significa que qualquer
inimigo, atual ou futuro (não só `ChaserAI`), pode se conectar sem este
pacote precisar saber que ele existe.

---

## 2. Arquivos

| Arquivo | O que é |
|---|---|
| `Runtime/EnemyHuntOrchestrator.cs` | Escuta uma lista de `Hub`, agrega o estado e expõe dois `UnityEvent`. |
| `Editor/EnemyHuntOrchestratorEditor.cs` | Mostra em Play Mode se o alvo está disponível para caça ou em área segura. |

---

## 3. Por que agregação por contagem, e não booleano simples

Se houver duas áreas seguras sobrepostas e o alvo estiver dentro das duas,
sair de apenas uma não deve liberar a caça — o alvo continua seguro pela
outra. Por isso o orquestrador mantém uma contagem de quantas áreas
monitoradas contêm o alvo agora, em vez de um booleano que uma única área
poderia sobrescrever incorretamente:

- Entrar em qualquer área → contagem `+1`. Se a contagem foi de `0` para
  `1`, dispara `OnTargetUnavailableForHunt`.
- Sair de qualquer área → contagem `-1`. Se a contagem chegou a `0`,
  dispara `OnTargetAvailableForHunt`.

Isso também cobre o caso de uma cena carregar com o alvo já dentro de uma
área segura: ao ativar o orquestrador, ele sincroniza a contagem com o
estado atual de cada `Hub` (`Hub.IsTargetInside`) antes de começar a ouvir
novos eventos, e já propaga `OnTargetUnavailableForHunt` se for o caso.

---

## 4. Como usar

1. Adicione o componente `EnemyHuntOrchestrator` a um GameObject (ex:
   `GameSystems/EnemyHuntOrchestrator`, um objeto de configuração único na
   cena).
2. No campo `Safe Areas`, arraste todos os `Hub` que devem contar como
   "área segura" para este jogo (pode ser um ou vários).
3. No Inspector, conecte os eventos `On Target Available For Hunt` e
   `On Target Unavailable For Hunt` (como qualquer `UnityEvent`, ex:
   `Button.OnClick`): clique em `+`, arraste o `GameObject` do inimigo, e
   selecione o método público correspondente — por exemplo,
   `ChaserAI.ExitResting` em `OnTargetAvailableForHunt` e
   `ChaserAI.EnterResting` em `OnTargetUnavailableForHunt`.
4. Repita a conexão para cada inimigo da cena. Para muitos inimigos, isso
   pode ficar repetitivo pelo Inspector — se esse for o caso no seu projeto,
   um próximo passo natural seria um pequeno componente "broadcaster" que
   mantém a lista de inimigos por código e reencaminha os dois eventos, mas
   isso não foi incluído aqui para não acoplar este pacote a um tipo
   concreto de inimigo sem necessidade real ainda.
5. Em Play Mode, o Inspector do `EnemyHuntOrchestrator` mostra o estado
   atual ("Disponível para caça" / "Em área segura") para conferência
   rápida sem precisar abrir cada `Hub` individualmente.

---

## 5. Notas

- `IsTargetAvailableForHunt` é uma propriedade pública, para o caso de algum
  sistema preferir consultar sob demanda em vez de assinar os eventos.
- Se um `Hub` da lista for destruído/removido em runtime sem passar por
  `OnDisable` neste componente antes, os eventos daquele `Hub` continuam
  inscritos até este componente também ser desativado — não há
  cancelamento automático por destruição de um `Hub` individual isolado.
  Isso não deveria ser um problema em cenas onde os `Hub` são fixos, mas
  vale registrar caso seu projeto crie/destrua áreas seguras dinamicamente.

  # ChaserPool

Pool de tamanho fixo de `ChaserAI` pré-existentes na cena. Mantém um número
configurável ativos ao redor do personagem Em Jogo atual, sem nunca lotar a
cena — o resto do roster fica recolhido em Resting, teleportado para um
ponto de espera, até ser reaproveitado.

---

## 1. Dependências

| Dependência | De onde vem | Para quê |
|---|---|---|
| `ChaserAI`, `EnterResting`/`ExitResting`/`SetTarget` | Pacote `ChaserAI` | Cada elemento da pool |
| `MapLimits`, `SafeArea` | Pacote `AreaZones` | Delimitar a área de spawn (mapa inteiro, exceto o HUB) |
| `PlayableCharacterController.OnCharacterEnteredInGame`, `InGameCharacter` | Pacote `PlayableCharacters` | Retargeting automático ao trocar de personagem Em Jogo |

`ChaserAI` ganhou um método novo, `SetTarget(Transform)`, para permitir essa
troca dinâmica (antes o alvo só podia ser definido uma vez, no Inspector).

---

## 2. Por que nada é instanciado/destruído

Segue o mesmo espírito de `PlayableCharacterController`: todos os `ChaserAI`
existem na cena o tempo todo, arrastados no Inspector do `ChaserPool`. A
pool só alterna cada um entre **Resting** (recolhido, parado no
`holdingPoint`) e **ativo** (Wandering/Chasing/Investigating normalmente,
como qualquer `ChaserAI`) — nunca `Instantiate`/`Destroy`.

---

## 3. Onde e como um Chaser aparece

Ao precisar ativar mais um Chaser (`activeChasers.Count < desiredActiveCount`),
a pool sorteia pontos num anel entre `spawnMinDistance` e `spawnMaxDistance`
ao redor do player, rejeitando qualquer candidato que seja:

- fora de `MapLimits`;
- dentro da `SafeArea` (HUB) excluída;
- visível pelo player (ver seção 4).

Se nenhum ponto válido for encontrado em `maxSampleAttempts` tentativas
numa rodada, a pool simplesmente tenta de novo na próxima avaliação — não
força um spawn ruim.

---

## 4. "Fora do campo de visão": aproximação simplificada por distância

Em vez de ângulo/dot-product, frustum de câmera, ou raycast de obstrução,
o campo de visão do player é aproximado por uma **esfera simples à frente
dele**:

viewPoint = player.position + player.forward * fieldOfViewForwardOffset


Um candidato a spawn é descartado se `Vector3.Distance(candidato, viewPoint)
<= fieldOfViewRadius`. Nenhuma referência de câmera é necessária, e o
cálculo é barato (uma distância, sem raycast).

**Trade-off explícito**: essa aproximação não conhece obstáculos nem o
ângulo real de visão da câmera — é geometricamente uma esfera, não um cone
de visão. Se isso se mostrar impreciso demais em playtests (ex: câmera
ortogonal, ou visão muito mais larga/estreita que uma esfera cobre bem),
trocar para um cone (dot-product) ou considerar obstrução por raycast fica
isolado inteiramente dentro de `IsInsidePlayerFieldOfView` — nenhum outro
método precisa mudar.

---

## 5. Histerese entre spawn e retorno

`returnDistance` deve ser configurado bem maior que `spawnMaxDistance`. Um
Chaser spawnado a, digamos, 20 unidades não deve imediatamente flertar com
a distância de retorno se o player andar um pouco na direção oposta — a
margem entre os dois evita spawn/retorno oscilando em sequência.

---

## 6. Retorno à pool

Quando um Chaser ativo fica a mais de `returnDistance` do player
(avaliado a cada `evaluationInterval` segundos, não todo frame):

1. `chaser.EnterResting()` — zera movimento e desliga percepção primeiro.
2. Só depois o `transform.position` é movido para `holdingPoint` —
   teleporte, não caminhada.

Essa ordem evita qualquer movimento/percepção rodando no frame em que a
posição muda bruscamente.

---

## 7. Reativação

Sempre entra em **Wandering** (`ExitResting()`), nunca direto em Chasing,
mesmo que o player esteja tecnicamente perto do ponto de spawn — mesma
regra já usada em `PlayableCharacter.ExitResting()`, por consistência.

---

## 8. Retargeting em troca de personagem

Ao ouvir `PlayableCharacterController.OnCharacterEnteredInGame`, a pool
chama `SetTarget` em **todos** os Chasers do roster (ativos ou não) — não
só nos ativos. Isso evita que um Chaser recolhido na pool seja reativado
mais tarde ainda apontando para um personagem antigo que já não é mais o
Em Jogo.

---

## 9. Como usar

1. Crie um `ChaserPoolSettings`
   (`Assets > Create > Movement > AI > Chaser Pool Settings`) e ajuste os
   valores — em especial, confirme que `returnDistance` é bem maior que
   `spawnMaxDistance`.
2. Crie um GameObject vazio como `holdingPoint`, posicionado fora da área
   jogável (ou qualquer lugar discreto).
3. Crie um GameObject com o componente `ChaserPool`. Arraste:
   - todos os `ChaserAI` da cena, no campo `Chasers`;
   - o `PlayableCharacterController` da cena;
   - o `MapLimits` e a `SafeArea`/`Hub` (pacote `AreaZones`);
   - o `holdingPoint`;
   - o `ChaserPoolSettings` criado no passo 1.
4. Rode a cena. Em Play Mode, o Inspector do `ChaserPool` mostra
   "Ativos: X / Y" e oferece botões para forçar uma reavaliação imediata
   ou recolher todos os Chasers manualmente, sem precisar esperar o
   `evaluationInterval`.

---

## 10. Notas

- A avaliação roda numa coroutine própria (`evaluationInterval`, padrão
  1s) — não a cada frame. Ajuste para menor se quiser reações mais rápidas
  a mudanças de posição do player, ao custo de mais checagens de distância.
- Se `desiredActiveCount` for maior que `PoolSize`, a pool simplesmente
  ativa todos os que existem e para — não é um erro, só um teto natural.
- `RandomPointAroundTarget` não flatten Y no cálculo do anel (usa
  `currentTarget.position.y` diretamente) - consistente com o resto do
  projeto assumindo terreno sem variação de altura relevante.