using DetectionSystem.Core;
using UnityEngine;

/// <summary>
/// Ativa/desativa um filho (ex.: Canvas World Space + TMP) quando algo entra ou sai da deteção.
/// <para>
/// <b>Interactável (setup atual):</b> <see cref="DetectionReceiver"/> — o jogador com
/// <see cref="DetectionComponent"/> entra na shape e este objeto recebe OnEnter/OnExit.
/// </para>
/// <para>
/// <b>Zona local:</b> <see cref="DetectionComponent"/> + <see cref="Detector"/> no mesmo
/// GameObject — enter/exit vêm de <see cref="Detector.OnDetectorEnter"/> / OnDetectorExit.
/// </para>
/// </summary>
public class DetectionPromptToggle : MonoBehaviour
{
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private bool faceMainCamera = true;
    [SerializeField] private Vector3 promptOffsetAboveColliderTop = new(0f, 0.25f, 0f);

    private const float FallbackPromptLocalHeight = 2f;

    private Detector _localDetector;
    private DetectionReceiver _detectionReceiver;
    private int _collidersInsideCount;

    private void Awake()
    {
        _localDetector = GetComponent<Detector>();
        _detectionReceiver = GetComponent<DetectionReceiver>();

        if (_localDetector == null && _detectionReceiver == null)
        {
            Debug.LogWarning(
                $"{nameof(DetectionPromptToggle)} em '{name}' precisa de {nameof(Detector)} ou {nameof(DetectionReceiver)}.",
                this);
        }

        if (promptRoot != null)
        {
            ApplyPromptAnchorLocalPosition();
            promptRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (_localDetector != null)
        {
            _localDetector.OnDetectorEnter += HandleColliderEntered;
            _localDetector.OnDetectorExit += HandleColliderExited;
        }

        if (_detectionReceiver != null && _detectionReceiver is not PlayableDetectionReceiver)
        {
            _detectionReceiver.OnEnter += HandleDetectorEnteredThisObject;
            _detectionReceiver.OnExit += HandleDetectorExitedThisObject;
        }
    }

    private void OnDisable()
    {
        if (_localDetector != null)
        {
            _localDetector.OnDetectorEnter -= HandleColliderEntered;
            _localDetector.OnDetectorExit -= HandleColliderExited;
        }

        if (_detectionReceiver != null && _detectionReceiver is not PlayableDetectionReceiver)
        {
            _detectionReceiver.OnEnter -= HandleDetectorEnteredThisObject;
            _detectionReceiver.OnExit -= HandleDetectorExitedThisObject;
        }

        _collidersInsideCount = 0;
        SetPromptActive(false);
    }

    /// <summary>
    /// Chamado pelo <see cref="PlayerDetectionSystem"/> quando o Main entra na área de personagem.
    /// </summary>
    public void RegisterPlayerProximity()
    {
        _collidersInsideCount++;
        SetPromptActive(true);
    }

    /// <summary>
    /// Chamado pelo <see cref="PlayerDetectionSystem"/> quando o Main sai da área de personagem.
    /// </summary>
    public void UnregisterPlayerProximity()
    {
        _collidersInsideCount = Mathf.Max(0, _collidersInsideCount - 1);

        if (_collidersInsideCount == 0)
        {
            SetPromptActive(false);
        }
    }

    private void LateUpdate()
    {
        if (!faceMainCamera || promptRoot == null || !promptRoot.activeInHierarchy)
        {
            return;
        }

        var mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        var promptTransform = promptRoot.transform;
        var cameraTransform = mainCamera.transform;
        promptTransform.rotation = Quaternion.LookRotation(
            promptTransform.position - cameraTransform.position,
            cameraTransform.up);
    }

    private void HandleColliderEntered(Collider otherCollider, string shapeLabel, int shapeIndex)
    {
        if (this.tag != "Player")
        {
            _collidersInsideCount++;
            SetPromptActive(true);
        }
    }

    private void HandleColliderExited(Collider otherCollider, string shapeLabel, int shapeIndex)
    {
        _collidersInsideCount = Mathf.Max(0, _collidersInsideCount - 1);

        if (_collidersInsideCount == 0)
        {
            SetPromptActive(false);
        }
    }

    private void HandleDetectorEnteredThisObject(Detector detector, string shapeLabel, int shapeIndex)
    {
        if (!ShouldShowPromptForDetection(detector, shapeLabel))
        {
            return;
        }

        _collidersInsideCount++;
        SetPromptActive(true);
    }

    private void HandleDetectorExitedThisObject(Detector detector, string shapeLabel, int shapeIndex)
    {
        if (!ShouldShowPromptForDetection(detector, shapeLabel))
        {
            return;
        }

        _collidersInsideCount = Mathf.Max(0, _collidersInsideCount - 1);

        if (_collidersInsideCount == 0)
        {
            SetPromptActive(false);
        }
    }

    private bool ShouldShowPromptForDetection(Detector detector, string shapeLabel)
    {
        if (_detectionReceiver is PlayableDetectionReceiver)
        {
            return detector != null
                   && detector.gameObject.CompareTag("Player")
                   && shapeLabel == "CharactersDetectionArea";
        }

        return true;
    }

    private void SetPromptActive(bool isActive)
    {
        if (promptRoot == null)
        {
            return;
        }

        if (isActive)
        {
            EnsurePromptHierarchyActive();
            ApplyPromptAnchorLocalPosition();
        }

        if (promptRoot.activeSelf == isActive)
        {
            return;
        }

        promptRoot.SetActive(isActive);

        if (isActive)
        {
            EnsurePromptTextChildrenActive();
        }
    }

    private void ApplyPromptAnchorLocalPosition()
    {
        if (promptRoot == null)
        {
            return;
        }

        var promptTransform = promptRoot.transform;
        if (promptTransform.parent != transform)
        {
            return;
        }

        var localTopY = TryGetLocalColliderTopY(out var colliderTopLocalY)
            ? colliderTopLocalY
            : FallbackPromptLocalHeight;

        var anchoredLocalPosition = new Vector3(
            promptOffsetAboveColliderTop.x,
            localTopY + promptOffsetAboveColliderTop.y,
            promptOffsetAboveColliderTop.z);

        if (promptTransform is RectTransform promptRectTransform)
        {
            promptRectTransform.localPosition = anchoredLocalPosition;
            promptRectTransform.anchoredPosition = new Vector2(
                anchoredLocalPosition.x,
                anchoredLocalPosition.y);
        }
        else
        {
            promptTransform.localPosition = anchoredLocalPosition;
        }
    }

    private bool TryGetLocalColliderTopY(out float localTopY)
    {
        localTopY = 0f;

        var characterColliders = GetComponentsInChildren<Collider>(includeInactive: false);
        var foundCollider = false;

        foreach (var characterCollider in characterColliders)
        {
            if (characterCollider == null || !characterCollider.enabled || characterCollider.isTrigger)
            {
                continue;
            }

            var colliderTopLocalY = transform.InverseTransformPoint(characterCollider.bounds.max).y;
            localTopY = foundCollider ? Mathf.Max(localTopY, colliderTopLocalY) : colliderTopLocalY;
            foundCollider = true;
        }

        return foundCollider;
    }

    private void EnsurePromptHierarchyActive()
    {
        var canvas = promptRoot.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = promptRoot.GetComponentInParent<Canvas>(includeInactive: true);
        }

        if (canvas != null && !canvas.gameObject.activeSelf)
        {
            canvas.gameObject.SetActive(true);
        }
    }

    private void EnsurePromptTextChildrenActive()
    {
        var textComponents = promptRoot.GetComponentsInChildren<TMPro.TMP_Text>(includeInactive: true);
        foreach (var textComponent in textComponents)
        {
            if (!textComponent.gameObject.activeSelf)
            {
                textComponent.gameObject.SetActive(true);
            }
        }
    }
}
