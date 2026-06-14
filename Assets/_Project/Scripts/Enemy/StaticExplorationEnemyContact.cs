using DetectionSystem.Core;
using UnityEngine;

/// <summary>
/// Inimigo estático colocado na cena (ex.: fantasma da vila).
/// Faz polling do <see cref="Detector"/> local e inicia combate no contato,
/// sem depender do ciclo de pool (<see cref="Systems.NPC.Enemy.NpcEnemy.Activate"/>).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Detector))]
public sealed class StaticExplorationEnemyContact : MonoBehaviour
{
    private const string CombatSceneName = "CombatScene";
    private const string ContactShapeLabel = "Contact";

    private Detector _detector;
    private bool _combatTriggered;

    private void Awake()
    {
        _detector = GetComponent<Detector>();
    }

    private void OnEnable()
    {
        if (_detector != null)
        {
            _detector.OnDetectorEnter += HandleDetectorEnter;
        }
    }

    private void OnDisable()
    {
        if (_detector != null)
        {
            _detector.OnDetectorEnter -= HandleDetectorEnter;
        }
    }

    private void Update()
    {
        if (_detector == null || CombatExplorationBridge.IsCombatReentryBlocked)
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

        if (_combatTriggered || CombatExplorationBridge.IsCombatReentryBlocked)
        {
            return;
        }

        _combatTriggered = true;
        CombatExplorationBridge.Instance?.NotifyEnteringCombat();
        SceneTransitionHandler.LoadScene(CombatSceneName);
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
