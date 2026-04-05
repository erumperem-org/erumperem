using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Erumperem.Combat
{
    /// <summary>
    /// Marcador 3D (ex.: cristal/losango) acima da cápsula sob o rato: instância, raycast, layer,
    /// punch e rotação DOTween. Não depende do <see cref="CombatPrototypeController"/>.
    /// </summary>
    public sealed class CombatHoverFocusMarker : MonoBehaviour
    {
        [Header("Prefab")]
        [Tooltip("Losango ou cristal acima da unidade sob o rato.")]
        [SerializeField] private GameObject markerPrefab;

        [Header("Raycast")]
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private float raycastMaxDistance = 200f;
        [SerializeField] private LayerMask raycastLayerMask = ~0;

        [Header("Posição")]
        [SerializeField] private float verticalOffsetAboveUnit = 0.35f;

        [Header("DOTween — aparecer")]
        [SerializeField] private float punchDuration = 0.35f;
        [SerializeField] private Vector3 punchScale = new(0.22f, 0.22f, 0.22f);
        [SerializeField] private int punchVibrato = 10;
        [SerializeField] private float punchElasticity = 0.45f;

        [Header("DOTween — rotação contínua")]
        [Tooltip("Segundos para uma volta completa no eixo Y.")]
        [SerializeField] private float spinPeriodSeconds = 3.5f;

        private GameObject _instance;
        private Vector3 _baseLocalScale = Vector3.one;
        private string _lastJuiceCombatantId;

        private void Awake()
        {
            if (raycastCamera == null)
            {
                raycastCamera = Camera.main;
            }
        }

        private void Start()
        {
            EnsureCreated();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void LateUpdate()
        {
            if (_instance == null || !isActiveAndEnabled)
            {
                return;
            }

            if (raycastCamera == null)
            {
                Hide();
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                Hide();
                return;
            }

            var ray = raycastCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, raycastMaxDistance, raycastLayerMask))
            {
                Hide();
                return;
            }

            var capsuleTag = hit.collider.GetComponentInParent<CombatCapsuleTag>();
            if (capsuleTag == null || string.IsNullOrEmpty(capsuleTag.combatantId) || !capsuleTag.isActiveAndEnabled)
            {
                Hide();
                return;
            }

            var unitRoot = capsuleTag.transform;
            if (!unitRoot.gameObject.activeInHierarchy)
            {
                Hide();
                return;
            }

            var unitRenderer = unitRoot.GetComponentInChildren<Renderer>();
            var topWorldY = unitRenderer != null
                ? unitRenderer.bounds.max.y
                : unitRoot.position.y + 1f;

            var markerPosition = unitRoot.position;
            markerPosition.y = topWorldY + verticalOffsetAboveUnit;

            PresentAt(markerPosition, capsuleTag.combatantId);
        }

        private void EnsureCreated()
        {
            if (markerPrefab == null || _instance != null)
            {
                return;
            }

            _instance = Instantiate(markerPrefab);
            _instance.name = "HoverFocusMarker";
            _baseLocalScale = _instance.transform.localScale;
            _instance.SetActive(false);

            var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer >= 0)
            {
                SetLayerRecursively(_instance, ignoreRaycastLayer);
            }
        }

        private void Hide()
        {
            _lastJuiceCombatantId = null;
            if (_instance != null)
            {
                var markerTransform = _instance.transform;
                markerTransform.DOKill();
                markerTransform.localScale = _baseLocalScale;
                markerTransform.localRotation = Quaternion.identity;
                _instance.SetActive(false);
            }
        }

        private void PresentAt(Vector3 worldPosition, string combatantId)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.SetActive(true);
            var markerTransform = _instance.transform;
            markerTransform.position = worldPosition;

            if (_lastJuiceCombatantId != combatantId)
            {
                _lastJuiceCombatantId = combatantId;
                PlayAppearJuice(markerTransform);
            }
        }

        private void PlayAppearJuice(Transform markerTransform)
        {
            markerTransform.DOKill();
            markerTransform.localScale = _baseLocalScale;
            markerTransform.localRotation = Quaternion.identity;

            markerTransform
                .DOPunchScale(punchScale, punchDuration, punchVibrato, punchElasticity)
                .SetLink(_instance);

            var spinDuration = Mathf.Max(0.05f, spinPeriodSeconds);
            markerTransform
                .DOLocalRotate(new Vector3(0f, 360f, 0f), spinDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental)
                .SetLink(_instance);
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            var transform = gameObject.transform;
            for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                SetLayerRecursively(transform.GetChild(childIndex).gameObject, layer);
            }
        }

        private void OnDestroy()
        {
            if (_instance != null)
            {
                _instance.transform.DOKill();
            }
        }
    }
}
