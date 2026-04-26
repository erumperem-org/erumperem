
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Services.DebugUtilities.Console;
using Services.DebugUtilities.Canvas;
using Core.Tokens;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Tokens
{
    /// <summary>
    /// Controller responsible for managing token allocation, removal,
    /// stacking behavior, and synergies within a container.
    /// Acts as the mediator between Model and View in an MVC pattern.
    /// </summary>
    public class TokenContainerController : MonoBehaviour
    {
        public TokenContainerModel model;
        public TokenContainerView view;

        /// <summary>
        /// Attempts to add a token to the container by:
        /// 1. Checking the token's allocation style prerequisites
        /// 2. Checking immunity restrictions
        /// 3. Applying stacking rules
        /// 4. Applying synergies (only if the token is actually added)
        /// </summary>
        public static async Task AddTokenToContainer(TokenAllocationContext context)
        {
            if (!IsAllocationStyleSatisfied(context)) return;
            if (IsBlockedByImmunity(context)) return;

            if (await ApplyStacking(context))
            {
                ApplySynergies(context);
            }
        }

        /// <summary>
        /// Validates the token's allocation style prerequisites before proceeding.
        /// Returns false if the style's condition is not met, blocking allocation.
        /// </summary>
        private static bool IsAllocationStyleSatisfied(TokenAllocationContext context)
        {
            switch (context.token.data.TokenAllocationStyle)
            {
                case IOnConditionMetTokenAllocation conditionStyle:
                    if (conditionStyle.AllocationCondition != null && !conditionStyle.AllocationCondition.Invoke())
                    {
                        CanvasLoggerService.PrintLogMessage(LogLevel.Debug,
                            new HashSet<LogCategory> { LogCategory.Combat },
                            $"Allocation blocked [{context.token.data.tokenDisplayName}] — condition not met.");
                        return false;
                    }
                    return true;

                case IOnHitTokenAllocation:
                case IOnEventTokenAllocation:
                default:
                    return true;
            }
        }

        /// <summary>
        /// Applies stacking logic for the incoming token.
        /// Returns TRUE if the token should be added to the container (and view).
        /// Returns FALSE if the effect was absorbed by stacking behavior (no new visual).
        /// </summary>
        private static async Task<bool> ApplyStacking(TokenAllocationContext context)
        {
            var token = context.token;
            var container = context.TokenContainerController;
            var existing = FindSameTokenType(context);

            switch (token.data.tokenStackingdata)
            {
                case RefreshDurationStackData data:
                    if (existing != null &&
                        existing.data.tokenStackingdata is RefreshDurationStackData refreshData)
                    {
                        refreshData.currentDuration = refreshData.maxDuration;
                        return false; // No new visual needed — duration refreshed in-place
                    }
                    break;

                case LinearStackData data:
                    if (existing != null &&
                        existing.data.tokenStackingdata is LinearStackData linearData)
                    {
                        linearData.stacks++;
                        return false; // No new visual — stack count updated on existing token
                    }
                    break;

                case GlobalRefreshStackData data:
                    if (existing != null &&
                        existing.data.tokenStackingdata is GlobalRefreshStackData globalData)
                    {
                        globalData.stacks++;

                        ModifyTokens(context.TokenContainerController,
                            t => t.GetType() == token.GetType(),
                            t =>
                            {
                                var stack = (GlobalRefreshStackData)t.data.tokenStackingdata;
                                stack.duration = globalData.duration;
                            });

                        return false; // No new visual — global refresh applied
                    }
                    break;

                case IndependentStackData:
                    // Always adds a new independent token — falls through to spawn
                    break;

                case DiminishingStackData data:
                    if (existing != null &&
                        existing.data.tokenStackingdata is DiminishingStackData diminishingData)
                    {
                        diminishingData.stacks++;
                        return false; // No new visual — stack updated on existing token
                    }
                    break;
            }

            CanvasLoggerService.PrintLogMessage(LogLevel.Debug,
                new HashSet<LogCategory> { LogCategory.Combat },
                $"Allocating Token [{context.token.data.tokenDisplayName}] | " +
                $"Owner: [{context.ownerName}] | " +
                $"Target Container: [{context.TokenContainerController.name}]");

            container.model.tokens.Add(token);
            await container.view.AddTokenToView(token);
            return true;
        }

        /// <summary>
        /// Removes a specific token instance from the container.
        /// Removes from model first, then removes its visual representation.
        /// </summary>
        public static void RemoveTokenFromContainer(TokenContainerController container, TokenController controller)
        {
            if (!container.model.tokens.Contains(controller)) return;

            UnApplySynergies(controller);
            container.model.tokens.Remove(controller);
            container.view.RemoveToken(controller);    // FIX: view removal now happens AFTER model removal,
                                                       // ensuring view always reflects actual model state.
        }

        /// <summary>
        /// Checks whether the token is blocked by any immunity rule.
        /// </summary>
        private static bool IsBlockedByImmunity(TokenAllocationContext context)
        {
            if (context.token is not IImmunitySynergy immunity) return false;

            var ctx = immunity.BuildContext(context);
            bool blocked = immunity.CheckImmunity(ctx);

            if (blocked)
            {
                CanvasLoggerService.PrintLogMessage(LogLevel.Debug,
                    new HashSet<LogCategory> { LogCategory.Combat },
                    $"Immunity Synergy blocked [{context.token.data.tokenDisplayName}] " +
                    $"on Container [{context.TokenContainerController.name}]");
            }

            return blocked;
        }

        /// <summary>
        /// Applies all compatible synergies for the given token.
        /// Uses a generic dispatch mechanism based on implemented interfaces.
        ///
        /// NOTE ON ASYNC SYNERGIES (Transformation, Evolution):
        /// These are dispatched fire-and-forget via async lambdas inside a sync Dispatch call.
        /// This means exceptions thrown inside them are silently swallowed.
        /// If ordering guarantees or error propagation matter, ApplySynergies should be made async.
        /// </summary>
        private static void ApplySynergies(TokenAllocationContext context)
        {
            if (context.token is not ITokenSynergy synergy) return;

            void Dispatch<TInterface, TContext>(
                Action<TInterface, TContext> apply,
                Func<TInterface, TContext> buildContext)
                where TInterface : class, ITokenSynergy
            {
                if (synergy is not TInterface typed) return;
                if (!typed.CanApply(context)) return;

                apply(typed, buildContext(typed));

                CanvasLoggerService.PrintLogMessage(LogLevel.Debug,
                    new HashSet<LogCategory> { LogCategory.Combat },
                    $"{typeof(TInterface).Name} applied");
            }

            Dispatch<ICancellationSynergy, CancellationSynergyContext>((s, ctx) => s.ApplyCancellationSynergy(ctx), s => s.BuildContext(context));
            Dispatch<IOverrideSynergy, OverrideSynergyContext>((s, ctx) => s.ApplyOverrideSynergy(ctx), s => s.BuildContext(context));
            Dispatch<IAbsorptionSynergy, AbsorptionSynergyContext>((s, ctx) => s.ApplyAbsorptionSynergy(ctx), s => s.BuildContext(context));
            Dispatch<IResistanceSynergy, ResistanceSynergyContext>((s, ctx) => s.ApplyResistanceSynergy(ctx), s => s.BuildContext(context));
            Dispatch<IAmplificationSynergy, AmplificationSynergyContext>((s, ctx) => s.ApplyAmplificationSynergy(ctx), s => s.BuildContext(context));
            Dispatch<IAdditiveSynergy, AdditiveSynergyContext>((s, ctx) => s.ApplyAdditiveSynergy(ctx), s => s.BuildContext(context));
            Dispatch<IInversionSynergy, InversionSynergyContext>((s, ctx) => s.ApplyInversionSynergy(ctx), s => s.BuildContext(context));
            Dispatch<ITransformationSynergy, TransformationSynergyContext>(async (s, ctx) => await s.ApplyTransformationSynergy(ctx), s => s.BuildContext(context));
            Dispatch<IEvolutionSynergy, EvolutionSynergyContext>(async (s, ctx) => await s.ApplyEvolutionSynergy(ctx), s => s.BuildContext(context));
            Dispatch<IConversionSynergy, ConversionSynergyContext>((s, ctx) => s.ApplyConversionSynergy(ctx), s => s.BuildContext(context));
            Dispatch<ISpreadSynergy, SpreadSynergyContext>((s, ctx) => s.ApplySpreadSynergy(ctx), s => s.BuildContext(context));
            Dispatch<IConditionalSynergy, ConditionalSynergyContext>((s, ctx) => s.ApplyConditionalSynergy(ctx), s => s.BuildContext(context));
            Dispatch<IPassiveSynergy, PassiveSynergyContext>((s, ctx) => s.ApplyPassiveSynergy(ctx), s => s.BuildContext(context));
        }

        /// <summary>
        /// Reverts any applied synergies when a token is removed.
        /// (Not yet implemented)
        /// </summary>
        private static void UnApplySynergies(TokenController tokenController)
        {

        }

        /// <summary>
        /// Returns the first token of type T in the container.
        /// </summary>
        public static T FindSameTokenType<T>(TokenContainerController container)
            where T : TokenController =>
            container.model.tokens.OfType<T>().FirstOrDefault();

        /// <summary>
        /// Checks if a token of the same type as the given controller exists.
        /// </summary>
        public static bool FindSameTokenTypeBool(TokenContainerController container, TokenController controller) =>
            container.model.tokens.Any(t => t.GetType() == controller.GetType());

        /// <summary>
        /// Returns another token of the same type as the context token (excluding itself).
        /// </summary>
        public static TokenController FindSameTokenType(TokenAllocationContext context) =>
            context.TokenContainerController.model.tokens
                .FirstOrDefault(t => t != context.token && t.GetType() == context.token.GetType());

        /// <summary>
        /// Checks if a token of type T exists in the container.
        /// </summary>
        public static bool TokenTypeExistsInList<T>(TokenContainerController container)
            where T : TokenController =>
            container.model.tokens.Exists(t => t is T);

        /// <summary>
        /// Returns another token of the same type (excluding the current one).
        /// </summary>
        public static T GetOtherToken<T>(TokenContainerController container, T current)
            where T : TokenController =>
            container.model.tokens.OfType<T>().FirstOrDefault(t => t != current);

        /// <summary>
        /// Returns another token of the same concrete type as the given instance (excluding itself).
        /// Used for base-type CanApply checks where the generic type is not known.
        /// </summary>
        public static TokenController GetOtherToken(TokenContainerController container, TokenController current) =>
            container.model.tokens.FirstOrDefault(t => t != current && t.GetType() == current.GetType());

        /// <summary>
        /// Counts how many tokens match the provided types.
        /// </summary>
        public static int CountByTypes(TokenContainerController container, IEnumerable<Type> types) =>
            container.model.tokens.Count(item => types.Contains(item.GetType()));

        /// <summary>
        /// Checks if a token of the same type as the reference object exists.
        /// </summary>
        public static bool HasSameTokenType(TokenContainerController container, object reference) =>
            container.model.tokens.Any(t => t.GetType() == reference.GetType());

        /// <summary>
        /// Returns all tokens that match any of the provided types.
        /// </summary>
        public static List<TokenController> GetTokensByTypes(TokenContainerController container, IEnumerable<Type> types) =>
            container.model.tokens.Where(t => types.Contains(t.GetType())).ToList();

        /// <summary>
        /// Checks if any token matches the provided types.
        /// </summary>
        public static bool HasAnyByTypes(TokenContainerController container, IEnumerable<Type> types) =>
            container.model.tokens.Any(t => types.Contains(t.GetType()));

        /// <summary>
        /// Executes an action on all tokens that satisfy a condition.
        /// </summary>
        public static void ModifyTokens(TokenContainerController container,
            Func<TokenController, bool> condition,
            Action<TokenController> action)
        {
            foreach (var token in container.model.tokens)
            {
                if (condition(token))
                {
                    action(token);
                }
            }
        }

        /// <summary>
        /// Removes the first token that matches any of the given types.
        /// FIX: Model removal now precedes view removal to prevent orphaned visuals.
        /// </summary>
        public static void RemoveFirstByTypes(TokenContainerController container, IEnumerable<Type> types)
        {
            var typeSet = new HashSet<Type>(types);
            var list = container.model.tokens;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (typeSet.Contains(list[i].GetType()))
                {
                    var token = list[i];
                    list.RemoveAt(i);              // FIX: remove from model before destroying visual
                    container.view.RemoveToken(token);
                    return;
                }
            }
        }

        /// <summary>
        /// Removes all tokens that match the given types and invokes a callback for each removal.
        /// FIX: Model removal now precedes view removal to prevent orphaned visuals.
        /// </summary>
        public static void RemoveByTypes(TokenContainerController container,
            IEnumerable<Type> types,
            Action<TokenController> onRemove)
        {
            var typeSet = new HashSet<Type>(types);
            var list = container.model.tokens;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var token = list[i];

                if (typeSet.Contains(token.GetType()))
                {
                    list.RemoveAt(i);              // FIX: remove from model before destroying visual
                    container.view.RemoveToken(token);
                    onRemove?.Invoke(token);
                }
            }
        }

        /// <summary>
        /// Triggers passive synergies for all tokens (tick/update cycle).
        /// </summary>
        public void Tick()
        {
            foreach (var token in model.tokens)
            {
                if (token is IPassiveSynergy passive)
                    passive.ApplyPassiveSynergy(passive.BuildContext(
                        new TokenAllocationContext("Tick", this, token)));
            }
        }

        /// <summary>
        /// Executes the primary effect of all tokens in the container.
        /// </summary>
        public void ExecuteAll()
        {
            foreach (var token in model.tokens)
                token.ExecuteTokenEffect();
        }

        /// <summary>
        /// Removes all tokens from the container, clearing both model and view.
        /// FIX: Previously only cleared model.tokens — visuals were left orphaned in the scene.
        /// </summary>
        public void RemoveAll()
        {
            // Iterate backwards to avoid index invalidation during removal
            for (int i = model.tokens.Count - 1; i >= 0; i--)
            {
                view.RemoveToken(model.tokens[i]);
            }
            model.tokens.Clear();
        }
    }
}
