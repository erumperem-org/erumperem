using DetectionSystem.Core;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(DetectionComponent))]
[RequireComponent(typeof(Detector))]
[DefaultExecutionOrder(-200)]
public sealed class HorseBossOverworldCombatContact : MonoBehaviour
{
    private const string CombatSceneName = "CombatScene";
    private const string ContactShapeLabel = "Contact";
    private const int CombatEnemyRosterSize = 4;

    private Detector _detector;
    private bool _combatTriggered;
    private bool _hadPlayerContact;

    private void Awake()
    {
        _detector = GetComponent<Detector>();
    }

    private void OnEnable()
    {
        _detector = GetComponent<Detector>();
        if (_detector != null)
        {
            _detector.ReinitializeScanner();
        }

        _combatTriggered = false;
        _hadPlayerContact = false;
    }

    private void Start()
    {
        _detector = GetComponent<Detector>();
        _detector?.ReinitializeScanner();
        _combatTriggered = false;
    }

    private void Update()
    {
        if (_detector == null) return;
        _detector.Scan();
        bool hasPlayerContact = HasPlayerContact();
        if (!hasPlayerContact)
        {
            if (_hadPlayerContact)
                CombatExplorationBridge.Instance?.NotifyPlayerLeftCombatEntryZone();
            _hadPlayerContact = false;
            _combatTriggered = false;
            return;
        }
        _hadPlayerContact = true;
        TryBeginCombat();
    }

    private bool HasPlayerContact()
    {
        var shapes = _detector.DetectionComponent.Shapes;
        var contacts = _detector.DetectedPerShape;
        if (contacts == null) return false;
        for (int i = 0; i < shapes.Count && i < contacts.Count; i++)
        {
            if (!string.Equals(shapes[i].label, ContactShapeLabel, System.StringComparison.Ordinal)) continue;
            foreach (var collider in contacts[i])
                if (IsPlayerCollider(collider)) return true;
        }
        return false;
    }

    private void TryBeginCombat()
    {
        if (_combatTriggered || SceneTransitionHandler.IsTransitioning || IsCombatTriggerBlocked())
        {
            return;
        }

        ExplorationLoadContext.EnsureRuntimeInstance();
        _combatTriggered = true;
        CombatExplorationBridge.RegisterHorseBossOverworldEncounter(CombatEnemyRosterSize);
        SceneTransitionHandler.LoadScene(CombatSceneName);
    }

    private static bool IsCombatTriggerBlocked()
    {
        return CombatExplorationBridge.IsHorseBossCombatReentryBlocked
            || CombatExplorationBridge.AreExplorationCombatContactsBlocked
            || CombatExplorationBridge.RequiresCombatEntryZoneClearance
            || ExplorationVillageEvents.IsPlayerInsideVillage;
    }

    private static bool IsPlayerCollider(Collider collider)
    {
        if (collider == null) return false;
        if (collider.CompareTag("Player")) return true;
        return collider.GetComponentInParent<PlayableCharacter>() != null;
    }
}
