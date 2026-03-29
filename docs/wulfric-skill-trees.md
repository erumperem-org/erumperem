# Wulfric — árvores de talentos (design)

Limite de **2 heróis** no grupo: combates típicos **2v3** ou **2v4**.

## Skills inatas (sempre equipadas)


| ID                      | Nome            | Função                                   |
| ----------------------- | --------------- | ---------------------------------------- |
| `wulfric_innate_cleave` | Talho direto    | 1 alvo inimigo, ranks 1–3                |
| `wulfric_innate_shove`  | Empurrão brutal | Ranks 1–2, empurra para trás, menos dano |
| `wulfric_innate_guard`  | Postura de lobo | Defesa: Block + Taunt em ti              |


## Árvore Fogo — *Forja interior*

DOT temático: **Bleed** (não Burn).


| Tier | Passivas (IDs)                  | Ativa                        |
| ---- | ------------------------------- | ---------------------------- |
| 1    | `f_t1_p1`, `f_t1_p2`, `f_t1_p3` | `f_t1_a1` Rasgar tendão      |
| 2    | `f_t2_p1`, `f_t2_p2`, `f_t2_p3` | `f_t2_a1` Fio candente       |
| 3    | `f_t3_p1`, `f_t3_p2`, `f_t3_p3` | `f_t3_a1` Execução de leilão |


## Árvore Metal — *Couraça e juramento*


| Tier | Passivas            | Ativa                      |
| ---- | ------------------- | -------------------------- |
| 1    | `m_t1_p1`–`m_t1_p3` | `m_t1_a1` Remendar couraça |
| 2    | `m_t2_p1`–`m_t2_p3` | `m_t2_a1` Muralha          |
| 3    | `m_t3_p1`–`m_t3_p3` | `m_t3_a1` Salvaguarda      |


## Árvore Anomalia — *Fio do Abismo*


| Tier | Passivas            | Ativa                     |
| ---- | ------------------- | ------------------------- |
| 1    | `a_t1_p1`–`a_t1_p3` | `a_t1_a1` Fio da anomalia |
| 2    | `a_t2_p1`–`a_t2_p3` | `a_t2_a1` Puxar o véu     |
| 3    | `a_t3_p1`–`a_t3_p3` | `a_t3_a1` Abrir o vão     |


---

## Simulação vs. jogo completo

- `**Game.Simulations/Data/skills.json`** — definições das **skills ativas** (danos, efeitos, `targetKind`, Bleed/Blight, etc.).
- `**Game.Simulations/Data/skill_trees.json`** — estrutura dos nós (passivas + ativas por tier).
- **Passivas** (`f_t1_p1`, …) ainda **não** aplicam modificadores no motor; só existem como dados para UI/progressão futura. O combate simulado usa as **ativas** e os **inatos** listados acima. Especificação técnica: `docs/passives-system-spec.md`; contratos: `Game.Core/Passives/PassiveSystemContracts.cs`.

