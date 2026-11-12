# Erumperem

Turn-based combat RPG with an overworld exploration layer and a party of playable heroes.

## Heroes

**Matsuda**:
The third playable hero (full name Maria Matsuda), a support/debuffer whose epithet is "The Star".
_Avoid_: Maria, The Star (as a name)

**Wulfric**:
The starting playable hero, epithet "Splintered Knight".

**Buck**:
A playable hero, epithet "The Gunslinger".

**Epithet**:
A hero's title, shown as the name of their skill tree (e.g. "The Star").
_Avoid_: Nickname

## Combat

**Action Shot**:
The camera sequence that presents a combatant using an active skill; nothing else (DoT ticks, passives, turn-start effects) triggers one. Actor Focus: the camera zooms in on the actor, who plays the attack animation. Target Focus: a near-instant snap that frames every affected combatant while they play their reaction. Return: the camera eases back to the battle overview.
_Avoid_: Camera setting, cutscene

**Token**:
A stacking status on a combatant that decays one stack at end of turn; either a buff or a debuff.
_Avoid_: Status effect

**Confusion**:
A debuff token: at turn start each of the holder's skills may swap target side (Ally↔Enemy, Self→None) for the rest of that turn.
