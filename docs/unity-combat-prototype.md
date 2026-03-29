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
3. Crie um GameObject vazio **`CombatRoot`**.
4. Adicione o componente **`CombatPrototypeController`** (`Assets/_Project/Scripts/Combat/`).
5. **Play.**

### Jogar

- **Clique** numa cápsula **inimiga** (raycast) para definir alvo de skills `Enemy`.
- Teclas **1–7**: primeira a sétima skill do **loadout** do herói cuja vez é (apenas as que existem em `skills.json` e estão equipadas; até 7 slots).
- Skills **Self** / **Ally** não exigem inimigo selecionado (alvo aliado usa regras do `PlayerActionBuilder`; por defeito o próprio actor para aliados).
- **Inimigos** resolvem turnos com a mesma AI que a simulação até ser vez de um herói.

### Scripts principais

| Ficheiro | Função |
|----------|--------|
| `CombatPrototypeController.cs` | Arranque da batalha 2v4, loop de iniciativa, input, spawn de cápsulas. |
| `CombatCapsuleTag.cs` | Liga o collider ao `Identity.Id` do `Combatant`. |

O motor expõe API pública em `BattleSimulator`: `TryPrepareActorTurn`, `ChooseAiAction`, `ResolveChosenAction`, `BuildInitiativeOrder`, `EmitBattleStarted` / `EmitBattleEnded`. Montagem de ações do jogador: `PlayerActionBuilder.TryCreate`.

## Versão Unity

Projeto testado com **Unity 6000.3.x** (URP). Input: **Input System** (`UnityEngine.InputSystem`).

## Notas

- Não duplicar lógica de dano/DOT/passivas no Unity; estender sempre `Game.Core`.
- Para IL2CPP em dispositivos, pode ser necessário `link.xml` contra stripping de tipos serializados por JSON — tratar quando houver build de player.
