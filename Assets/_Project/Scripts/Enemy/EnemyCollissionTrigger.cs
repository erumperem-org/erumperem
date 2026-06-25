using UnityEngine;

/// <summary>
/// Gatilho de combate para inimigos estáticos colocados na cena (ex.: fantasma da vila).
/// Os NPCs da pool usam <see cref="Systems.NPC.Enemy.NpcEnemyContactHandler"/>.
/// </summary>
[DisallowMultipleComponent]
public class EnemyCollissionTrigger : MonoBehaviour
{
    private const string CombatSceneName = "CombatScene";

    private static readonly Vector3 DefaultTriggerCenter = new(0f, 1f, 0f);
    private static readonly Vector3 DefaultTriggerSize = new(2f, 2f, 2f);

    private void Awake() => EnsureTriggerPhysicsConfigured();

    private void OnEnable() => EnsureTriggerPhysicsConfigured();

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        if (IsCombatTriggerBlocked())
            return;

        CombatExplorationBridge.Instance?.NotifyStaticCombatContactTriggered();
        CombatExplorationBridge.Instance?.NotifyEnteringCombat();
        SceneTransitionHandler.LoadScene(CombatSceneName);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        CombatExplorationBridge.Instance?.NotifyPlayerLeftCombatEntryZone();
    }

    private static bool IsCombatTriggerBlocked()
    {
        return CombatExplorationBridge.IsCombatReentryBlocked
            || CombatExplorationBridge.AreExplorationCombatContactsBlocked
            || CombatExplorationBridge.RequiresCombatEntryZoneClearance
            || ExplorationVillageEvents.IsPlayerInsideVillage;
    }

    private void EnsureTriggerPhysicsConfigured()
    {
        var rigidbody = GetComponent<Rigidbody>();
        if (rigidbody == null)
            rigidbody = gameObject.AddComponent<Rigidbody>();

        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;

        var collider = GetComponent<Collider>();
        if (collider == null)
        {
            var triggerCollider = gameObject.AddComponent<BoxCollider>();
            triggerCollider.center = DefaultTriggerCenter;
            triggerCollider.size = DefaultTriggerSize;
            triggerCollider.isTrigger = true;
            return;
        }

        collider.isTrigger = true;
    }

    private static bool IsPlayerCollider(Collider collider)
    {
        if (collider == null) return false;

        if (collider.CompareTag("Player"))
            return true;

        return collider.GetComponentInParent<PlayableCharacter>() != null;
    }
}
