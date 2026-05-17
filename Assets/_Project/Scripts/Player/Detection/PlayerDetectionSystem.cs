using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DetectionSystem.Core;
using Services.DebugUtilities;
using UnityEngine;
using Player;

[RequireComponent(typeof(Detector))]
public class PlayerDetectionSystem : MonoBehaviour
{
    [SerializeField] private PlayableAnimationController animationController;
    [SerializeField] private PlayableCharacter playableCharacter;
    [SerializeField] public List<Interactable> availableInteractables;

    private Detector _detector;
    private Coroutine _scanCoroutine;

    private static readonly string[] DetectionAreas =
    {
        "InteractableDetectionArea",
        "CharactersDetectionArea"
    };

    private void Awake()
    {
        _detector = GetComponent<Detector>();
        _detector.OnDetectorEnter += OnDetectorEnter;
        _detector.OnDetectorExit += OnDetectorExit;
    }

    private void OnDisable() => StopScan();

    // ── Scan ──────────────────────────────────────────────────────────────

    public void Scan()
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

    private IEnumerator ScanLoop()
    {
        while (true)
        {
            _detector.Scan();
            yield return null;
        }
    }

    // ── Detecção ──────────────────────────────────────────────────────────

    private void OnDetectorEnter(Collider collider, string shapeLabel, int shapeIndex)
    {
        if (!IsRelevantArea(shapeLabel)) return;

        var interactable = collider.gameObject.GetComponent<Interactable>();
        if (interactable == null || availableInteractables.Contains(interactable)) return;
        availableInteractables.Add(interactable);
        LoggerService.PrintLogMessage(LogLevel.Debug, $"Interactable [{collider.gameObject.name}] found");
    }

    private void OnDetectorExit(Collider collider, string shapeLabel, int shapeIndex)
    {
        if (!IsRelevantArea(shapeLabel)) return;

        var interactable = collider.gameObject.GetComponent<Interactable>();
        if (interactable == null) return;

        availableInteractables.Remove(interactable);
        LoggerService.PrintLogMessage(LogLevel.Debug, $"Interactable [{collider.gameObject.name}] lost");
    }

    private static bool IsRelevantArea(string label) =>
        System.Array.IndexOf(DetectionAreas, label) >= 0;

    // ── Interação ─────────────────────────────────────────────────────────

    public void Interact()
    {
        availableInteractables.RemoveAll(t => t == null);

        if (availableInteractables.Count == 0) return;

        Interactable closest = availableInteractables
            .OrderBy(t => (transform.position - t.transform.position).sqrMagnitude)
            .FirstOrDefault();

        if (closest == null) return;

        if (!closest.CanInteract)
        {
            availableInteractables.Remove(closest);
            return;
        }

        TriggerAnimation(closest);
        closest.ExecuteInteraction(playableCharacter.movementController);

        if (!closest.CanInteract)
            availableInteractables.Remove(closest);
    }

    private void TriggerAnimation(Interactable interactable)
    {
        switch (interactable)
        {
            case DoorInteractable door:
                if (door.opened) animationController.TriggerClosingDoor();
                else animationController.TriggerOpeningDoor();
                break;

            case ChestInteractable:
                animationController.TriggerOpeningChest();
                break;
        }
    }
}
