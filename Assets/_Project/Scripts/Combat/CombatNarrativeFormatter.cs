using System;
using System.Collections.Generic;
using Game.Core.Analytics;
using Game.Core.Domain;
using Game.Core.Models;

namespace Erumperem.Combat
{
    /// <summary>
    /// Converte um bloco de eventos de uma única <see cref="BattleSimulator.ResolveChosenAction"/> em linhas de log em PT.
    /// </summary>
    public static class CombatNarrativeFormatter
    {
        public static IEnumerable<string> BuildLines(BattleState state, ChosenAction action, IReadOnlyList<CombatEvent> slice)
        {
            if (slice.Count == 0)
            {
                yield break;
            }

            CombatEvent? hitEvent = null;
            CombatEvent? damageEvent = null;
            foreach (var combatEvent in slice)
            {
                switch (combatEvent.EventType)
                {
                    case BattleEventType.HitResolved:
                        hitEvent = combatEvent;
                        break;
                    case BattleEventType.DamageApplied:
                        damageEvent = combatEvent;
                        break;
                }
            }

            var actorName = DisplayName(state, action.Actor.Identity.Id);
            var targetName = DisplayName(state, action.Target.Identity.Id);
            var skillName = action.Skill.Name;

            if (hitEvent == null)
            {
                yield return $"{actorName} usou {skillName} em {targetName}.";
            }
            else if (!hitEvent.IsHit)
            {
                yield return $"{actorName} usou {skillName} em {targetName}, mas falhou o golpe.";
            }
            else if (damageEvent != null && damageEvent.DamageAmount > 0)
            {
                var dmg = damageEvent.DamageAmount;
                var crit = damageEvent.IsCrit ? " (crítico!)" : string.Empty;
                yield return $"{actorName} usou {skillName} em {targetName}, causando {dmg} de dano{crit}.";
            }
            else
            {
                yield return $"{actorName} usou {skillName} em {targetName}.";
            }

            foreach (var combatEvent in slice)
            {
                if (combatEvent.EventType != BattleEventType.TokenApplied)
                {
                    continue;
                }

                foreach (var line in FormatTokenLine(state, combatEvent))
                {
                    yield return line;
                }
            }

            foreach (var combatEvent in slice)
            {
                if (combatEvent.EventType != BattleEventType.CombatantDied)
                {
                    continue;
                }

                var who = DisplayName(state, combatEvent.TargetId);
                yield return $"{who} foi derrotado.";
            }
        }

        private static IEnumerable<string> FormatTokenLine(BattleState state, CombatEvent combatEvent)
        {
            var targetName = DisplayName(state, combatEvent.TargetId);
            var token = combatEvent.TokenType ?? string.Empty;
            if (string.Equals(token, nameof(TokenType.Stun), StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{targetName} ficou atordoado.";
                yield break;
            }

            if (string.Equals(token, nameof(TokenType.Blind), StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{targetName} ficou cego.";
                yield break;
            }

            if (string.Equals(token, nameof(TokenType.Dodge), StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{targetName} ganhou esquiva.";
                yield break;
            }

            if (string.Equals(token, nameof(TokenType.Taunt), StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{targetName} provocou os inimigos.";
                yield break;
            }

            if (string.Equals(token, nameof(TokenType.Combo), StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{targetName} acumulou combo (+{combatEvent.TokenDelta}).";
                yield break;
            }

            if (string.Equals(token, nameof(TokenType.Block), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, nameof(TokenType.BlockPlus), StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{targetName} recebeu bloqueio (+{combatEvent.TokenDelta}).";
                yield break;
            }

            yield return $"{targetName} recebeu efeito {token} (+{combatEvent.TokenDelta}).";
        }

        private static string DisplayName(BattleState state, string combatantId)
        {
            var combatant = FindCombatant(state, combatantId);
            return combatant?.Identity.DisplayName ?? combatantId;
        }

        private static Combatant FindCombatant(BattleState state, string id)
        {
            foreach (var ally in state.Allies)
            {
                if (ally.Identity.Id == id)
                {
                    return ally;
                }
            }

            foreach (var enemy in state.Enemies)
            {
                if (enemy.Identity.Id == id)
                {
                    return enemy;
                }
            }

            return null;
        }
    }
}
