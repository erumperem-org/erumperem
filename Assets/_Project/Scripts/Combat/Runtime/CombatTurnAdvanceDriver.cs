using System;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;

namespace Erumperem.Combat.Runtime
{
    /// <summary>
    /// Drives initiative order and auto-resolves non-player turns until player input or presentation is required.
    /// </summary>
    public sealed class CombatTurnAdvanceDriver
    {
        public void BeginRound(CombatSessionRuntime session)
        {
            session.State.TurnNumber++;
            session.RoundOrder.Clear();
            session.RoundOrder.AddRange(session.Simulator.BuildInitiativeOrder(session.State));
            session.ActorIndex = 0;
            session.PreparedThisStep = false;
            session.AnnouncedRoundSide = null;
        }

        public bool TryAdvanceCombatStep(CombatSessionRuntime session, CombatTurnAdvanceCallbacks callbacks)
        {
            if (session.PresentationBusy)
            {
                return false;
            }

            while (session.ActorIndex >= session.RoundOrder.Count)
            {
                BeginRound(session);
            }

            var actor = session.RoundOrder[session.ActorIndex];
            if (actor.Health.IsDead)
            {
                session.ActorIndex++;
                session.PreparedThisStep = false;
                return true;
            }

            AnnounceRoundSideIfNeeded(session, callbacks, actor.Position.Side);

            if (!session.PreparedThisStep)
            {
                var turnEventStartIndex = session.EventCollector.Events.Count;
                if (!session.Simulator.TryPrepareActorTurn(session.State, actor))
                {
                    session.ActorIndex++;
                    session.PreparedThisStep = false;
                    return true;
                }

                callbacks.ProcessTurnStartCombatEvents(turnEventStartIndex);
                session.PreparedThisStep = true;
                callbacks.SessionHub?.RaiseTurnStarted();
            }

            if (IsPlayerControlled(actor))
            {
                session.NeedsPlayerInput = true;
                session.PendingPlayerActor = actor;
                callbacks.SessionHub?.RaisePlayerCommandRequired(actor);
                callbacks.PublishPlayerSkillHelp(actor, callbacks.FindAllyIndex(actor));
                return false;
            }

            var chosenAiAction = session.Simulator.ChooseAiAction(session.State, actor);
            if (chosenAiAction != null)
            {
                session.PresentationBusy = true;
                callbacks.PresentChosenAction(
                    chosenAiAction,
                    () =>
                    {
                        if (BattleSimulator.ShouldActorRetainTurn(actor))
                        {
                            actor.PassiveRuntime.ShouldRetainTurnForBonusAction = false;
                            actor.Tokens.ConsumeOne(TokenType.BonusAction);
                            session.PreparedThisStep = true;
                            return;
                        }

                        session.ActorIndex++;
                        session.PreparedThisStep = false;
                    });
                return false;
            }

            session.ActorIndex++;
            session.PreparedThisStep = false;
            callbacks.SessionHub?.RaiseTurnEnded();
            return true;
        }

        public static bool IsPlayerControlled(Combatant actor) =>
            actor.AI == null && actor.Identity.Faction == Faction.Player;

        private static void AnnounceRoundSideIfNeeded(
            CombatSessionRuntime session,
            CombatTurnAdvanceCallbacks callbacks,
            Side actingSide)
        {
            if (session.AnnouncedRoundSide == actingSide)
            {
                return;
            }

            session.AnnouncedRoundSide = actingSide;
            callbacks.SessionHub?.RaiseCombatRoundSideBegan(actingSide);
        }
    }

    public sealed class CombatTurnAdvanceCallbacks
    {
        public CombatSessionHub SessionHub { get; set; }
        public Action<int> ProcessTurnStartCombatEvents { get; set; }
        public Action<ChosenAction, Action> PresentChosenAction { get; set; }
        public Func<Combatant, int> FindAllyIndex { get; set; }
        public Action<Combatant, int> PublishPlayerSkillHelp { get; set; }
    }
}
