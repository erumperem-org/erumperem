using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A spread vector that propagates poison to nearby targets.
    /// On allocation, checks each container in <see cref="targets"/> and allocates
    /// a new <see cref="PoisonToken"/> to any that do not yet hold one.
    /// Does not consume or modify itself — the contagion persists after spreading.
    /// The spread target list must be provided externally (e.g. by the combat system).
    /// Allocation style: on-hit — applied whenever the attack connects.
    /// </summary>
    public class ContagionToken : TokenController, ISpreadSynergy
    {
        private readonly List<TokenContainerController> targets;

        public ContagionToken(List<TokenContainerController> spreadTargets) : base(
            typeof(ContagionToken).Name,
            new IndependentStackData(),
            new IOnHitTokenAllocation())
        {
            targets = spreadTargets;
        }

        public SpreadSynergyContext BuildContext(TokenAllocationContext context) =>
            new SpreadSynergyContext(context.TokenContainerController, this, targets,
                async container =>
                {
                    var spread = new PoisonToken();
                    await TokenContainerController.AddTokenToContainer(
                        new TokenAllocationContext(context.ownerName, container, spread));
                });

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
