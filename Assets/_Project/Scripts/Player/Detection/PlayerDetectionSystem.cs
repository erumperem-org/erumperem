using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DetectionSystem.Core;
using Services.DebugUtilities;
using UnityEngine;

[RequireComponent(typeof(Detector))]
[DefaultExecutionOrder(-200)]
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

    public void Update()
    {
        if (this.tag == "Player")
        {
            _detector.Scan();
        }
    }
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
            .Where(t => t.isActiveAndEnabled && t.CanInteract)
            .OrderBy(t => (transform.position - t.transform.position).sqrMagnitude)
            .FirstOrDefault();

        TryInteract(closest);
    }

    public bool IsInInteractionRange(Interactable target) => target != null && _available.Contains(target);

    public bool TryInteract(Interactable closest)
    {
        if (closest == null || !closest.isActiveAndEnabled || !IsInInteractionRange(closest)) return false;
        if (_character == null) _character = GetComponent<PlayableCharacter>();
        if (_character == null || _character.CurrentState != PlayableCharacterState.Main) return false;
        if (_character.PlayerInput != null && !_character.PlayerInput.CanAcceptWorldInput) return false;

        if (!closest.CanInteract)
        {
            _available.Remove(closest);
            return false;
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

        if (!closest.CanInteract && closest is not CharacterSelectionNpc)
            _available.Remove(closest);
        return true;
    }

    // ── Detecção ──────────────────────────────────────────────────────────

    private IEnumerator ScanLoop()
    {
        while (true) { _detector.Scan(); yield return null; }
    }

    private void OnEnter(Collider col, string label, int shapeIndex)
    {



        if (!IsRelevant(label)) return;

        TryToggleCharacterInteractPrompt(col, label, shouldShow: true);
        var interactable = ResolveInteractable(col);
        if (interactable == null) return;

        NotifyInteractionOutline(interactable, label, shapeIndex, shouldShow: true);
        if (_available.Contains(interactable)) return;

        _available.Add(interactable);
        LoggerService.PrintLogMessage(LogLevel.Debug, $"Interactable [{col.gameObject.name}] found");
    }

    private void OnExit(Collider col, string label, int shapeIndex)
    {
        TryToggleCharacterInteractPrompt(col, label, shouldShow: false);

        if (!IsRelevant(label)) return;

        var interactable = ResolveInteractable(col);
        if (interactable != null)
        {
            NotifyInteractionOutline(interactable, label, shapeIndex, shouldShow: false);
            _available.Remove(interactable);
            var characterSelectionNpc = interactable.GetComponent<CharacterSelectionNpc>();
            if (characterSelectionNpc != null && characterSelectionNpc._canvas != null)
            {
                characterSelectionNpc._canvas.Close();
            }
        }

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

    private void NotifyInteractionOutline(
        Interactable interactable,
        string shapeLabel,
        int shapeIndex,
        bool shouldShow)
    {
        var outline = interactable.GetComponent<InteractionOutline>()
                      ?? interactable.GetComponentInChildren<InteractionOutline>();

        if (outline == null) return;

        if (shouldShow)
            outline.RegisterPlayerProximity(_detector, shapeLabel, shapeIndex);
        else
            outline.UnregisterPlayerProximity(_detector, shapeLabel, shapeIndex);
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
