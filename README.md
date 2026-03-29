# ⚔️ Erumperem

Projeto Unity com simulação de combate em .NET para análise e balanceamento.

---

## 📂 Dados do jogo 

Os ficheiros JSON usados pelo carregamento de dados ficam em:

`Game.Simulations/Data/`


| Ficheiro           | Conteúdo             |
| ------------------ | -------------------- |
| `skills.json`      | Definições de skills |
| `enemies.json`     | Inimigos             |
| `skill_trees.json` | Árvores de skills    |

Por defeito a simulação carrega `skills.json` via `CombatDataLoader.ResolveDefaultSkillsPath()` (funciona a partir de `Game.Simulations` ou `Game.Tests`). As **passivas** dos nós estão em `skill_trees.json` para progressão/UI; o motor de combate atual aplica só **skills ativas** (incluindo as inatas do Wulfric). Ver `docs/wulfric-skill-trees.md` e a especificação de talentos em `docs/passives-system-spec.md`.

---

## 🎲 Simulação headless (CLI)

O projeto `Game.Simulations` corre batalhas em lote e gera CSVs de eventos e agregados (win rate por skill, etc.).

Na **raiz do repositório**, usa `dotnet run` com `--` para passar argumentos ao programa:

### Comandos úteis


| O quê                                             | Comando                                                                                                                    |
| ------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| 🏃 Correr **50** batalhas                         | `dotnet run --project Game.Simulations/Game.Simulations.csproj -- --battles 50`                                            |
| 🎰 Outra sequência “aleatória” (muda a seed base) | `dotnet run --project Game.Simulations/Game.Simulations.csproj -- --battles 50 --seed 12345`                               |
| 📁 Gravar CSV noutra pasta                        | `dotnet run --project Game.Simulations/Game.Simulations.csproj -- --battles 50 --out "C:\caminho\para\pasta"`              |
| 📜 Usar skills de um JSON                         | `dotnet run --project Game.Simulations/Game.Simulations.csproj -- --battles 10 --skills Game.Simulations/Data/skills.json` |


**Nota:** o `--` separa os argumentos do `dotnet` dos argumentos da simulação.

### ⚙️ Argumentos


| Argumento      | Default        | Descrição                                    |
| -------------- | -------------- | -------------------------------------------- |
| `--battles`    | `100`          | Número de batalhas                           |
| `--seed`       | `42`           | Seed base (cada batalha usa `seed + índice`) |
| `--out`        | *(ver abaixo)* | Pasta de saída dos CSV                       |
| `--skills`     | *(embutido)*   | Caminho para `skills.json`                   |
| `--enemies`    | *(opcional)*   | Caminho para `enemies.json`                  |
| `--skillTrees` | *(opcional)*   | Caminho para `skill_trees.json`              |


---

## 📤 Onde fica o output

Por defeito, os CSV são escritos em:

`**Game.Simulations/SimulationOutput/`**

Ficheiros gerados:


| Ficheiro                | Conteúdo                                                    |
| ----------------------- | ----------------------------------------------------------- |
| `combat_events.csv`     | Todos os eventos de combate (linha a linha)                 |
| `combat_aggregates.csv` | Agregados por skill (win rate, pick rate, dano médio, etc.) |


O consola também imprime um resumo da win rate por skill ao terminar.

> 💡 Esta pasta de simulação é **regenerável**; os dados de jogo versionados continuam em `Game.Simulations/Data/`.

---

## 🧪 Testes

```bash
dotnet test Game.Tests/Game.Tests.csproj
```

---

## 🔧 Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Unity (versão do projeto conforme `ProjectSettings`)

