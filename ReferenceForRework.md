Base Stats
Nickname: Splintered Knight

Health: 100
Defense: 25%
Critical Chance: +3%

Leader Passive: Self heal 5 for each defeated enemy 
Companion Passive: +2% defense per 18 health below maximum 

Corruption Passive:
Tier 1: Taking damage grants +6% defense until the end of the enemies turn, initial defense -5%.
Tier 2: Taking damage grants +12% defense until the end of the enemies turn, initial defense -10%
Tier 3: Taking damage grants +18% defense until the end of the enemies turn, initial defense -15%

Base Kit

ACTIVE SKILL - BASIC ATTACK
id: wulfricBasicHit
name: Sword cleave
corruption cost: 0
element: metal
target kind: 1 enemy
base damage: 6 to 10
base crit chance: 2%
accuracy: 80%
effects on hit: none


ACTIVE SKILL - 1
id: wulfricTaunt
name: Taunt
corruption cost: 2
element: metal
target kind: self
base damage: 0
base crit chance: 0
accuracy: 100%
effects on hit: Apply 1 taunt token and apply 2 controlled instability

ACTIVE SKILL - 2
id: wulfricAreaAttack
name: Iron Maiden
corruption cost: 3
element: metal
target kind: 3 enemies
base damage: 5 to 8
base crit chance: 3%
accuracy: 70%
effects on hit: Applies 2 destabilization tokens

ACTIVE SKILL - 3:
id: wulfricRaiseShield
name: Raise Shield
corruption cost: 2
element: metal
target kind: self
base damage: 0
base crit chance: 0
accuracy: 100%
effects on hit: Apply 3 defense token


TREE 1: UNSTABLE SLASHER

Tier 1 Passives:
Using your basic attack against an enemy with Destabilization grants +1 controlled instability to self
Defense tokens are +25% more effective in you
Receives 1 controlled instability for every 10 health lost

ACTIVE SKILL - 4
id: wulfricUnstable
name: Focused Instability
corruption cost: 4
element: anomaly
target kind: self
base damage: 0
base crit chance: 0
accuracy: 100%
effects on hit: Apply 6 Controlled instability

Tier 2 Skills:
Controlled stability deals 4 damage per stack while you have defense tokens
Receive 1 Controlled instability for every 2 defense tokens received
When you apply Destabilization receive +1 controlled instability

ACTIVE SKILL - 5
id: wulfricStabilize
name: Re-Stabilization
corruption cost: 4
element: anomaly
target kind: self
base damage: 0
base crit chance: 0
accuracy: 100%
effects on hit: Lose all Controlled instability tokens, recover 1 health per lost token.

Tier 3 Skills:
When you receive Controlled instability receive +1 more
Deal +1 damage for every 3 current Controlled instability 
recover 1 health for every 5 Controlled instability applied


ACTIVE SKILL - 6
id: wulfricNocontrol
name: Loss of control
corruption cost: 6
element: anomaly
target kind: all enemies
base damage: 0
base crit chance: 0
accuracy: 75%
effects on hit: Lose all Controlled instability tokens, deal 6 damage per lost token, lose 1 health per token lost.






TREE 2: THE DESTABILIZER
Tier 1 Passives:
Receive +1 Controlled Instability when hit by enemies with Destabilization
Your defense tokens are +10% more effective when hit by enemies with Destabilization
Receives 1 Strength for every 5 health lost

ACTIVE SKILL - 4
id: wulfricWhip
name: Whip Sword
corruption cost: 4
element: metal
target kind: all enemies
base damage: 3 to 5
base crit chance: 3%
accuracy: 90%
effects on hit: Applies 1 destabilization token, applies +2 destabilization tokens per 1 current Controlled Instability tokens

Tier 2 Skills:
When you apply Destabilization, apply +1 more
When you apply Destabilization there’s a 25% chance you also apply the same amount of Weaken
You deal 10% extra damage per Destabilization token in the enemy

ACTIVE SKILL - 5
id: wulfricBigSword
name: From Body to Blade
corruption cost: 6
element: metal
target kind: 1 enemy
base damage: 25 to 30
base crit chance: 5%
accuracy: 200%
effects on hit: Lose 10 health

Tier 3 Skills:
Change the target of your basic attack to “3 enemies”.
When you apply Destabilization there’s a 25% chance you also apply the same amount of Vulnerability
Destabilization tokens are 25% more effective per Controlled Instability stack

ACTIVE SKILL - 6
id: wulfricForceExplosion
name: Unleash Instability
corruption cost: 5
element: anomaly
target kind: all enemies
base damage: 0
base crit chance: 0
accuracy: 200%
effects on hit: Trigger the effect of all Destabilization tokens









TREE 3: STABLE FORTRESS

Tier 1 Passives:
Base defense is +15%
Defense tokens you apply are 75% more effective
When you receive controlled instability you also receive +1 defense token

ACTIVE SKILL - 4
id: wulfricShieldAttack
name: Shield Charge
corruption cost: 2
element: metal
target kind: 1 enemy
base damage: 2 to 4
base crit chance: 2%
accuracy: 90%
effects on hit: apply 1 Confusion, deals +1 damage per defense token

Tier 2 Skills:
50% chance to gain a defense token at the start of the turn
Receive +1 defense token when hit by enemies with Destabilization
Shield charges also deal +1 damage per Destabilization the target has

ACTIVE SKILL - 5
id: wulfricDefendAlly
name: Protect The Weak
corruption cost: 3
element: metal
target kind: Ally
base damage: 0
base crit chance: 0
accuracy: 100%
effects on hit: Apply 3 defense tokens + 1 defense token per 2 Controlled instability.

Tier 3 Skills:
Defense tokens you apply have a 25% chance to not be lost at the end of turn.
Defense tokens you apply also provide a damage buff of +10% per stack
Shield charges also deal +1 damage per Controlled Instability you have


ACTIVE SKILL - 6
id: wulfricFrenzy
name: Shape Shifting Swordsmanship
corruption cost: 10
element: metal
target kind: 1 enemy
base damage: 7 to 9
base crit chance: 2%
accuracy: 60%
effects on hit: attacks 5 times, each successful hit applies 1 Bleeding

TOKEN DICTIONARY
Taunt: Enemies attacks can only target this. 1 stack is lost when hit by an enemy.

Controlled instability: Enemies who hit you will receive 2 damage back per stack.

Destabilization: Trigger when this character dies, exploding its corpse, nearby characters take 3 damage per stack.

Strength: Deals 25% more damage. Lose 1 stack at the end of turn.

Defense: Takes 25% less damage. Lose 1 stack at the end of turn.

Weaken: Deals 50% less damage. Lose 1 stack at the end of turn.

Vulnerability: Takes 50% more damage. Lose 1 stack at the end of turn.

Confusion: Triggered at the start of turn: each skill has 33% chance to swap targets for the rest of this turn: Ally with Enemy, Enemy with Ally and Self with None. Lose 1 stack at the end of turn.

Bleeding: Takes damage equal to 5% maximum health at the end of turn. Lose 1 stack at the end of turn.












Base Stats
Nickname: Pistoleer

Health: 85
Defense: 17%
Critical chance: +5% 

Leader Passive: Critical strike damage increases by 50% for each critical strike dealt
Companion Passive: basic attacks have 50% chance to apply 2 stacks of “Lucky Shot” to self

Corruption Passive:
Tier 1: -5% accuracy, +3% critical chance
Tier 2: -8% accuracy, +5% critical chance
Tier 3: -10% accuracy, +8% critical chance

Base Kit

ACTIVE SKILL - BASIC ATTACK: 
id:buckBasicHit
name: revolver shot
corruption cost: 0
element: fire
target kind: 1 enemy
base damage: 8 to 13
base crit chance: 5%
Accuracy: 55%
effects on hit: none


ACTIVE SKILL - 1:
id: buckPistol
name: Pistol Draw
corruption cost: 2
element: fire
target kind: 3 enemies
base damage: 4 to 8
base crit chance: 7%
Accuracy: 50%
effects on hit: 75% chance to not finish your turn

ACTIVE SKILL - 2:
id: buckRevolver
name: Unload ammo
corruption cost: 3
element: fire
target kind: 1 enemy
base damage: 5 to 10
base crit chance: 6% 
Accuracy: 40%
effects on hit: hits 3 times

ACTIVE SKILL - 3:
id: buckRifle
name: Hunting Rifle
corruption cost: 4
element: Anomaly
target kind: 1 target
base damage: 15 to 25
base crit chance: 5%
Accuracy: 80%
effects on hit: Applies 3 vulnerability tokens




TREE 1: ARACHNID
Tier 1 Passives:
Accuracy +15%
Revolver attacks deal +2 damage
Unload Ammo shoots 1 more bullet

ACTIVE SKILL - 4:
id: buckSpiderHands
name: More hands to aim with
corruption cost: 4
element: anomaly
target kind: self
base damage: 0
base crit chance: 0 
Accuracy: 100%
effects on hit: Applies 3 Strength and 3 Dexterity tokens

Tier 2 Passives:
Receive +1 Strength when you deal damage to an enemy with vulnerability
Receive +1 Dexterity when you deal damage to an enemy with vulnerability
Revolver attacks deal +3 damage

ACTIVE SKILL - 5:
id: buckAllGuns
name: Guns for all
corruption cost: 6
element: anomaly
target kind: 1 enemy 
base damage: 0
base crit chance: 0
Accuracy: 0
effects on hit: Use Skills 1, 2 and 3 with this enemy as the target.

Tier 3 Passives:
Rifle shots have 50% chance to not end your turn
The first time you use your basic attack it has +25% chance to not end your turn
The first time you use any skill it has +25% chance to not end your turn

ACTIVE SKILL - 6:
id: buckJuggle
name: Juggling
corruption cost: 5 
element: anomaly
target kind: self
base damage: 0
base crit chance 0: 
Accuracy: 100%
effects on hit: Accuracy is -10% per enemy. You and your ally can take an extra turn before the enemies







TREE 2: SNAKE
Focus: Increase single instance damage and accuracy with Tokens. High single dmg.

Tier 1 Passives:
Rifle attacks Accuracy +20%
Pistol attacks Accuracy +25%
Rifle attacks deals +5 damage

ACTIVE SKILL - 4:
id: buckSnakeVision
name: Heat Vision
corruption cost: 4 
element: anomaly
target kind: 1 enemy
base damage: 0
base crit chance 0: 
Accuracy: 200%
effects on hit: Applies 3 vulnerability and 3 exposition.

Tier 2 Passives:
Revolver attacks have +15% chance to apply 1 vulnerability
Pistol attacks have +20% chance to apply 2 vulnerability
You have +20% Accuracy against enemies with vulnerability

ACTIVE SKILL - 5:
id: buckSnakeBite
name: Corrosive bite
corruption cost: 3 
element: anomaly
target kind: 1 enemy
base damage: 3 to 8
base crit chance 3% 
Accuracy: 75%
effects on hit: Applies 3 Corrosion


Tier 3 Passives:
Vulnerability you apply is 50% more effective 
Exposition you apply is 50% more effective
Every hit has 10% chance to apply 2 Corrosion

ACTIVE SKILL - 6:
id: buckSnakeTail
name: Strangle
corruption cost: 5
element: anomaly
target kind: 1 enemy
base damage: 0
base crit chance 0 
Accuracy: 0%
effects on hit: Per every different type of debuff token in the target: +10 damage, + 4% crit chance, +30% accuracy








TREE 3: DUELIST
Tier 1 Passives:
Pistol attacks deal +50% more damage
Critical Strikes with pistols have +100% more damage
You have +3% critical chance against enemies with vulnerability

ACTIVE SKILL - 4:
id: buckMark
name: Challenge for Duel
corruption cost: 3
element: fire
target kind: 1 enemy
base damage: 0
base crit chance: 0
Accuracy: 80%
effects on hit: Applies 3 Mark

Tier 2 Passives:
Revolver shots deal +25% damage against marked enemies
Rifle shots deal +75% damage against marked enemies
Every hit against an enemy with vulnerability have a 10% chance to apply 1 mark

ACTIVE SKILL - 5:
id: buckPistolHeadShot
name: Aim for the head
corruption cost: 5
element: fire
target kind: 1 enemy
base damage: 4 to 8
base crit chance: +25%
Accuracy: 40%
effects on hit: attacks 3 times, 90% chance to not finish your turn

Tier 3 Passives:
When an enemy hits you, they have a 25% chance to receive 2 Mark
Your Pistol damage increases +50% per defeated enemy
Marks you apply are 100% more effective

ACTIVE SKILL - 6:
id: buckLuckManipulation
name: Lucky ammunition
corruption cost: 6
element: anomaly
target kind: self
base damage: 0
base crit chance: 0
Accuracy: 100%
effects on hit: Applies 6 stacks of “Lucky Shot”

TOKEN DICTIONARY
Strength: Deals 25% more damage. Lose 1 stack at the end of turn.

Vulnerability: Takes 50% more damage. Lose 1 stack at the end of turn.

Lucky Shot: this character has 4% more critical chance per stack. Lose 1 stack at the end of turn.

Dexterity: This character has +10% accuracy. Lose 1 stack at the end of turn.

Exposition: Skills targeting this character have +20% accuracy. Lose 1 stack at the end of turn.


Corrosion: Every debuff token in this character is +10% more effective per corrosion stack. Take 5 damage and lose 1 stack at the end of turn.

Mark: This character has a +10% chance to receive a critical hit. Critical hits will deal +50% more damage per Mark. Lose 1 stack at the end of turn.


Base Stats
Nickname: The Star

Health: 70
Defense: 12%
Critical chance: +1% 

Leader Passive: When you apply a debuff token, apply +1 more
Companion Passive: When you apply a buff token, apply +1 more

Corruption Passive:
Tier 1: All tokens you apply are 25% more effective, you have a 5% chance to use your basic attack against a target you applied a token to.
Tier 2: All tokens you apply are 50% more effective, you have a 10% chance to use your basic attack against a target you applied a token to.
Tier 3: All tokens you apply are 100% more effective, you have a 15% chance to use your basic attack against a target you applied a token to.

Base Kit

ACTIVE SKILL - BASIC ATTACK
id: mariaBasicHit
name: Sound strike
corruption cost: 0
element: anomaly
target kind: 1 enemy
base damage: 4 to 8
base crit chance: 1%
accuracy: 90%
effects on hit: none

ACTIVE SKILL - 1
id: mariaHealVoice
name: Healing voice
corruption cost: 4
element: anomaly
target kind: self or ally
base damage: 0
base crit chance: 0%
accuracy: 80%
effects on hit: Heal the target 5 to 10
ACTIVE SKILL - 2
id: mariaScreamAttack
name: Amplified Scream
corruption cost: 3
element: anomaly
target kind: all enemies
base damage: 3 to 6
base crit chance: 0%
accuracy: 70%
effects on hit: Apply 2 Vulnerability tokens


ACTIVE SKILL - 3
id: mariaDamageBuff
name: Inspirational song
corruption cost: 4
element: anomaly
target kind: self and ally
base damage: 0
base crit chance: 0%
accuracy: 75%
effects on hit: Apply 3 Strength tokens to the targets 

TREE 1: LIFE SYMPHONY

Tier 1 Passives:
Healing voice is are 50% more effective
Receive 2 Strength tokens after healing
When any of your effects heal a target, there’s a 25% chance you also apply 2 Defense tokens to it

ACTIVE SKILL - 4
id: mariaEchoHeal
name: Echoing regeneration
corruption cost: 6
element: anomaly
target kind: self and ally
base damage: 0
base crit chance: 0%
accuracy: 75%
effects on hit: Heal the target 3 to 6, Apply 3 Regeneration tokens.


Tier 2 Passives:
Applying a buff to a target also heals it by 3
Applying a debuff to a target also heals yourself by 3
At the start of battle apply 3 regeneration tokens to you and your ally

ACTIVE SKILL - 5
id: mariaCleanse
name: Refreshing song
corruption cost: 6
element: anomaly
target kind: self and Ally
base damage: 0
base crit chance: 0
accuracy: 60%
effects on hit: Heal the target 5 to 10, remove all debuffs of the targets.

Tier 3 Skills:
Every hit you deal has 20% chance to heal you for half the amount of damage dealt
If you are below 25% of your health at the start of your turn: apply 5 regeneration tokens to yourself. This can only happen once per battle.
Healing skills have a 10% chance to heal double the amount.

ACTIVE SKILL - 6
id: mariaResurrection
name: Resurrection Hymn
corruption cost: 20
element: anomaly
target kind: self and Ally
base damage: 0
base crit chance: 0
accuracy: 100%
effects on hit: Heal the target 40 to 50, remove all debuffs of the targets, apply 2 Strength and 2 Defense tokens.







	
TREE 2: BATTLE HYMN

Tier 1 Passives:
The inspirational song has 10% more accuracy
There’s a 25% chance your team heals 3 each when any of you hit an enemy while having an active buff.
When you apply any token to an ally, apply +1 strength token.


ACTIVE SKILL - 4
id: mariaChanceBuff
name: Higher Pitch
corruption cost: 4
element: anomaly
target kind: self or ally
base damage: 0
base crit chance: 0%
accuracy: 85%
effects on hit: Apply 3 Dexterity tokens, apply 2 Lucky shot tokens


Tier 2 Passives:
When you heal an ally it receives +1 Strength
When you apply any token to an ally, there’s a 70% chance to apply +1 defense token.
Your scream has 20% more accuracy


ACTIVE SKILL - 5
id: mariaDefenseBuff
name: Protection song
corruption cost: 5
element: anomaly
target kind: self and Ally
base damage: 0
base crit chance: 0
accuracy: 75%
effects on hit: Apply 4 Defense tokens to the targets





Tier 3 Skills:
You apply to self and ally +1 Dexterity on enemy hit
Your team takes 10% less damage while buffed.
Your team’s critical strikes deal triple damage instead of double if the attacker is buffed while attacking.

ACTIVE SKILL - 6
id: mariaShow
name: Dance of the Revolution
corruption cost: 9
element: anomaly
target kind: self and Ally
base damage: 0
base crit chance: 0
accuracy: 85%
effects on hit: apply 3 Strength tokens, 3 defense tokens, 3 dexterity tokens.

TREE 3: DEAFENING SCREAM

Tier 1 Passives:
When you apply a debuff deal damage to the target equal to the number of debuffs applied
Hitting a debuffed enemy has a 50% chance to heal you by 6
You take 10% less damage from debuffed enemies

ACTIVE SKILL - 4
id: mariaScreechNoise
name: Screech Noise
corruption cost: 4
element: anomaly
target kind: 1 enemy
base damage: 10 to 12
base crit chance: 0%
accuracy: 80%
effects on hit: Apply 2 Clumsy tokens

Tier 2 Passives:
Your basic hit has 40% chance to apply 2 Weaken
Upon using a healing skill there is 40% chance to apply 1 weaken to all enemies
Upon using a buff skill there is 40% chance to apply 1 vulnerability to all enemies

ACTIVE SKILL - 5
id: mariaPiercingYell
name: Piercing Yell
corruption cost: 6
element: anomaly
target kind: all enemies
base damage: 7 to 9
base crit chance: 0
accuracy: 70%
effects on hit: Apply 3 Clumsy tokens

Tier 3 Skills:
When your team gets hit, apply 1 weaken to the attacker
All your debuff tokens are *50% more effective
When your team gets hit, apply 1 vulnerability to the attacker


ACTIVE SKILL - 6
id: mariaChaosMelody
name: Chaos Melody
corruption cost: 8
element: anomaly
target kind: all enemies
base damage: 2 to 6
base crit chance: 0
accuracy: 70%
effects on hit: apply 2 confusion














TOKEN DICTIONARY
Strength: Deals 25% more damage. Lose 1 stack at the end of turn.

Defense: Takes 25% less damage. Lose 1 stack at the end of turn.

Weaken: Deals 50% less damage. Lose 1 stack at the end of turn.

Vulnerability: Takes 50% more damage. Lose 1 stack at the end of turn.

Regeneration: Receive 1 health per token stack at the end of turn. Lose 1 stack at the end of turn.

Lucky Shot: this character has 4% more critical chance per stack. Lose 1 stack at the end of turn.

Dexterity: This character has +10% accuracy. Lose 1 stack at the end of turn.

Clumsy: This character has -20% accuracy. Lose 1 stack at the end of turn.

Confusion: Triggered at the start of turn: each skill has 33% chance to swap targets for the rest of this turn: Ally with Enemy, Enemy with Ally and Self with None. Lose 1 stack at the end of turn.



Lista de Tokens
Tokens servem para demarcar quando um personagem está sofrendo influência de um tipo específico de mecânica, como “damage over time”, buffs e debuffs temporários ou não e quaisquer efeitos relativos aos mesmos, influenciados primariamente pela quantidade de tokens presentes no personagem. Para o Player vamos usar a palavra “Status” ao invés de “Token”

Buffs e Debuffs básicos (afetam apenas dano ou pontaria):

Strength: Deals 25% more damage. Lose 1 stack at the end of turn.

Defense: Takes 25% less damage. Lose 1 stack at the end of turn.

Weaken: Deals 50% less damage. Lose 1 stack at the end of turn.

Vulnerability: Takes 50% more damage. Lose 1 stack at the end of turn.

Dexterity: This character has +10% accuracy. Lose 1 stack at the end of turn.

Clumsy: This character has -20% accuracy. Lose 1 stack at the end of turn.

// Outros efeitos que forma pensado mas não usados pois seriam idênticos com Clumsy: Blind e Fear. - Uma interessante adição que pode ser feita futuramente: adicionar esses efeitos como debuffs mistos, afetando dano E pontaria. 

Exposition: Skills targeting this character have +20% accuracy. Lose 1 stack at the end of turn.

Stealth: Skills targeting this character have -40% accuracy. Lose 1 stack at the end of turn

OBSERVAÇÃO: Criar uma versão Perma[BuffName] para os buffs acima, com 25% da eficiência (exemplo: 50% = 12,5%) mas tokens não são perdidos e o efeito aumento por cada acúmulo de token. Lembrando: o mínimo de dano é sempre 1 e o de acerto é sempre 5%.

Efeitos de Controle:

Taunt: Enemies attacks can only select this. 1 stack is lost when hit by an enemy.

Confusion: Triggered at the start of turn: each skill has 33% chance to swap targets for the rest of this turn: Ally with Enemy, Enemy with Ally and Self with None. Lose 1 stack at the end of turn.

Hypnosis: This character can only use the same skill it used before this effect was applied to it. Skill: [SkillName]. Lose 1 stack at the end of turn.

Dizzy: This character has a 50% chance to change which target / targets, the skill will hit upon selecting a target / targets. Lose 1 stack at the end of turn.

Damage Over Time:
Burn: Takes damage equal to stacks at the end of turn. Lose 1 stack at the end of turn.

Bleeding: Takes damage equal to 5% maximum health at the end of turn. Lose 1 stack at the end of turn.

Tokens Condicionais:
Controlled instability: Enemies who hit you will receive 2 damage back per stack.

Destabilization: Trigger when this character dies, exploding its corpse, nearby characters take 3 damage per stack.

Buffs e Debuffs avançados:

Lucky Shot: this character has 4% more critical chance per stack. Lose 1 stack at the end of turn.

Corrosion: Every other debuff type in this character is +10% more effective per corrosion stack. Take 5 damage and lose 1 stack at the end of turn.

Mark: This character has a +10% chance to receive a critical hit. Critical hits will deal +50% more damage per Mark. Lose 1 stack at the end of turn.

Regeneration: Receive 1 health per token stack at the end of turn. Lose 1 stack at the end of turn.





Lista de Efeitos
Efeitos podem ocorrem ao acertar skills, ou quando certas condições passivas estiverem presentes ou seus requisitos se cumprirem.

Character Stat Change: altera estatísticas do personagem (vida atual, vida total, defesa, dano extra, pontaria extra, chance de crítico extra, dano crítico extra). Pode ser própria, do aliado ou ambos.

Skill Stat Change: altera estatísticas de uma skill (dano, pontaria, custo, alvo, efeitos, chance de crítico).

Token Stat Change: altera estatísticas de tokens aplicados por esse personagem e / ou altera estatísticas de tokens aplicados a este personagem (eficiência, efeito adicional).

Extra stats from resources: estatísticas alteradas conforme recursos específicos (menos vida dá mais dano).

Token Manipulation: permite a aplicação ou remoção de tokens 

Turn Manipulation: permite que o turno não seja considerado como finalizado após o uso de uma skill.

Lista de Regras Passivas
Condições a serem criadas para que os efeitos passivos planejados possam existir no projeto.

Ativações:
Permanent: o efeito está sempre ativo.

While having status: o efeito está sempre ativo enquanto o personagem possuir um status genérico ou específico.

Activated Upon Damage Take: o efeito ocorre quando o personagem recebe dano, pode ser em quantidade específica (for every X HP lost), pode ser que leve em consideração o inimigo possuir um status específico.

Activated Upon Kill: o efeito ocorre quando um inimigo morre

Activated Upon Critical Strike: o efeito ocorre quando se realiza um golpe crítico.

Activated Upon Applying Buff / Debuff / Status: o efeito ocorre quando se aplica um buff, debuff ou status, específico ou genérico; pode ser em quantidade específica.

Upon reaching stat threshold: o efeito ocorre quando um recurso específico chega a uma certa quantidade. (HP below, or above certain percentage; After getting X status buffs receive +5 Strength)

On turn End: o efeito ocorre no fim de todo turno desse personagem.

Upon hitting target with specific Status: o efeito ocorre quando o alvo possui algum token específico ou genérico.

Activated Upon Receiving Buff / Debuff / Status: o efeito ocorre quando se recebe um buff, debuff ou status, específico ou genérico; pode ser em quantidade específica.

Activated upon dealing damage / healing: o efeito ocorre ao curar / causar dano.

OBSERVAÇÃO: todos efeitos que ocorrem quando a condição é cumprida devem possuir um booleano para indicar se eles podem ocorrem indefinidamente ou um número limitado de vezes e um float para indicar chance de ocorrer, quando não for 100%. Passivas podem conter múltiplos efeitos e condições!


Fluxo de Combate
Funcionalidade atual:
Ao iniciar um combate 1 valor de 0 a 10 é selecionado aleatoriamente por cada personagem vivo do jogador e os valores são somados para determinar a velocidade do jogador.
1 valor de 1 a 5 é selecionado aleatoriamente por cada personagem vivo do time inimigo e os valores são somados para determinar a velocidade dos inimigos.
Se a velocidade dos inimigos for maior ou igual à do jogador, os inimigos agem primeiro, depois o jogador. Se a velocidade dos inimigos for menor então o jogador age primeiro e depois os inimigos.
Round do jogador: o personagem Líder (mais a frente) age primeiro, depois o companheiro age. Round dos inimigos: agem em ordem um por um, do mais à esquerda para o mais à direita da tela.

Em cada Round, cada personagem pode e deve executar apenas uma ação, a menos que um efeito o permita agir novamente. O seu turno é automaticamente finalizado após o uso de uma habilidade, e então é iniciado o turno do próximo personagem. 

Para o jogador isso é feito selecionando skills com mouse ou os números no teclado e então selecionando um alvo válido com o mouse. Ao dar hover em uma skill uma tooltip aparece com informações sobre ela.

Mudanças a serem realizadas:
Efeitos que permitam agir múltiplas vezes por turno ainda não foram devidamente implementados.
Migrar todas informações de skills em tooltips para tokens.
Adicionar indicação de quando o jogador começa, quando o inimigo começa e indicação de quando o round do jogador começa, quando o round do inimigo começa.

Corrupção
Funcionalidade atual:
Formas de acúmulo de corrupção:
Permanecer tempo fora da área segura / vila.
Uso de skills ativas que aumentam o valor de corrupção

Divisão dos Tiers de corrupção:
0 - 33 = Tier 0
33 - 66 = Tier 1
66 - 99 = Tier 2
100 - 199 = Tier 3
200+ = Tier 4

Tiers 1 - 4:
Loots serão melhores
Todos os personagens ficam mais poderosos.
No tier 4 os inimigos passam a causar *1.35 em golpes críticos // não conferi se eles usam o valor de crítico vezes o bônus.

Buffs de estatísticas de acordo com o nível de corrupção:
Dano causado pelo jogador
Dano recebido pelo jogador
Chance de crítico do jogador
Chance de crítico do inimigo

Apresentação do sistema:
Slider com valor de 0 a 250. A versão da barra em combate possui ícones de “perigo” ao longo da barra.

Mudanças a serem realizadas:
Tiers 1 - 3:
Remover modificador de dano causado pelo jogador
Remover modificador de dano recebido pelo jogador
Protagonistas têm uma passiva que se ativa no Tier 1 e fica mais forte até alcançar o Tier 3. Elas continuaram com a mesma força no Tier 4.
Adicionar modificador que aumenta a pontaria dos monstros a cada Tier.

Tier 4:
Remover buffs do jogador; Apenas os inimigos ficam mais fortes. Esse buff deve ser consideravelmente maior que os buffs dos tiers anteriores


Apresentação do sistema:
Slider com valor de 0 a 100; o limite real de corrupção deve ser qualquer número acima de 200 para manter a funcionalidade, mas o valor apresentado deve ser limitado para criar uma atmosfera de falsa segurança, uma vez que em plano de fundo o mundo continua ficando mais perigoso.
Ao invés de colocar os ícones na barra adicionar um ícone ao lado da barra que se altera conforme o Tier aumenta. Dar hover sobre este ícone deve informar ao jogador o Tier atual de corrupção e efeitos do mesmo.

OBSERVAÇÕES: conteúdo que foi cortado desse sistema por conta de escopo, mas pode ser reconsiderado ou de qualquer modo ao menos documentado para uso futuro:
IA avançada de inimigos, com golpes mais fortes sendo usado mais constantemente conforme a corrupção cresce.
Sistema de spawn de inimigos com combates pré-determinados, sendo que os mais difíceis aparecem apenas em Tiers maiores de corrupção.

Loot
Funcionalidade atual:
Após vencer em um combate, o jogador recebe loot de acordo com o Tier de corrupção.

Mudanças a serem realizadas:
Não uma mudança em si, mas apenas verificar se o loot está sendo gerado conforme o Tier de corrupção ao entrar ou ao sair do combate. Deve ser ao sair do combate.

Skills
Funcionalidade atual:
Você pode selecionar skills ativas com o mouse ou apertando os números no teclado, e então selecionar um inimigo para atacar.

Mudanças a serem realizadas:
Deve haver feedback da skill atualmente selecionada
Self Skills devem ser ativadas ao selecionar a si mesmo, não os inimigos.
Mudanças em código para funcionalidade correta das skills ativas e skills passivas dos protagonistas, incluindo a preparação de quaisquer efeitos on hit pertinentes.
> lembrete: permitir skills que acertem: 3 enemies, all enemies, self and ally, self or ally.

Tokens
Funcionalidade atual:
Skills diversas podem aplicar diversos tipos de tokens com efeitos diversos. No momento existe uma separação entre Tokens e DOTs. Além disso, todos DOTs possuem a mesma funcionalidade genérica.

Mudanças a serem realizadas:
Refatorar o sistema para fácil adição e configuração de tokens. 
Alterar para que todos efeitos sejam tokens, sendo quaisquer classes DOT auxiliares do sistema de tokens.
Corrigir os tooltips de tokens: rich text.

Element
Funcionalidade atual:
Todos ataques e inimigos possuem elementos. Os elementos formam um triângulo, no qual cada elemento é neutro para si e causa *1.5 em vantagem e *0.5 quando em desvantagem.
Fogo > Metal > Anomalia > Fogo

Mudanças a serem realizadas:
Deve haver um feedback da existência do sistema de elementos, demonstrando que um ataque possui vantagem ou desvantagem contra o alvo selecionado.

Passiva de posição
Funcionalidade atual:
Atualmente não há essa funcionalidade no projeto. 

Mudanças a serem realizadas:
Protagonistas têm uma passiva que se ativa no Tier 1 e fica mais forte até alcançar o Tier 3. Elas continuaram com a mesma força no Tier 4.

Observação: caso essa mecânica tenha de ser cortada, o sistema de corrupção deve bufar as estatísticas do jogador em compensação. 

Almanaque de inimigos
Funcionalidade atual:
Atualmente não há essa funcionalidade no projeto. 
Mudanças a serem realizadas:
Permitir que o jogador visualize informações sobre o inimigo:
Skills ativas (descrição completa)
Skills passivas
Elemento

Extra: Se possível fazer com que a visualização das skills ativas ocorra apenas quando ela tenha sido usada ao menos uma vez por aquele tipo de inimigo.

OBSERVAÇÃO FINAL:
Garantir que todos sistemas de combate sejam independentes dos sistemas de exploração.



Notas de implementação

**Legenda de intensidade:** `+` leve · `++` médio · `+++` forte · `++++` extremo (só em versões Extreme)
Mesma lógica para valores negativos: `-`, `--`, `---`, `----`

**Legenda de tipo:** `Absolute` = altera o status em valor fixo · `Relative` = altera o status em % (n/100)

**Sistema de Raridade** (afeta apenas a cor de fundo do ícone — o ícone em si é o mesmo por item, independente da raridade):

| Tier | Raridade | Cor sugerida |
|---|---|---|
| Small | Common | Cinza |
| Medium | Rare | Azul |
| Big | Epic | Roxo |
| Extreme / Reset de Skill Tree | Legendary | Dourado/Laranja |

> como os itens temáticos possuem efeitos diversos e mais potentes, eles pulam a raridade **Common** — começam direto de **Rare (Small)** indo para **Epic (Big)** e **Legendary (Extreme)**. Já os itens de status individual mantêm as 3 versões (Small/Medium/Big) e passam pelas 3 raridades normalmente (Common/Rare/Epic) — não possuem versão Legendary.
> Cada **item** (não cada tier) precisa de **um ícone único**; a raridade só altera o fundo/moldura desse mesmo ícone.

---

Itens de Status Individual

Itens que alteram **apenas um** status, disponíveis em três tamanhos (Small/Medium/Big) e duas categorias (Absolute/Relative).

### Health

| Item | Tipo | Efeito | Raridade |
|---|---|---|---|
| Health Potion (Small) | Absolute | Health + | Common |
| Health Potion (Medium) | Absolute | Health ++ | Rare |
| Health Potion (Big) | Absolute | Health +++ | Epic |
| Vitality Charm (Small) | Relative | Health + | Common |
| Vitality Charm (Medium) | Relative | Health ++ | Rare |
| Vitality Charm (Big) | Relative | Health +++ | Epic |

### Defense

| Item | Tipo | Efeito | Raridade |
|---|---|---|---|
| Iron Plate (Small) | Absolute | Defense + | Common |
| Iron Plate (Medium) | Absolute | Defense ++ | Rare |
| Iron Plate (Big) | Absolute | Defense +++ | Epic |
| Ward Sigil (Small) | Relative | Defense + | Common |
| Ward Sigil (Medium) | Relative | Defense ++ | Rare |
| Ward Sigil (Big) | Relative | Defense +++ | Epic |

### Critical Chance

| Item | Tipo | Efeito | Raridade |
|---|---|---|---|
| Sharp Edge (Small) | Absolute | Critical + | Common |
| Sharp Edge (Medium) | Absolute | Critical ++ | Rare |
| Sharp Edge (Big) | Absolute | Critical +++ | Epic |
| Focus Lens (Small) | Relative | Critical + | Common |
| Focus Lens (Medium) | Relative | Critical ++ | Rare |
| Focus Lens (Big) | Relative | Critical +++ | Epic |

---

Itens Temáticos (Multi-Status)

Itens que alteram **dois ou mais** status simultaneamente, cada um com identidade temática própria. Três versões: Small, Big e Extreme (única).

### Paladino
*Tema: item premium, sem trade-off, todos os status sobem*

| Versão | Health | Defense | Critical | Raridade |
|---|---|---|---|---|
| Small | + | + | + | Rare |
| Big | ++ | ++ | ++ | Epic |
| **Extreme** | +++ | +++ | +++ | Legendary |

### Vampírico
*Tema: rouba vida, agressivo, abre mão de proteção*

| Versão | Health | Defense | Critical | Raridade |
|---|---|---|---|---|
| Small | ++ | - | ++ | Rare |
| Big | +++ | -- | +++ | Epic |
| **Extreme** | ++++ | --- | ++++ | Legendary |

### Escudo Vivo
*Tema: extremo tanque, quase não crita*

| Versão | Health | Defense | Critical | Raridade |
|---|---|---|---|---|
| Small | ++ | ++ | --- | Rare |
| Big | +++ | +++ | ---- | Epic |
| **Extreme** | ++++ | ++++ | ---- | Legendary |

### Sangue Frio
*Tema: assassino, abre mão de blindagem por precisão letal (2 status)*

| Versão | Defense | Critical | Raridade |
|---|---|---|---|
| Small | - | ++ | Rare |
| Big | -- | +++ | Epic |
| **Extreme** | --- | ++++ | Legendary |

### Berserker
*Tema: fúria cega — machuca, mas acerta forte e aguenta um pouco mais*

| Versão | Health | Defense | Critical | Raridade |
|---|---|---|---|---|
| Small | -- | ++ | +++ | Rare |
| Big | --- | +++ | ++++ | Epic |
| **Extreme** | ---- | ++++ | ++++ | Legendary |

---

Itens de Utilidade
Itens de consumo único que não afetam status de combate diretamente, mas permite redistribuir pontos de habilidade.

| Item | Efeito | Raridade |
|---|---|---|
| Reset de Skill Tree Geral | Reseta todos os pontos investidos na árvore de habilidades geral, permitindo redistribuição total | Legendary |
| Reset de Skill Tree — Buck Wyatt | Reseta os pontos investidos na árvore de habilidades específica do personagem Buck Wyatt | Epic |
| Reset de Skill Tree — Wulfric | Reseta os pontos investidos na árvore de habilidades específica do personagem Wulfric | Epic |
| Reset de Skill Tree — Matsuda | Reseta os pontos investidos na árvore de habilidades específica do personagem Matsuda | Epic |

---

## Resumo de Contagem

- **Itens de status individual:** 18 (6 por status × 3 status)
- **Itens temáticos multi-status:** 15 (5 temas × 3 versões: Small/Big/Extreme)
- **Itens de utilidade:** 4 (resets de skill tree)
- **Total:** 37 itens

### Distribuição por Raridade

| Raridade | Itens de Status Individual | Itens Temáticos | Itens de Utilidade | Total |
|---|---|---|---|---|
| Common | 6 | 0 | 0 | 6 |
| Rare | 6 | 5 | 0 | 11 |
| Epic | 6 | 5 | 3 | 14 |
| Legendary | 0 | 5 | 1 | 6 | 


Sinopse
Ankoku Matsuda é um cientista renomado responsável pela descoberta da chamada energia anômala no final dos anos 1990. A descoberta ocorreu durante uma série de experimentos envolvendo física quântica e manipulação de partículas em condições extremas.

A energia anômala mostrou-se uma promessa revolucionária para o futuro da exploração espacial, pois possuía a capacidade de rasgar o tecido do espaço-tempo e da própria realidade, abrindo possibilidades para viagens interestelares e manipulação dimensional. No entanto, a substância era extremamente instável e altamente danosa a organismos vivos.

Com o passar dos anos, a obsessão de Matsuda em compreender e controlar essa energia cresceu. Em um ato controverso e desesperado, ele realizou experimentos em sua própria filha ainda no estágio embrionário, expondo o embrião a pequenas doses de energia anômala. O experimento aparentemente não causou efeitos imediatos na criança, mas acabou resultando na morte de sua esposa durante o processo.

Em 2026, durante uma tentativa de estabilizar e controlar a chamada matéria anômala, algo deu terrivelmente errado. O experimento culminou em um evento catastrófico que ficou conhecido como: “A Erupção”, um fenômeno cuja natureza e consequências ainda não são totalmente compreendidas.

Conceitos
Gênero do jogo: rpg de turno

Público-alvo: Jovem adulto (18-30 anos), entusiasta de RPG, com forte interesse em narrativas imersivas, construção de mundo e elementos de mistério. Valoriza experiências interativas profundas, storytelling de qualidade e exploração de universos ficcionais complexos. Busca envolvimento emocional e intelectual por meio de tramas intrigantes e sistemas ricos de jogo.

Loop básico: O jogador parte de uma área central segura, de onde se prepara para expedições. A partir desse ponto, explora regiões externas em busca de recursos, enfrenta inimigos e coletar itens. Durante essas incursões, progride em poder por meio de combate e aquisição de loot.



Após a exploração, retorna à área central para processar e revitalizar os itens obtidos, convertendo-os em melhorias permanentes ou recursos úteis. Esse ciclo de exploração, combate e aprimoramento culmina no enfrentamento de chefes de área, desbloqueando progressão até o confronto final na região central.

Game design básico: centrado na criação de uma experiência envolvente, baseada em sistemas de risco e recompensa que incentivam a tomada constante de decisões estratégicas. A exploração é marcada por uma progressão de tensão e perigo, mantendo o jogador em estado de alerta à medida que avança. Em contrapartida, os desafios mais arriscados oferecem recompensas mais valiosas, reforçando o ciclo de engajamento e satisfação.

