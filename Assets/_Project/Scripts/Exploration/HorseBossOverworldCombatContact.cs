using DetectionSystem.Core;
using UnityEngine;

/// <summary>
/// Contacto no overworld com o Horse Boss: inicia combate com exactamente um inimigo
/// (posição aleatória entre os 4 slots) configurado como Horse Boss.
/// Usa <see cref="Detector"/> (overlap por frame), igual aos inimigos estáticos —
/// fiável com CharacterController e quando o jogador já está dentro da zona ao carregar.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DetectionComponent))]
[RequireComponent(typeof(Detector))]
public sealed class HorseBossOverworldCombatContact : MonoBehaviour
{
    private const string CombatSceneName = "CombatScene";
    private const string ContactShapeLabel = "Contact";
    private const int CombatEnemyRosterSize = 4;

    private static readonly Vector3 ContactShapeLocalOffset = new(0f, 2.5f, 0f);
    private static readonly Vector3 ContactShapeHalfExtents = new(2f, 2.5f, 2f);

    private Detector _detector;
    private bool _combatTriggered;

    private void Awake()
    {
        EnsureContactDetectionConfigured();
        _detector = GetComponent<Detector>();
    }

    private void OnEnable()
    {
        EnsureContactDetectionConfigured();
        _detector = GetComponent<Detector>();
        if (_detector != null)
        {
            _detector.OnDetectorEnter += HandleDetectorEnter;
            _detector.OnDetectorExit += HandleDetectorExit;
        }
    }

    private void OnDisable()
    {
        if (_detector != null)
        {
            _detector.OnDetectorEnter -= HandleDetectorEnter;
            _detector.OnDetectorExit -= HandleDetectorExit;
        }
    }

    private void Update()
    {
        if (_detector == null || IsCombatTriggerBlocked())
        {
            return;
        }

        _detector.Scan();
    }

    private void HandleDetectorEnter(Collider detectedCollider, string shapeLabel, int shapeIndex)
    {
        if (!string.Equals(shapeLabel, ContactShapeLabel, System.StringComparison.Ordinal))
        {
            return;
        }

        if (!IsPlayerCollider(detectedCollider))
        {
            return;
        }

        if (_combatTriggered || IsCombatTriggerBlocked())
        {
            return;
        }

        ExplorationLoadContext.EnsureRuntimeInstance();

        _combatTriggered = true;
        CombatExplorationBridge.RegisterHorseBossOverworldEncounter(CombatEnemyRosterSize);
        SceneTransitionHandler.LoadScene(CombatSceneName);
    }

    private void HandleDetectorExit(Collider detectedCollider, string shapeLabel, int shapeIndex)
    {
        if (!string.Equals(shapeLabel, ContactShapeLabel, System.StringComparison.Ordinal))
        {
            return;
        }

        if (!IsPlayerCollider(detectedCollider))
        {
            return;
        }

        _combatTriggered = false;
        CombatExplorationBridge.Instance?.NotifyPlayerLeftCombatEntryZone();
    }

    private static bool IsCombatTriggerBlocked()
    {
        return CombatExplorationBridge.IsCombatReentryBlocked
            || CombatExplorationBridge.AreExplorationCombatContactsBlocked
            || CombatExplorationBridge.RequiresCombatEntryZoneClearance
            || ExplorationVillageEvents.IsPlayerInsideVillage;
    }

    private void EnsureContactDetectionConfigured()
    {
        var detectionComponent = GetComponent<DetectionComponent>();
        if (detectionComponent == null)
        {
            detectionComponent = gameObject.AddComponent<DetectionComponent>();
        }

        detectionComponent.EnsurePlayerContactBoxShape(
            ContactShapeLocalOffset,
            ContactShapeHalfExtents,
            ContactShapeLabel);

        if (GetComponent<Detector>() == null)
        {
            gameObject.AddComponent<Detector>();
        }
    }

    private static bool IsPlayerCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (collider.CompareTag("Player"))
        {
            return true;
        }

        return collider.GetComponentInParent<PlayableCharacter>() != null;
    }
}
