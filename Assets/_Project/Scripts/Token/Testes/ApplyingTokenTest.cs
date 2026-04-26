using System.Collections.Generic;
using Core.Tokens;
using UnityEngine;

public class ApplyingTokenTest : MonoBehaviour
{
    public TokenContainerController container;
    public TokenContainerController containerB;
    public TokenContainerController containerC;

    private bool isLowHealth = false;
    private bool tookDamageThisTurn = false;

    // -------------------------------------------------------------------------
    // IMMUNITY — FreezingStatusToken blocks BurningStatusToken and vice-versa
    // -------------------------------------------------------------------------

    public void AllocFreezing()
    {
        var token = new FreezingStatusToken(container, 3);
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    public void AllocBurning()
    {
        var token = new BurningStatusToken(container, 3);
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    // -------------------------------------------------------------------------
    // CANCELLATION — AcidToken and ArmorToken cancel each other on contact
    // -------------------------------------------------------------------------

    public void AllocAcid()
    {
        var token = new AcidToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    public void AllocArmor()
    {
        var token = new ArmorToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    // -------------------------------------------------------------------------
    // OVERRIDE — DivineShieldToken removes DoTs, CurseToken removes buffs
    // -------------------------------------------------------------------------

    public void AllocDivineShield()
    {
        var token = new DivineShieldToken(container);
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    public void AllocCurse()
    {
        var token = new CurseToken(container);
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Enemy", container, token));
    }

    // -------------------------------------------------------------------------
    // ABSORPTION — VampireToken absorbs BleedToken, PhoenixToken absorbs AshToken
    // -------------------------------------------------------------------------

    public void AllocVampire()
    {
        var token = new VampireToken(container);
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    public void AllocPhoenix()
    {
        var token = new PhoenixToken(container);
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    // -------------------------------------------------------------------------
    // RESISTANCE — StoneToken resists Bleed, MysticVeilToken resists Curse
    // -------------------------------------------------------------------------

    public void AllocStone()
    {
        var token = new StoneToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    public void AllocMysticVeil()
    {
        var token = new MysticVeilToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    // -------------------------------------------------------------------------
    // AMPLIFICATION — RageToken scales with Bleed, FrostNovaToken scales with Freezing
    // -------------------------------------------------------------------------

    public void AllocRage()
    {
        var token = new RageToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    public void AllocFrostNova()
    {
        var token = new FrostNovaToken(container);
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    // -------------------------------------------------------------------------
    // ADDITIVE — PoisonToken and RegenToken accumulate their values additively
    // -------------------------------------------------------------------------

    public void AllocPoison()
    {
        var token = new PoisonToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Enemy", container, token));
    }

    public void AllocRegen()
    {
        var token = new RegenToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    // -------------------------------------------------------------------------
    // INVERSION — MirrorToken inverts Bleed into heal, ChaoticAura inverts Regen into damage
    // -------------------------------------------------------------------------

    public void AllocMirror()
    {
        var token = new MirrorToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    public void AllocChaoticAura()
    {
        var token = new ChaoticAuraToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Enemy", container, token));
    }

    // -------------------------------------------------------------------------
    // TRANSFORMATION — IceShardToken + BleedToken → FrostBleedToken
    //                  EmberToken + PoisonToken → VenomFireToken
    // Allocate the pair tokens first, then the transformer to trigger the reaction
    // -------------------------------------------------------------------------

    public void AllocIceShard()
    {
        var bleed = new BleedToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Enemy", container, bleed));

        var token = new IceShardToken(container);
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    public void AllocEmber()
    {
        var poison = new PoisonToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Enemy", container, poison));

        var token = new EmberToken(container);
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    // -------------------------------------------------------------------------
    // EVOLUTION — BleedToken evolves into HemorrhageToken at 3 stacks
    //             AshToken evolves into InfernoToken at 3 stacks
    // Allocate 3 times to trigger evolution
    // -------------------------------------------------------------------------

    public void AllocBleedStack()
    {
        var token = new BleedToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Enemy", container, token));
    }

    public void AllocAshStack()
    {
        var token = new AshToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Enemy", container, token));
    }

    // -------------------------------------------------------------------------
    // CONVERSION — AlchemyToken converts Poison → Regen over ticks
    //              CorruptionToken converts Regen → Poison over ticks
    // Allocate the source token first, then the converter
    // -------------------------------------------------------------------------

    public void AllocAlchemy()
    {
        var poison = new PoisonToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Enemy", container, poison));

        var token = new AlchemyToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    public void AllocCorruption()
    {
        var regen = new RegenToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, regen));

        var token = new CorruptionToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Enemy", container, token));
    }

    // -------------------------------------------------------------------------
    // SPREAD — ContagionToken spreads Poison to containerB and containerC
    //          WildFireToken spreads Burning to containerB and containerC
    // Requires containerB and containerC to be assigned in Inspector
    // -------------------------------------------------------------------------

    public void AllocContagion()
    {
        var targets = new List<TokenContainerController> { containerB, containerC };
        var token = new ContagionToken(targets);
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Enemy", container, token));
    }

    public void AllocWildFire()
    {
        var targets = new List<TokenContainerController> { containerB, containerC };
        var token = new WildFireToken(targets);
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Enemy", container, token));
    }

    // -------------------------------------------------------------------------
    // CONDITIONAL — ExecutionToken triggers when HP < 20%
    //               RevengeToken triggers when holder took damage this turn
    // Use SimulateLowHealth() and SimulateTookDamage() to test the conditions
    // -------------------------------------------------------------------------

    public void AllocExecution()
    {
        var token = new ExecutionToken(container, () => isLowHealth);
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    public void AllocRevenge()
    {
        var token = new RevengeToken(container, () => tookDamageThisTurn);
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    public void SimulateLowHealth() => isLowHealth = true;
    public void SimulateTookDamage() => tookDamageThisTurn = true;

    // -------------------------------------------------------------------------
    // PASSIVE — ThornToken reflects damage per Bleed stack
    //           RegenerationAuraToken heals per Regen stack each tick
    // Call Tick() externally each turn to drive passive synergies
    // -------------------------------------------------------------------------

    public void AllocThorn()
    {
        var token = new ThornToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    public void AllocRegenerationAura()
    {
        var token = new RegenerationAuraToken();
        _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", container, token));
    }

    // -------------------------------------------------------------------------
    // UTILITY
    // -------------------------------------------------------------------------

    public void Tick() => container.Tick();
    public void ExecuteAll() => container.ExecuteAll();
    public void RemoveAll() => container.RemoveAll();
}