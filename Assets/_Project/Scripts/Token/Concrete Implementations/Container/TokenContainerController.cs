
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Services.DebugUtilities;

using Core.Tokens;
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
        private enum TokenStackingResult
        {
            AddedToContainer,
            UpdatedExistingToken
        }

        public TokenContainerModel model;
        public TokenContainerView view;

        /// <summary>
        /// Attempts to add a token to the container by:
        /// 1. Checking the token's allocation style prerequisites
        /// 2. Checking immunity provided by tokens already active in the container
        /// 3. Applying stacking rules
        /// 4. Applying merge synergies for an absorbed reapplication, or all
        ///    post-allocation synergies for a newly added token
        /// </summary>
        public static async Task AddTokenToContainer(TokenAllocationContext context)
        {
            if (!IsAllocationStyleSatisfied(context)) return;
            if (IsBlockedByActiveImmunity(context)) return;

            TokenStackingResult stackingResult = await ApplyStacking(context);
            if (stackingResult == TokenStackingResult.UpdatedExistingToken)
            {
                ApplyReapplicationSynergies(context);
                return;
            }

            await ApplySynergies(context);
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
                            $"Allocation blocked [{context.token.data.tokenDisplayName}] — condition not met.",
                            LogCategory.Combat);
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
        /// Reports whether a new model/view entry was added or an existing entry was updated.
        /// </summary>
        private static async Task<TokenStackingResult> ApplyStacking(TokenAllocationContext context)
        {
            TokenController incomingToken = context.token;
            TokenContainerController container = context.TokenContainerController;
            TokenController existingToken = FindSameTokenType(context);

            switch (incomingToken.data.tokenStackingdata)
            {
                case RefreshDurationStackData:
                    if (existingToken != null &&
                        existingToken.data.tokenStackingdata is RefreshDurationStackData refreshData)
                    {
                        refreshData.currentDuration = refreshData.maxDuration;
                        return TokenStackingResult.UpdatedExistingToken;
                    }
                    break;

                case LinearStackData:
                    if (existingToken != null &&
                        existingToken.data.tokenStackingdata is LinearStackData linearData)
                    {
                        linearData.stacks++;
                        return TokenStackingResult.UpdatedExistingToken;
                    }
                    break;

                case GlobalRefreshStackData:
                    if (existingToken != null &&
                        existingToken.data.tokenStackingdata is GlobalRefreshStackData globalData)
                    {
                        globalData.stacks++;

                        ModifyTokens(context.TokenContainerController,
                            token => token.GetType() == incomingToken.GetType(),
                            token =>
                            {
                                var globalRefreshStackData =
                                    (GlobalRefreshStackData)token.data.tokenStackingdata;
                                globalRefreshStackData.duration = globalData.duration;
                            });

                        return TokenStackingResult.UpdatedExistingToken;
                    }
                    break;

                case IndependentStackData:
                    // Always adds a new independent token — falls through to spawn
                    break;

                case DiminishingStackData:
                    if (existingToken != null &&
                        existingToken.data.tokenStackingdata is DiminishingStackData diminishingData)
                    {
                        diminishingData.stacks++;
                        return TokenStackingResult.UpdatedExistingToken;
                    }
                    break;
            }

            CanvasLoggerService.PrintLogMessage(LogLevel.Debug,
                $"Allocating Token [{context.token.data.tokenDisplayName}] | " +
                $"Owner: [{context.ownerName}] | " +
                $"Target Container: [{context.TokenContainerController.name}]", LogCategory.Combat);

            container.model.tokens.Add(incomingToken);
            await container.view.AddTokenToView(incomingToken);
            return TokenStackingResult.AddedToContainer;
        }

        /// <summary>
        /// Removes a specific token instance from the container.
        /// Removes from model first, then removes its visual representation.
        /// </summary>
        public static void RemoveTokenFromContainer(TokenContainerController container, TokenController controller)
        {
            if (!container.model.tokens.Contains(controller)) return;

            UnApplySynergies(controller, container);
            CanvasLoggerService.PrintLogMessage(LogLevel.Debug,
                $"Removing Token [{controller.data.tokenDisplayName}] | " +
                $"Target Container: [{container.name}]", LogCategory.Combat);
            container.model.tokens.Remove(controller);
            container.view.RemoveToken(controller);
        }

        /// <summary>
        /// Checks immunity sources already active in the target container.
        /// A cancellation reaction may bypass only the immunity source it consumes.
        /// </summary>
        private static bool IsBlockedByActiveImmunity(TokenAllocationContext context)
        {
            foreach (TokenController activeToken in context.TokenContainerController.model.tokens)
            {
                if (activeToken is not IImmunitySynergy activeImmunity)
                    continue;

                ImmunitySynergyContext immunityContext =
                    activeImmunity.BuildImmunityContext(context);
                immunityContext.incomingToken = context.token;

                if (!activeImmunity.CheckImmunity(immunityContext))
                    continue;

                if (CanIncomingCancellationConsumeImmunitySource(context.token, activeToken))
                    continue;

                CanvasLoggerService.PrintLogMessage(LogLevel.Debug,
                    $"Immunity Synergy blocked [{context.token.data.tokenDisplayName}] " +
                    $"because [{activeToken.data.tokenDisplayName}] protects " +
                    $"Container [{context.TokenContainerController.name}]",
                    LogCategory.Combat);
                return true;
            }

            return false;
        }

        private static bool CanIncomingCancellationConsumeImmunitySource(
            TokenController incomingToken,
            TokenController immunitySource)
        {
            return incomingToken is ICancellationSynergy cancellationSynergy
                && cancellationSynergy.cancellationSynergys.Contains(immunitySource.GetType());
        }

        /// <summary>
        /// Applies only merge behavior when stacking absorbs the incoming instance.
        /// Other post-allocation synergies belong to the surviving stored token.
        /// </summary>
        private static void ApplyReapplicationSynergies(TokenAllocationContext context)
        {
            if (context.token is not IAdditiveSynergy additiveSynergy)
                return;

            if (!additiveSynergy.CanApply(context))
                return;

            additiveSynergy.ApplyAdditiveSynergy(
                additiveSynergy.BuildAdditiveContext(context));

            CanvasLoggerService.PrintLogMessage(LogLevel.Debug,
                $"{nameof(IAdditiveSynergy)} applied during token reapplication",
                LogCategory.Combat);
        }

        /// <summary>
        /// Applies all compatible synergies for the given token.
        /// Async synergies are awaited so each step observes the completed prior step.
        /// </summary>
        private static async Task ApplySynergies(TokenAllocationContext context)
        {
            if (context.token is not ITokenSynergy synergy) return;

            void DispatchSynchronous<TInterface, TContext>(
                Action<TInterface, TContext> apply,
                Func<TInterface, TContext> buildContext)
                where TInterface : class, ITokenSynergy
            {
                if (synergy is not TInterface typedSynergy) return;
                if (!typedSynergy.CanApply(context)) return;

                apply(typedSynergy, buildContext(typedSynergy));

                CanvasLoggerService.PrintLogMessage(LogLevel.Debug,
                    $"{typeof(TInterface).Name} applied", LogCategory.Combat);
            }

            async Task DispatchAsynchronous<TInterface, TContext>(
                Func<TInterface, TContext, Task> apply,
                Func<TInterface, TContext> buildContext)
                where TInterface : class, ITokenSynergy
            {
                if (synergy is not TInterface typedSynergy) return;
                if (!typedSynergy.CanApply(context)) return;

                await apply(typedSynergy, buildContext(typedSynergy));

                CanvasLoggerService.PrintLogMessage(LogLevel.Debug,
                    $"{typeof(TInterface).Name} applied", LogCategory.Combat);
            }

            DispatchSynchronous<ICancellationSynergy, CancellationSynergyContext>(
                (typedSynergy, synergyContext) =>
                    typedSynergy.ApplyCancellationSynergy(synergyContext),
                typedSynergy => typedSynergy.BuildCancellationContext(context));

            if (!context.TokenContainerController.model.tokens.Contains(context.token))
                return;

            DispatchSynchronous<IOverrideSynergy, OverrideSynergyContext>(
                (typedSynergy, synergyContext) =>
                    typedSynergy.ApplyOverrideSynergy(synergyContext),
                typedSynergy => typedSynergy.BuildOverrideContext(context));
            DispatchSynchronous<IAbsorptionSynergy, AbsorptionSynergyContext>(
                (typedSynergy, synergyContext) =>
                    typedSynergy.ApplyAbsorptionSynergy(synergyContext),
                typedSynergy => typedSynergy.BuildAbsorptionContext(context));
            DispatchSynchronous<IResistanceSynergy, ResistanceSynergyContext>(
                (typedSynergy, synergyContext) =>
                    typedSynergy.ApplyResistanceSynergy(synergyContext),
                typedSynergy => typedSynergy.BuildResistanceContext(context));
            DispatchSynchronous<IAmplificationSynergy, AmplificationSynergyContext>(
                (typedSynergy, synergyContext) =>
                    typedSynergy.ApplyAmplificationSynergy(synergyContext),
                typedSynergy => typedSynergy.BuildAmplificationContext(context));
            DispatchSynchronous<IAdditiveSynergy, AdditiveSynergyContext>(
                (typedSynergy, synergyContext) =>
                    typedSynergy.ApplyAdditiveSynergy(synergyContext),
                typedSynergy => typedSynergy.BuildAdditiveContext(context));
            DispatchSynchronous<IInversionSynergy, InversionSynergyContext>(
                (typedSynergy, synergyContext) =>
                    typedSynergy.ApplyInversionSynergy(synergyContext),
                typedSynergy => typedSynergy.BuildInversionContext(context));

            await DispatchAsynchronous<ITransformationSynergy, TransformationSynergyContext>(
                (typedSynergy, synergyContext) =>
                    typedSynergy.ApplyTransformationSynergy(synergyContext),
                typedSynergy => typedSynergy.BuildTransformationContext(context));

            if (!context.TokenContainerController.model.tokens.Contains(context.token))
                return;

            await DispatchAsynchronous<IEvolutionSynergy, EvolutionSynergyContext>(
                (typedSynergy, synergyContext) =>
                    typedSynergy.ApplyEvolutionSynergy(synergyContext),
                typedSynergy => typedSynergy.BuildEvolutionContext(context));

            if (!context.TokenContainerController.model.tokens.Contains(context.token))
                return;

            await DispatchAsynchronous<IConversionSynergy, ConversionSynergyContext>(
                (typedSynergy, synergyContext) =>
                    typedSynergy.ApplyConversionSynergy(synergyContext),
                typedSynergy => typedSynergy.BuildConversionContext(context));
            await DispatchAsynchronous<ISpreadSynergy, SpreadSynergyContext>(
                (typedSynergy, synergyContext) =>
                    typedSynergy.ApplySpreadSynergy(synergyContext),
                typedSynergy => typedSynergy.BuildSpreadContext(context));
            DispatchSynchronous<IConditionalSynergy, ConditionalSynergyContext>(
                (typedSynergy, synergyContext) =>
                    typedSynergy.ApplyConditionalSynergy(synergyContext),
                typedSynergy => typedSynergy.BuildConditionalContext(context));
            DispatchSynchronous<IPassiveSynergy, PassiveSynergyContext>(
                (typedSynergy, synergyContext) =>
                    typedSynergy.ApplyPassiveSynergy(synergyContext),
                typedSynergy => typedSynergy.BuildPassiveContext(context));
        }

        /// <summary>
        /// Reverts any reverseable synergy applied by this token.
        /// Called automatically by RemoveTokenFromContainer before the token leaves the model.
        /// Only synergies that implement IReverseableSynergy are affected —
        /// destructive synergies (Cancellation, Override, Absorption, Transformation, Evolution, Spread)
        /// are intentionally excluded because their effects are irreversible by design.
        /// </summary>
        private static void UnApplySynergies(TokenController tokenController, TokenContainerController container)
        {
            if (tokenController is IReverseableSynergy reverseable)
                reverseable.ReverseSynergy(container);
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
        /// </summary>
        public static void RemoveFirstByTypes(TokenContainerController container, IEnumerable<Type> types)
        {
            var matchingTypes = new HashSet<Type>(types);
            List<TokenController> tokens = container.model.tokens;

            for (int tokenIndex = tokens.Count - 1; tokenIndex >= 0; tokenIndex--)
            {
                TokenController token = tokens[tokenIndex];
                if (!matchingTypes.Contains(token.GetType()))
                    continue;

                RemoveTokenFromContainer(container, token);
                return;
            }
        }

        /// <summary>
        /// Removes all tokens that match the given types and invokes a callback for each removal.
        /// </summary>
        public static void RemoveByTypes(TokenContainerController container,
            IEnumerable<Type> types,
            Action<TokenController> onRemove)
        {
            var matchingTypes = new HashSet<Type>(types);
            List<TokenController> tokens = container.model.tokens;

            for (int tokenIndex = tokens.Count - 1; tokenIndex >= 0; tokenIndex--)
            {
                TokenController token = tokens[tokenIndex];

                if (!matchingTypes.Contains(token.GetType()))
                    continue;

                RemoveTokenFromContainer(container, token);
                onRemove?.Invoke(token);
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
                    passive.ApplyPassiveSynergy(passive.BuildPassiveContext(
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
                TokenContainerController.RemoveTokenFromContainer(this, model.tokens[i]);
            }
        }
    }
}
