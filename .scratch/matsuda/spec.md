# Spec: Matsuda — third playable hero

Labels: `ready-for-agent`

## Problem Statement

The party has only two playable heroes, Wulfric and Buck. Matsuda (Maria Matsuda, epithet "The Star") is designed (stats, innate kit, three skill trees, leader/companion/corruption passives), and much of her data already exists in the project, but she cannot be selected in the overworld and played in combat like the other two. Her data is also split across two names ("maria" in combat/progression, "Matsuda" in the overworld), there are two competing stat definitions for her, she has no battle model wired, her skill tree is missing from the skill tree panel, and the Confusion token does not behave as designed.

## Solution

Open Unity, pick Matsuda in the overworld the same way as Buck or Wulfric, take her into combat with her real 3D model, use her full kit as written in the design, spend skill points on her three trees in the skill tree panel, and see Confusion work as specified. Anything visual that is missing (icons, portrait, VFX) uses a placeholder. The goal is to play her normally; polish comes later.

## User Stories

1. As a player, I want to see Matsuda in the overworld, so that I know she is part of my group.
2. As a player, I want to select Matsuda as Main or Companion through the same flow as Buck and Wulfric, so that I can build the party I want.
3. As a player, I want Matsuda to start Resting by default, so that the existing Wulfric+Buck opening is unchanged.
4. As a player, I want Matsuda to enter combat with her own 3D model, so that she reads as a distinct hero.
5. As a player, I want Matsuda to have 70 max HP both in the overworld and in combat, so that her health is consistent.
6. As a player, I want Matsuda to have 12% defense and +1% critical chance, so that she matches her design.
7. As a player, I want to use Sound Strike (anomaly, 1 enemy, 4–8, 1% crit, 90% acc), so that I have a free basic attack.
8. As a player, I want to use Healing Voice (4 corruption, self or ally, 80% acc, heals 5–10), so that I can sustain the party.
9. As a player, I want to use Amplified Scream (3 corruption, all enemies, 3–6, 70% acc, 2 Vulnerability), so that I can set up team damage.
10. As a player, I want to use Inspirational Song (4 corruption, self and ally, 80% acc, 3 Strength), so that I can buff the team.
11. As a player with Matsuda as leader, I want each debuff token I apply to apply +1 more, so that leading with her pays off.
12. As a player with Matsuda as companion, I want each buff token she applies to apply +1 more, so that she supports the leader.
13. As a player raising corruption, I want Matsuda's tokens to be 25%/50%/100% more effective at tiers 1/2/3, so that corruption scales her identity.
14. As a player raising corruption, I want a 5%/10%/15% chance for Matsuda to use her basic attack on a target she just applied a token to, so that corruption gives her extra pressure.
15. As a player, I want the Life Symphony tree (passives and Echoing Regeneration, Refreshing Song, Resurrection Hymn) to work as designed, so that a healer build is viable.
16. As a player, I want the Battle Hymn tree (passives and Higher Pitch, Protection Song, Dance of the Revolution) to work as designed, so that a buffer build is viable.
17. As a player, I want the Deafening Scream tree (passives and Screech Noise, Piercing Yell, Chaos Melody) to work as designed, so that a debuffer build is viable.
18. As a player, I want "heal" passives to trigger from any of Matsuda's heal effects, so that tree synergies feel reliable.
19. As a player, I want "when your team gets hit" passives to trigger when either party member is hit, so that her defensive debuffs work.
20. As a player, I want "team crits deal triple if buffed" to apply to all heroes, so that the Battle Hymn capstone matters.
21. As a player, I want Resurrection Hymn to revive a dead ally, so that the 20-corruption capstone is worth it.
22. As a player, I want Matsuda's tree in the skill tree panel, titled "The Star", reachable with the same arrows as Wulfric and Buck, so that I can progress her.
23. As a player, I want Matsuda to share the same skill point budget as the other heroes, so that progression rules are uniform.
24. As a player, I want to reset Matsuda's skill tree like the others', so that I can respec.
25. As a player, I want the UI to call her "Matsuda" everywhere, so that her name is consistent.
26. As a player, I want missing icons and portraits to show a placeholder instead of breaking, so that I can play now.
27. As a player whose hero is Confused, I want each skill to have a 33% chance at turn start to swap its targets for the rest of the turn (Ally↔Enemy, Self→None), so that Confusion is a real threat.
28. As a player applying Confusion to enemies (Chaos Melody), I want enemies to suffer the same swap rules, so that the debuff is symmetric.
29. As a player, I want a Confused skill whose target becomes None to do nothing, so that the rule is predictable.
30. As a designer, I want Matsuda's abilities authored as ScriptableObjects and exported to the catalog like the other kits, so that I tune her without code.
31. As a designer, I want a single id (`matsuda`) for her across progression, combat, overworld and assets, so that no data is split across two names.
32. As a designer, I want only one stat definition for her, so that edits are not silently ignored.
33. As a developer, I want automated tests for her kit and for Confusion, so that regressions are caught without opening Unity.
34. As a developer, I want the authoring docs to describe the Matsuda kit and the real Confusion behaviour, so that the docs stop contradicting the code.

## Implementation Decisions

- **One id: `matsuda`.** Every `maria` id (skills, passives, tree nodes, kit, `ownerCharacterId`, progression character id, reset enum value, factory loadouts, tests, docs) is renamed to `matsuda`, and the `maria_*` ability ids become `matsuda_*`. The skill tree node assets (today named `maria*` in camelCase) follow the same rename. The authoring doc's "don't create a Matsuda kit" rule is reversed.
- **Display:** player-facing name "Matsuda"; "The Star" is the epithet (skill tree title). Glossary terms: Matsuda, Epithet, Token, Confusion.
- **Stat definition:** keep the Matsuda stat asset as the only definition (max HP 70, defense 12%, crit 1%, progression id `matsuda`, element anomaly) and delete the duplicate "Maria" definition. Hardcoded overworld fallbacks for Matsuda (max HP 100, starting HP 0) become 70/70.
- **Model:** the existing Matsuda character prefab (the one in the main character prefabs folder, not the Rework variant) is her battle prefab, and the overworld uses the same model.
- **Overworld:** default exploration state is Resting. Selection goes through the existing playable-character flow; no new UI.
- **Skill tree panel:** a third character profile (`matsuda`, "The Star", placeholder portrait and background colour, her tree root). The editor setup tool that builds the panel generates three profiles. `matsuda` joins the shared skill-level character list.
- **Kit content:** the pasted design is the source of truth. Every existing Matsuda ability is checked against it (cost, element, target kind, damage range, crit, accuracy, effects, token counts) and corrected in the ScriptableObjects, then the catalog is re-exported.
- **Passives:** expressed with the existing Conditions + Effects model. Where a passive cannot be expressed, the smallest general capability is added to the passive engine (no per-passive one-off cases). Expected candidates: "use basic attack on a target you applied a token to" (chance-based follow-up), crit multiplier override while buffed, "token potency +X%" for tokens applied by the owner, "on team member hit, debuff the attacker", "first time below 25% HP at turn start" (once per battle), and "heal for a fraction of damage dealt".
- **Token effectiveness:** "X% more effective" scales the token's per-stack magnitude for tokens Matsuda applied (stack count unchanged).
- **Confusion:** the target-swap table already exists in the domain. The fix makes the 33% roll happen per skill at the holder's turn start, persist for the rest of that turn, apply to heroes and enemies alike, and apply to both player-driven and AI-driven turns. Self→None means the skill resolves with no targets.
- **Placeholders:** missing icons, portrait and VFX reuse existing assets. No new art.

## Testing Decisions

- A good test drives a real battle through the public engine (catalog loaded from the shipped data, battle built by the factory, turns run through the simulator) and asserts only observable outcomes: HP, token stacks, who was targeted, and battle log events. No asserting on private helpers.
- **One automated seam:** the `Game.Core` battle simulation over the real catalog. It covers stats, every innate and tree skill, every passive, leader/companion/corruption passives, Confusion (heroes and enemies, swap table, None case, per-turn persistence, with a seeded RNG), and the id rename (tests reference `matsuda_*`).
- **Prior art:** the existing per-hero kit tests (Wulfric, Buck and the current Maria kit tests, which get renamed), the skill contract tests, and the skill tree catalog description tests.
- **Unity wiring has no automated seam.** It is verified by a manual Play Mode checklist: Matsuda appears Resting in the overworld; she can be selected as Main and as Companion; she enters combat with her model; her HP is 70 and her skills show; the skill tree panel cycles to "The Star", spends points and resets; the catalog export runs cleanly.

## Out of Scope

- Final icons, portrait, VFX, animations beyond the existing Matsuda rig, audio.
- Balancing beyond the design numbers.
- New overworld selection UI.
- Cutscenes for Resurrection Hymn.
- Other heroes' kits, except where a shared engine change (Confusion, new passive capabilities) touches them.

## Further Notes

- Decisions came from a grilling session; vocabulary is in `GLOSSARY.md`.
- The rename should land first: every other slice references `matsuda_*` ids.
- Confusion currently documents itself as an MVP (enemies-only random retarget) while the domain already holds the full swap table; the docs are updated with the fix.
