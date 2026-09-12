# Wulfric — árvores de talentos

Limite de **2 heróis** no grupo: combates típicos **2v3** ou **2v4**.  
Contrato de runtime: `Assets/StreamingAssets/Data/` (`skills.json`, `passives.json`, `skill_trees.json`). Authoring: `CombatAbilityAsset` + Export Catalog (`docs/skill-authoring.md`).

## Skills inatas (sempre equipadas)

| ID | Nome |
| --- | --- |
| `wulfric_innate_active1` | Sword cleave |
| `wulfric_innate_active2` | Taunt |
| `wulfric_innate_active3` | Iron Maiden |
| `wulfric_innate_active4` | Raise Shield |

Também no catálogo: `wulfric_leader_passive1`, `wulfric_companion_passive1`, `wulfric_corruption_tier1..3_passive1`.

## Árvore 1 — Unstable Slasher

| Tier | Passivas | Ativa |
| --- | --- | --- |
| 1 | `wulfric_tree1_tier1_passive1..3` | `wulfric_tree1_tier1_active` |
| 2 | `wulfric_tree1_tier2_passive1..3` | `wulfric_tree1_tier2_active` |
| 3 | `wulfric_tree1_tier3_passive1..3` | `wulfric_tree1_tier3_active` |

## Árvore 2 — The Destabilizer

| Tier | Passivas | Ativa |
| --- | --- | --- |
| 1 | `wulfric_tree2_tier1_passive1..3` | `wulfric_tree2_tier1_active` |
| 2 | `wulfric_tree2_tier2_passive1..3` | `wulfric_tree2_tier2_active` |
| 3 | `wulfric_tree2_tier3_passive1..3` | `wulfric_tree2_tier3_active` |

## Árvore 3 — Stable Fortress

| Tier | Passivas | Ativa |
| --- | --- | --- |
| 1 | `wulfric_tree3_tier1_passive1..3` | `wulfric_tree3_tier1_active` |
| 2 | `wulfric_tree3_tier2_passive1..3` | `wulfric_tree3_tier2_active` |
| 3 | `wulfric_tree3_tier3_passive1..3` | `wulfric_tree3_tier3_active` |

IDs legado (`wulfric_innate_cleave`, `f_t*`, `m_t*`, `a_t*`) foram removidos na fase H.
