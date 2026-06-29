using DetectionSystem.Core;
using System.Collections;
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

    // Tempo de espera antes de confirmar o combate
    [SerializeField] private float _detectionDelay = 3f;


    private Detector _detector;
    private bool _combatTriggered;
    private Coroutine _pendingCombatCoroutine;   // <-- coroutine pendente

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
            _detector.OnDetectorEnter += HandleDetectorEnter;
            _detector.OnDetectorExit  += HandleDetectorExit;
        }

        _combatTriggered = false;
    }

    private void OnDisable()
    {
        CancelPendingCombat();   // garante limpeza ao desativar

        if (_detector != null)
        {
            _detector.OnDetectorEnter -= HandleDetectorEnter;
            _detector.OnDetectorExit  -= HandleDetectorExit;
        }
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
    }

    // --- detecção de entrada: agenda combate com delay ---

    private void HandleDetectorEnter(Collider detectedCollider, string shapeLabel, int shapeIndex)
    {
        if (detectedCollider.tag != "Player") return;
        if (_pendingCombatCoroutine != null) return;  // já aguardando

        _pendingCombatCoroutine = StartCoroutine(CombatDelayRoutine());
    }

    private IEnumerator CombatDelayRoutine()
    {
        yield return new WaitForSeconds(_detectionDelay);

        // confirma que o combate ainda não foi bloqueado durante a espera
        if (CombatExplorationBridge.IsHorseBossCombatReentryBlocked)
        {
            _pendingCombatCoroutine = null;
            yield break;
        }

        ExplorationLoadContext.EnsureRuntimeInstance();
        _combatTriggered = true;
        _pendingCombatCoroutine = null;
        CombatExplorationBridge.RegisterHorseBossOverworldEncounter(CombatEnemyRosterSize);
        SceneTransitionHandler.LoadScene(CombatSceneName);
    }

    // --- detecção de saída: cancela se o jogador saiu antes do delay ---

    private void HandleDetectorExit(Collider detectedCollider, string shapeLabel, int shapeIndex)
    {
        if (detectedCollider.tag != "Player") return;
        CancelPendingCombat(); 
        _combatTriggered = false;
    }

    private void CancelPendingCombat()
    {
        if (_pendingCombatCoroutine == null) return;
        StopCoroutine(_pendingCombatCoroutine);
        _pendingCombatCoroutine = null;
    }

    // --- resto inalterado ---

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