# Protótipo de combate no Unity

Integração com o motor **`Game.Core`** (mesmas regras que `BattleSimulator` e simulações headless). Pasta de jogo: **`Assets/_Project/`** (sem asmdef no protótipo).

## Referência Game.Core → Unity

- **Alvo:** `netstandard2.1` + pacotes `PolySharp` e `System.Text.Json` (definidos em `Game.Core.csproj`).
- **Saída:** `Assets/_Project/Plugins/GameCore/` (DLL + dependências).
- **Atualizar após mudanças no motor:**

```powershell
powershell -File tools/PublishGameCoreForUnity.ps1
```

Ou manualmente:

```text
dotnet publish Game.Core/Game.Core.csproj -c Release -f netstandard2.1 /p:CopyLocalLockFileAssemblies=true -o Assets/_Project/Plugins/GameCore
```

Abrir o Unity para reimportar os plugins.

## Dados (JSON)

Cópias em **`Assets/StreamingAssets/Data/`**:

- `skills.json`
- `passives.json`

O protótipo lê com `Application.streamingAssetsPath + "/Data/..."`. Se faltarem ficheiros, copie a partir de `Game.Simulations/Data/`.

## Cena e controlos

1. Crie ou abra uma cena (ex.: duplique `Assets/Scenes/SampleScene.unity` ou cena nova).
2. Garanta **Main Camera** (tag `MainCamera`) e iluminação básica.
3. Coloca na cena **2** prefabs de herói e **4** de inimigo (posição/arte à tua escolha). Cada um precisa de **Collider** para raycast; o controlador adiciona ou atualiza **`CombatCapsuleTag`** no root (ou reutiliza o que já existir num filho).
4. Crie um GameObject **`CombatRoot`** com **`CombatPrototypeController`**.
5. No Inspector, preenche **Ally Visual Roots** (tamanho 2): índice `0` = `ally_1`, `1` = `ally_2`. **Enemy Visual Roots** (tamanho 4): `0`..`3` = `enemy_1`..`enemy_4` (mesma ordem que `BattleFactory.CreateSampleBattle`).
6. Opcional: **`CombatHoverFocusMarker`** noutro objeto — autónomo (raycast + DOTween). **Marker Prefab**, câmara e layer mask conforme necessário.
7. **Play.**

**Nota:** **Sync Hp As Vertical Scale** escala o root pela % de HP (comportamento antigo das cápsulas). Desliga se os prefabs não devam ser escalados.

### Jogar

- **Clique** numa unidade **aliada** (collider + tag) para imprimir no **Console** a hotbar simplificada `[1]`–`[7]` (nome, dano, alvo, efeitos).
- **Clique** numa unidade **inimiga** (raycast) para definir alvo de skills `Enemy`.
- Teclas **1–7**: primeira a sétima skill do **loadout** do herói cuja vez é (apenas as que existem em `skills.json` e estão equipadas; até 7 slots).
- Skills **Self** / **Ally** não exigem inimigo selecionado (alvo aliado usa regras do `PlayerActionBuilder`; por defeito o próprio actor para aliados).
- **Inimigos** resolvem turnos com a mesma AI que a simulação até ser vez de um herói.

### Scripts principais

| Ficheiro | Função |
|----------|--------|
| `CombatPrototypeController.cs` | Batalha 2v4: liga unidades da cena ao estado, input, sync HP/visível. |
| `CombatHoverFocusMarker.cs` | Marcador de hover autónomo: raycast ao `CombatCapsuleTag`, posição acima do renderer, punch/spin DOTween. |
| `CombatCapsuleTag.cs` | Liga o collider ao `Identity.Id` do `Combatant`. |
| `CombatSkillBarDebug.cs` | Ao clicar num herói, imprime no console a hotbar [1]–[7] (mesma ordem que o combate). |

O motor expõe API pública em `BattleSimulator`: `TryPrepareActorTurn`, `ChooseAiAction`, `ResolveChosenAction`, `BuildInitiativeOrder`, `EmitBattleStarted` / `EmitBattleEnded`. Montagem de ações do jogador: `PlayerActionBuilder.TryCreate`.

## DOTween

O projeto inclui **DOTween** (Demigiant) para animações de “juice” (punch, fade, loops, etc.). Documentação: [DOTween — Documentation](https://dotween.demigiant.com/documentation.php).

## Versão Unity

Projeto testado com **Unity 6000.3.x** (URP). Input: **Input System** (`UnityEngine.InputSystem`).

## Notas

- Não duplicar lógica de dano/DOT/passivas no Unity; estender sempre `Game.Core`.
- Para IL2CPP em dispositivos, pode ser necessário `link.xml` contra stripping de tipos serializados por JSON — tratar quando houver build de player.
