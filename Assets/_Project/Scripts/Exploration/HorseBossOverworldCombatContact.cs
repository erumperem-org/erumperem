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
[DefaultExecutionOrder(-200)]
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
            _detector.ReinitializeScanner();
            _detector.OnDetectorEnter += HandleDetectorEnter;
            _detector.OnDetectorExit += HandleDetectorExit;
        }

        _combatTriggered = false;
    }

    private void OnDisable()
    {
        if (_detector != null)
        {
            _detector.OnDetectorEnter -= HandleDetectorEnter;
            _detector.OnDetectorExit -= HandleDetectorExit;
        }
    }

    private void Start()
    {
        EnsureContactDetectionConfigured();
        _detector = GetComponent<Detector>();
        _detector?.ReinitializeScanner();
        _combatTriggered = false;
    }

    private void Update()
    {
        if (_detector == null)
        {
            return;
        }

        if (IsCombatTriggerBlocked())
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
        return CombatExplorationBridge.IsHorseBossCombatReentryBlocked
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

        EnsureSolidBlockingCollider();
    }

    /// <summary>
    /// Mantém collider sólido para bloqueio físico; combate usa Detector (overlap), não trigger.
    /// </summary>
    private void EnsureSolidBlockingCollider()
    {
        var boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            boxCollider.isTrigger = false;
            return;
        }

        var rootCollider = GetComponent<Collider>();
        if (rootCollider != null)
        {
            rootCollider.isTrigger = false;
            return;
        }

        boxCollider = gameObject.AddComponent<BoxCollider>();
        boxCollider.center = ContactShapeLocalOffset;
        boxCollider.size = ContactShapeHalfExtents * 2f;
        boxCollider.isTrigger = false;
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
