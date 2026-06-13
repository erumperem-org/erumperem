using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DetectionSystem.Core;
using Services.DebugUtilities;
using UnityEngine;

[RequireComponent(typeof(Detector))]
public sealed class PlayerDetectionSystem : MonoBehaviour
{
    [SerializeField] private PlayableAnimationController _animationController;
    [SerializeField] private PlayableCharacter _character;
    [SerializeField] private PlayerInventorySystem _inventory;

    public IReadOnlyList<Interactable> Available => _available;
    [SerializeField] private List<Interactable> _available = new();
    private Detector _detector;
    private Coroutine _scanCoroutine;

    private static readonly string[] RelevantAreas =
    {
        "InteractableDetectionArea",
        "CharactersDetectionArea"
    };

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        _detector = GetComponent<Detector>();
        _detector.OnDetectorEnter += OnEnter;
        _detector.OnDetectorExit += OnExit;

        if (_animationController == null)
        {
            _animationController = GetComponentInChildren<PlayableAnimationController>();
        }
    }

    private void OnDisable() => StopScan();

    // ── API pública ───────────────────────────────────────────────────────

    public void StartScan()
    {
        StopScan();
        _scanCoroutine = StartCoroutine(ScanLoop());
    }

    public void StopScan()
    {
        if (_scanCoroutine == null) return;
        StopCoroutine(_scanCoroutine);
        _scanCoroutine = null;
    }

    public void ClearAvailable() => _available.Clear();

    public void SetTag(string tag) => gameObject.tag = tag;

    /// <summary>
    /// Executa a interação com o interactable mais próximo.
    /// Chamado via <see cref="Player.PlayerInputReader.OnInteract"/> — não lê input diretamente.
    /// </summary>

    public void Interact()
    {
        _available.RemoveAll(t => t == null);
        if (_available.Count == 0) return;

        var closest = _available
            .OrderBy(t => (transform.position - t.transform.position).sqrMagnitude)
            .FirstOrDefault();

        if (closest == null) return;

        if (!closest.CanInteract)
        {
            _available.Remove(closest);
            return;
        }

        TriggerInteractionAnimation(closest);

        // ── FIX: captura o reader no momento da interação, não na closure ──
        // Se PlayerInput for null aqui o personagem já não é Main — a lambda
        // vira um no-op seguro em vez de lançar NullReferenceException.
        var inputReader = _character != null ? _character.PlayerInput : null;

        var ctx = new InteractionContext(
            setInputBlocked: blocked =>
            {
                if (inputReader != null)
                    inputReader.IsBlocked = blocked;
            },
            inventory: _inventory);

        closest.ExecuteInteraction(ctx);

        if (!closest.CanInteract)
            _available.Remove(closest);
    }

    // ── Detecção ──────────────────────────────────────────────────────────

    private IEnumerator ScanLoop()
    {
        while (true) { _detector.Scan(); yield return null; }
    }

    private void OnEnter(Collider col, string label, int _)
    {
        TryToggleCharacterInteractPrompt(col, label, shouldShow: true);

        if (!IsRelevant(label)) return;

        var interactable = ResolveInteractable(col);
        if (interactable == null || _available.Contains(interactable)) return;

        _available.Add(interactable);
        LoggerService.PrintLogMessage(LogLevel.Debug, $"Interactable [{col.gameObject.name}] found");
    }

    private void OnExit(Collider col, string label, int _)
    {
        TryToggleCharacterInteractPrompt(col, label, shouldShow: false);

        if (!IsRelevant(label)) return;

        var interactable = ResolveInteractable(col);
        if (interactable != null) _available.Remove(interactable);

        LoggerService.PrintLogMessage(LogLevel.Debug, $"Interactable [{col.gameObject.name}] lost");
    }

    private static void TryToggleCharacterInteractPrompt(Collider collider, string shapeLabel, bool shouldShow)
    {
        if (shapeLabel != "CharactersDetectionArea")
        {
            return;
        }

        var promptToggle = collider.GetComponentInParent<DetectionPromptToggle>();
        if (promptToggle == null)
        {
            return;
        }

        if (shouldShow)
        {
            promptToggle.RegisterPlayerProximity();
        }
        else
        {
            promptToggle.UnregisterPlayerProximity();
        }
    }

    private static Interactable ResolveInteractable(Collider collider)
    {
        if (collider.TryGetComponent(out Interactable interactableOnCollider))
        {
            return interactableOnCollider;
        }

        return collider.GetComponentInParent<Interactable>();
    }

    private static bool IsRelevant(string label) =>
        System.Array.IndexOf(RelevantAreas, label) >= 0;

    // ── Animação ──────────────────────────────────────────────────────────

    private void TriggerInteractionAnimation(Interactable interactable)
    {
        if (_animationController == null) return;

        switch (interactable)
        {
            case DoorInteractable door:
                if (door.IsOpened) _animationController.TriggerClosingDoor();
                else _animationController.TriggerOpeningDoor();
                break;

            case ChestInteractable:
                _animationController.TriggerOpeningChest();
                break;
        }
    }
}