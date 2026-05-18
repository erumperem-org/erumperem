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

        if (_detectionReceiver != null)
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

        if (_detectionReceiver != null)
        {
            _detectionReceiver.OnEnter -= HandleDetectorEnteredThisObject;
            _detectionReceiver.OnExit -= HandleDetectorExitedThisObject;
        }

        _collidersInsideCount = 0;
        SetPromptActive(false);
    }

    private void LateUpdate()
    {
        if (!faceMainCamera || promptRoot == null || !promptRoot.activeSelf)
        {
            return;
        }

        var mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        promptRoot.transform.rotation = mainCamera.transform.rotation;
    }

    private void HandleColliderEntered(Collider otherCollider, string shapeLabel, int shapeIndex)
    {
        _collidersInsideCount++;
        SetPromptActive(true);
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
        _collidersInsideCount++;
        SetPromptActive(true);
    }

    private void HandleDetectorExitedThisObject(Detector detector, string shapeLabel, int shapeIndex)
    {
        _collidersInsideCount = Mathf.Max(0, _collidersInsideCount - 1);

        if (_collidersInsideCount == 0)
        {
            SetPromptActive(false);
        }
    }

    private void SetPromptActive(bool isActive)
    {
        if (promptRoot == null || promptRoot.activeSelf == isActive)
        {
            return;
        }

        promptRoot.SetActive(isActive);
    }
}
