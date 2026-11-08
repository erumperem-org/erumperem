# ChaserAI (rework)

Sistema de perseguidores com responsabilidades separadas. Cada chaser tem seus sensores,
atuadores e view originais; o que mudou é como ele é gerado, mantido perto do player e
configurado por tipo.

## Arquitetura

```
Runtime/
  Core/          ChaserAI (máquina de estados), ChaserState, ChaserSettings,
                 ChaserContactSensor (contato físico), ChaserMovementValidators (factory)
  Movement/      IRoamStrategy + WanderAreaRoam (onde vagar), validadores, WanderArea
  Perception/    PerceptionSensor, PlayerFieldOfViewApproximation
  Spawning/      ChaserWorld (player atual + zonas), ChaserSpawner, ChaserRelocator,
                 ChaserPlacementRules (regras únicas de posição), ChaserSpawnSettings,
                 ChaserPool (só o registro; ver nota)
  Behaviours/    PeriodicHuntBehaviour (caçada periódica)
  Presentation/  ChaserAIView (modelo por tier), ChaserAnimationDriver, ChaserProximityAudio
  EnemyHuntOrchestrator.cs   (NÃO modificado)
```

| Responsabilidade | Quem |
|---|---|
| Estados Wandering / Chasing / Investigating, captura (decide se o contato é com o alvo) | `ChaserAI` |
| Avisar contato físico com qualquer collider | `ChaserContactSensor` |
| Qual é o player atual, zonas da cena, margem da área segura | `ChaserWorld` (evento `TargetChanged`) |
| Onde um chaser pode aparecer | `ChaserPlacementRules` (mapa, fora da área segura, fora da visão) |
| Criar a população e colocá-la perto do player | `ChaserSpawner` |
| Trazer de volta quem se afastou demais | `ChaserRelocator` |
| Tipo "caçador" | `PeriodicHuntBehaviour` |
| Para onde vagar (futuro: rodear baú) | `IRoamStrategy` via `ChaserAI.SetRoamStrategy` |

## Regras de spawn (as originais, em um só lugar)

Todo ponto de nascimento/realocação precisa estar: dentro de `MapLimits`, fora da área
segura **+ margem** (`ChaserWorld.safeAreaMargin`, a mesma que o chaser respeita ao andar) e fora da aproximação de campo de visão do player (esfera à frente dele). Sorteio
no anel `spawnMinDistance..spawnMaxDistance` ao redor do player. Quem passa de
`returnDistance` é teleportado de volta e retorna sempre em Wandering. **Só é realocado quem está em
Wandering** (`ChaserAI.CanBeRelocated`): quem persegue ou investiga (inclusive a caçada periódica) não é interrompido.
O teleporte posiciona o Rigidbody e zera a velocidade herdada.

## Captura

O `ChaserContactSensor` apenas avisa o contato (enter e stay) com qualquer collider. O `ChaserAI` captura **em qualquer
estado**, desde que o collider seja do alvo atual do `ChaserWorld` (o personagem Em Jogo). Companheiro e Resting
também são `PlayableCharacters`, mas nunca são o alvo, então não disparam captura. Em Chasing a captura também ocorre por
`catchDistance`.

## Travamento

Em Wandering e Investigating o chaser mede o progresso a cada `stuckCheckInterval`. Sem se mover `stuckMinProgress` por
`stuckGiveUpTime` segundos: Wandering sorteia outro destino; Investigating considera que chegou e começa a espera
(`investigateWaitTime`). Destinos de investigação dentro da área segura (+ margem) são levados para a borda externa dela,
porque o validador impede de entrar. Chasing não tem essa desistência: perseguir o player parado dentro do Hub
mantém o chaser na borda enquanto o enxergar.

## Obstáculos

Os casts de desvio e a linha de visão da percepção ignoram triggers (`QueryTriggerInteraction.Ignore`): um trigger
na layer de cenário não bloqueia nem esconde o player.

## Tipos

- **Vagante** (padrão): vaga e persegue ao avistar o player.
- **Caçador**: igual ao vagante, mais `PeriodicHuntBehaviour`: a cada intervalo sorteado
  (min..max), se o player estiver fora da área segura, vai até a posição dele naquele instante
  (Investigating; se o avistar no caminho, vira Chasing). Se o player estiver na área segura, a
  caçada é pulada e um novo intervalo começa.
- **Baú**: pendente (será uma `IRoamStrategy`).

Os tipos são "arquétipos" na lista do `ChaserSpawner` (prefab + quantidade + flag `periodicHunt`).

## Como montar

1. Reaproveite o asset `ChaserPoolSettings` existente (mesmo GUID: agora é um `ChaserSpawnSettings`)
   ou crie um novo (`Create > Movement > AI > Chaser Spawn Settings`).
2. No GameObject `ChaserPool` (filho de `Chasers`, irmão do `EnemyHuntOrchestrator`) mantenha o
   `ChaserPool` e adicione `ChaserWorld`, `ChaserSpawner` e `ChaserRelocator`.
3. `ChaserWorld`: arraste `PlayableCharacterController`, `MapLimits`, a `SafeArea` e a `WanderArea`, e ajuste
   `Safe Area Margin` (padrão 2). No Awake ele repassa `MapLimits`, `SafeArea` e margem ao `WanderArea`: não é mais
   preciso (nem útil) preencher esses campos nele.
4. `ChaserSpawner`: atribua o settings e crie os arquétipos (ex.: Vagante x20, Caçador x10, ambos com o
   prefab `Chaser`). Deixe `spawnParent` vazio: os chasers nascem em `Chasers`, o pai do orquestrador.
5. `ChaserRelocator`: atribua o settings.
6. As instâncias `Chaser (n)` soltas dentro de `Chasers` adotam o `ChaserWorld` da cena no Start e se registram no
   `ChaserPool`, então continuam funcionando. Se quiser só a população gerada, apague-as.
7. No prefab `Chaser`, desligue `enableStateDebugLogs` (ligado no prefab, spamma o Console).

## Animação (pronta para plugar)

`ChaserAnimationDriver` já está no prefab `Chaser`. Falta só: colocar o Animator Controller nos
Animators dos modelos e criar nele os parâmetros:

- `Speed` (Float): velocidade horizontal real em unid./s (ou 0..1 com `normalizeSpeed`)
- `Grab` (Trigger): disparado na captura

Os nomes são campos no Inspector (vazio = ignora). Funciona com os Animators dos 5 tiers (mesmo
rig/controller): os dos modelos inativos são pulados e passam a ser alimentados ao ativar.
Dica de performance: no Animator, `Culling Mode = Cull Update Transforms` para chasers fora da tela.

## Áudio

- "Avistou" (`EnemySpot`): inalterado, em `ChaserAI`.
- Proximidade: `ChaserProximityAudio` (já no prefab). Atribua um `loopClip`; é som 3D com rolloff
  linear, e só toca com o player a `maxDistance` (as demais ficam paradas, sem ocupar vozes).

## EnemyHuntOrchestrator (inalterado) – o que o sistema respeita

- Usa `ChaserAI.OnTargetCaught` e `ResetCatchStateForCombatRetry()`: mantidos.
- Encontra chasers por `ChaserPool.GetManagedChasers()` e por varredura do pai
  (`GetComponentsInChildren<ChaserAI>(true)`, incluindo inativos), e só se inscreve no `OnEnable`.
  Por isso o `ChaserSpawner` cria toda a população no Awake (execução antecipada), inicialmente
  inativa, e a população é fixa: nada é criado/destruído depois. O spawner ainda reinicia a inscrição do
  orquestrador no spawn inicial caso ele tenha sido habilitado antes.
- `ChaserPool` continua existindo só porque o orquestrador referencia o tipo. Não faz pooling.

## Diagnóstico (Console)

Cada falha de configuração agora diz a causa: `ChaserWorld` sem `PlayableCharacterController` ou sem
`WanderArea`, prefab sem `ChaserSettings`, spawner sem arquétipos, spawner esperando alvo ou sem ponto
válido (aviso após 5 s). Ao concluir, o spawner loga "N chasers ativos". Menu de contexto do `ChaserAI`:
`Debug > Forçar Unfreeze` (`Time.timeScale = 1`, caso o jogo esteja congelado).

## Valores que mudaram de lugar

`safeAreaAvoidanceMargin` (`ChaserSettings`) e `safeAreaMargin` (`WanderArea`) deixaram de existir como fontes: a
margem é só `ChaserWorld.safeAreaMargin`. Se você tinha um valor diferente de 2, transfira-o para lá.
`ChaserContactSensor.PlayerContact` virou `Contact(Collider)`.

## Removido

Pooling por roster fixo, `ChaserPoolEditor`, verificação de spawn inicial dentro do `ChaserAI`
(agora `ChaserPlacementRules`), `PlayerFieldOfViewApproximation.PushOutside`, e os campos `initial*` de `ChaserSettings`.
