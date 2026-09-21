# EnemyOrchestration

Camada acima da IA de cada Chaser individual. Escuta a captura do alvo por
qualquer um dos `ChaserAI` monitorados e expõe um único evento agregado
(`OnPreyCaught`), para que outros sistemas (troca de cena, UI de derrota,
etc.) encadeiem suas próprias ações sem precisar conhecer `ChaserAI`
diretamente nem se inscrever em cada instância individualmente.

---

## 1. Dependências

| Dependência | De onde vem | Para quê |
|---|---|---|
| `ChaserAI.OnTargetCaught` | Pacote `ChaserAI` | Evento (C#) de cada Chaser individual que o orquestrador agrega |

A comunicação para **fora** do orquestrador acontece via `UnityEvent`
(`OnPreyCaught`), conectado pelo Inspector — não por referência de código.
Isso significa que qualquer sistema, atual ou futuro, pode se conectar sem
este pacote precisar saber que ele existe.

---

## 2. Arquivos

| Arquivo | O que é |
|---|---|
| `Runtime/EnemyHuntOrchestrator.cs` | Assina `OnTargetCaught` de uma lista de `ChaserAI` e repassa como um único `OnPreyCaught`. |
| `Editor/EnemyHuntOrchestratorEditor.cs` | Inspector padrão (sem estado próprio para exibir). |

---

## 3. Como usar

1. Adicione o componente `EnemyHuntOrchestrator` a um GameObject (ex:
   `GameSystems/EnemyHuntOrchestrator`, um objeto de configuração único na
   cena).
2. No campo `Chasers`, arraste todos os `ChaserAI` cuja captura do alvo deve
   contar (normalmente, o mesmo roster usado no `ChaserPool`).
3. No Inspector, conecte `On Prey Caught` (como qualquer `UnityEvent`) a
   quem deve reagir — por exemplo, um script que carrega a cena de combate.
   O `ChaserAI` não carrega mais nenhuma cena sozinho; isso agora é
   responsabilidade de quem escuta este evento.

---

## 4. Notas

- `OnPreyCaught` dispara na primeira captura por qualquer Chaser da lista —
  não distingue qual deles pegou o alvo.
- Se um `ChaserAI` da lista for destruído/removido em runtime sem passar
  por `OnDisable` neste componente antes, a assinatura daquele Chaser
  continua até este componente também ser desativado — não há cancelamento
  automático por destruição de um Chaser individual isolado. Isso não
  deveria ser um problema em cenas onde o roster é fixo (ver `ChaserPool`).

  # ChaserPool

Conjunto fixo de `ChaserAI` pré-existentes na cena, **todos sempre ativos**
(Wandering/Chasing/Investigating — não existe mais um estado Resting).
Periodicamente verifica a distância de cada um até o personagem Em Jogo
atual e teleporta de volta, para perto do player, qualquer Chaser que se
afaste demais — permitindo que poucos `ChaserAI` deem a impressão de povoar
um mapa grande, sem nunca desligar movimento/percepção de nenhum deles.

---

## 1. Dependências

| Dependência | De onde vem | Para quê |
|---|---|---|
| `ChaserAI`, `Relocate`/`SetTarget` | Pacote `ChaserAI` | Cada elemento do pool |
| `MapLimits`, `SafeArea` | Pacote `AreaZones` | Delimitar a área de reposicionamento (mapa inteiro, exceto o HUB) |
| `PlayableCharacterController.OnCharacterEnteredInGame`, `InGameCharacter` | Pacote `PlayableCharacters` | Retargeting automático ao trocar de personagem Em Jogo |

`ChaserAI` expõe dois métodos usados pelo pool: `SetTarget(Transform)`,
para a troca dinâmica de alvo (o personagem "Em Jogo" pode mudar em
runtime), e `Relocate(Vector3)`, para o teleporte de volta para perto do
player.

---

## 2. Por que nada é instanciado/destruído

Todos os `ChaserAI` existem na cena o tempo todo, arrastados no Inspector
do `ChaserPool`. A pool nunca `Instantiate`/`Destroy` nem desliga nenhum
deles — só teleporta (`Relocate`) o Chaser que ficar longe demais de volta
para um ponto próximo do player, e ele continua imediatamente em
Wandering.

---

## 3. Reposicionamento por distância

A cada `evaluationInterval` segundos (coroutine própria, não todo frame), a
pool mede a distância de cada Chaser da lista até o alvo atual. Qualquer um
a mais de `returnDistance` sorteia um novo ponto num anel entre
`spawnMinDistance` e `spawnMaxDistance` ao redor do player, rejeitando
qualquer candidato que seja:

- fora de `MapLimits`;
- dentro da `SafeArea` (HUB) excluída;
- visível pelo player (ver seção 4).

Se nenhum ponto válido for encontrado em `maxSampleAttempts` tentativas
numa rodada, o Chaser simplesmente continua onde está e a pool tenta de
novo na próxima avaliação — não força um reposicionamento ruim.

---

## 4. "Fora do campo de visão": aproximação simplificada por distância

Em vez de ângulo/dot-product, frustum de câmera, ou raycast de obstrução,
o campo de visão do player é aproximado por uma **esfera simples à frente
dele** (`Runtime/PlayerFieldOfViewApproximation.cs`, compartilhada com o
próprio `ChaserAI` — ver README do pacote `ChaserAI`, seção de
posicionamento inicial):

viewPoint = player.position + player.forward \* fieldOfViewForwardOffset

Um candidato a reposicionamento é descartado se `Vector3.Distance(candidato,
viewPoint) <= fieldOfViewRadius`. Nenhuma referência de câmera é
necessária, e o cálculo é barato (uma distância, sem raycast).

**Trade-off explícito**: essa aproximação não conhece obstáculos nem o
ângulo real de visão da câmera — é geometricamente uma esfera, não um cone
de visão. Se isso se mostrar impreciso demais em playtests (ex: câmera
ortogonal, ou visão muito mais larga/estreita que uma esfera cobre bem),
trocar para um cone (dot-product) ou considerar obstrução por raycast fica
isolado inteiramente em `PlayerFieldOfViewApproximation` — nenhum outro
método precisa mudar.

---

## 5. Histerese entre reposicionamentos

`returnDistance` deve ser configurado bem maior que `spawnMaxDistance`. Um
Chaser reposicionado a, digamos, 20 unidades não deve imediatamente
flertar com a distância de retorno se o player andar um pouco na direção
oposta — a margem entre os dois evita reposicionamentos oscilando em
sequência.

---

## 6. Reativação após teleporte

Sempre volta em **Wandering** (`ChaserAI.Relocate` chama isso
internamente), nunca direto em Chasing, mesmo que o novo ponto esteja
tecnicamente perto do player.

---

## 7. Retargeting em troca de personagem

Ao ouvir `PlayableCharacterController.OnCharacterEnteredInGame`, a pool
chama `SetTarget` em **todos** os Chasers do roster — `ChaserAI.SetTarget`
já repassa a troca imediatamente para o `PerceptionSensor`, então não há
mais nenhuma etapa "adormecida" que precise ser reativada para pegar o
alvo novo.

---

## 8. Como usar

1. Crie um `ChaserPoolSettings`
   (`Assets > Create > Movement > AI > Chaser Pool Settings`) e ajuste os
   valores — em especial, confirme que `returnDistance` é bem maior que
   `spawnMaxDistance`.
2. Crie um GameObject com o componente `ChaserPool`. Arraste:
   - todos os `ChaserAI` da cena, no campo `Chasers`;
   - o `PlayableCharacterController` da cena;
   - o `MapLimits` e a `SafeArea`/`Hub` (pacote `AreaZones`);
   - o `ChaserPoolSettings` criado no passo 1.
3. Rode a cena. Em Play Mode, o Inspector do `ChaserPool` mostra o total de
   Chasers gerenciados e oferece um botão para forçar uma reavaliação
   imediata, sem precisar esperar o `evaluationInterval`.

---

## 9. Notas

- Não existe mais um "holding point": como nenhum Chaser é desligado, não
  há para onde "recolher" ninguém — o reposicionamento sempre acontece
  direto para um ponto próximo e válido perto do player.
- Se `RandomPointAroundTarget` não flatten Y no cálculo do anel (usa
  `currentTarget.position.y` diretamente) - consistente com o resto do
  projeto assumindo terreno sem variação de altura relevante.

---

# ChaserAI: percepção sensível ao estado do alvo + posicionamento inicial

## 1. Fatores de percepção (`TorchStateFactor` / `MovementStateFactor`)

O `PerceptionSensor` lê, a cada checagem, o `CharacterStateExposed` do alvo
atual (se ele tiver um) e calcula:

raio efetivo = `perceptionRadius` × fator de movimento × fator de tocha

- Fator de movimento: `movementFactorIdle` / `movementFactorWalk` /
  `movementFactorRun` (`ChaserSettings`), conforme
  `CharacterStateExposed.MovementState`.
- Fator de tocha: `torchFactorOff` / `torchFactorOn`, conforme
  `CharacterStateExposed.TorchState`.

Se o alvo não tiver `CharacterStateExposed` (ou `target` for null), os dois
fatores caem para `1` — sem bônus nem penalidade, comportamento idêntico ao
anterior. A leitura é feita por polling (a cada
`perceptionCheckInterval`), não por assinatura de evento — reage
naturalmente a uma troca de alvo em runtime (`SetTarget`) sem precisar
gerenciar inscrição/cancelamento de eventos do alvo antigo.

**Assumido, ajuste se necessário**: os valores default
(`Idle=0.7, Walk=1, Run=1.4` / `Off=1, On=1.6`) são só um ponto de partida
razoável — o ajuste fino é feito diretamente no `ChaserSettings`.

## 2. Posicionamento inicial (não nascer visível ao player)

Antes de entrar em Wandering pela primeira vez, todo `ChaserAI.Start()`
verifica se sua posição atual está dentro da mesma aproximação de campo de
visão do player usada pelo `ChaserPool`
(`PlayerFieldOfViewApproximation`, com os parâmetros
`initialViewForwardOffset`/`initialViewRadius` do `ChaserSettings`). Se
estiver, sorteia um ponto fora dela num raio (`initialPlacementSearchRadius`)
ao redor da própria posição, respeitando `MapLimits` e a `SafeArea`
excluída (se atribuídos no Inspector do `ChaserAI`). Se nenhuma tentativa
der certo em `initialPlacementMaxSampleAttempts`, cai num fallback que
empurra o Chaser em linha reta para fora da esfera de visão (sem garantia
de respeitar `MapLimits`/área excluída nesse caso extremo).

Isso é independente do `ChaserPool` — funciona também para um `ChaserAI`
posicionado manualmente numa cena de teste sem pool nenhum.

## 3. Sem Resting

Não existe mais o estado `Resting`. Todo `ChaserAI` fica sempre em
Wandering, Chasing ou Investigating, com a percepção sempre ligada. O
`ChaserPool` continua existindo para reposicionar (`Relocate`) quem se
afastar demais do player, mas isso nunca desliga movimento/percepção — é
um teleporte seguido de reentrada normal em Wandering.
