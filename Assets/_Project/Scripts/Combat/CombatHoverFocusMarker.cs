using DG.Tweening;
using Erumperem.Combat.Runtime;
using UnityEngine;

namespace Erumperem.Combat
{
	public enum HoverMarkerSpinAxis
	{
		WorldY,
		LocalZ,
	}

	/// <summary>
	/// Marker displayed above hovered combatants.
	/// Supports allies and enemies.
	/// Preserves prefab local rotation.
	/// </summary>
	public sealed class CombatHoverFocusMarker : MonoBehaviour
	{
		[Header("Prefab")]
		[SerializeField] private GameObject markerPrefab;

		[Header("Raycast")]
		[SerializeField] private Camera raycastCamera;
		[SerializeField] private float raycastMaxDistance = 200f;
		[SerializeField] private LayerMask raycastLayerMask = ~0;

		[Header("Position Offset")]
		[SerializeField] private Vector3 markerOffset = new Vector3(0f, 0.35f, 0f);

		[Header("Target Filtering")]
		[SerializeField] private bool showOnEnemies = true;

		[Header("DOTween - Appear")]
		[SerializeField] private float punchDuration = 0.35f;
		[SerializeField] private Vector3 punchScale = new(0.22f, 0.22f, 0.22f);
		[SerializeField] private int punchVibrato = 10;
		[SerializeField] private float punchElasticity = 0.45f;

		[Header("DOTween - Rotation")]
		[SerializeField] private HoverMarkerSpinAxis spinAxis = HoverMarkerSpinAxis.WorldY;
		[SerializeField] private float spinPeriodSeconds = 3.5f;

		private GameObject _instance;
		private Vector3 _baseLocalScale = Vector3.one;
		private Quaternion _baseLocalRotation = Quaternion.identity;
		private string _lastCombatantId;
		private readonly CombatPointerRaycastService _pointerRaycast = new();

		private void Awake()
		{
			_pointerRaycast.Configure(
				raycastCamera != null ? raycastCamera : Camera.main,
				raycastMaxDistance,
				raycastLayerMask);
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
				return;

			if (!_pointerRaycast.TryRaycastCombatCapsuleTagFromInputManager(out var capsuleTag))
			{
				Hide();
				return;
			}

			if (string.IsNullOrEmpty(capsuleTag.combatantId) ||
				!capsuleTag.isActiveAndEnabled)
			{
				Hide();
				return;
			}

			if (!showOnEnemies &&
				capsuleTag.combatantId.StartsWith("enemy",
					System.StringComparison.OrdinalIgnoreCase))
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

			var topWorldY = CombatUnitColliderVerticalExtents.TryGetTopWorldY(
				unitRoot,
				out var colliderTopWorldY)
				? colliderTopWorldY
				: unitRoot.position.y;

			var markerPosition = unitRoot.position;
			markerPosition.y = topWorldY;
			markerPosition += markerOffset;

			PresentAt(markerPosition, capsuleTag.combatantId);
		}

		private void EnsureCreated()
		{
			if (_instance != null || markerPrefab == null)
				return;

			_instance = Instantiate(markerPrefab);
			_instance.name = "HoverFocusMarker";

			_baseLocalScale = _instance.transform.localScale;
			_baseLocalRotation = _instance.transform.localRotation;

			_instance.SetActive(false);

			var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

			if (ignoreRaycastLayer >= 0)
				SetLayerRecursively(_instance, ignoreRaycastLayer);
		}

		private void PresentAt(Vector3 position, string combatantId)
		{
			_instance.SetActive(true);

			var markerTransform = _instance.transform;
			markerTransform.position = position;

			if (_lastCombatantId == combatantId)
				return;

			_lastCombatantId = combatantId;

			PlayAppearJuice(markerTransform);

			if (AudioManager.instance != null)
				AudioManager.instance.PlaySFX("CharacterHover");
		}

		private void Hide()
		{
			_lastCombatantId = null;

			if (_instance == null)
				return;

			var markerTransform = _instance.transform;

			markerTransform.DOKill();
			markerTransform.localScale = _baseLocalScale;
			markerTransform.localRotation = _baseLocalRotation;

			_instance.SetActive(false);
		}

		private void PlayAppearJuice(Transform markerTransform)
		{
			markerTransform.DOKill();

			markerTransform.localScale = _baseLocalScale;
			markerTransform.localRotation = _baseLocalRotation;

			markerTransform
				.DOPunchScale(
					punchScale,
					punchDuration,
					punchVibrato,
					punchElasticity)
				.SetLink(_instance);

			var spinDuration = Mathf.Max(0.05f, spinPeriodSeconds);

			if (spinAxis == HoverMarkerSpinAxis.WorldY)
			{
				markerTransform
					.DORotate(
						new Vector3(0f, 360f, 0f),
						spinDuration,
						RotateMode.WorldAxisAdd)
					.SetEase(Ease.Linear)
					.SetLoops(-1, LoopType.Incremental)
					.SetLink(_instance);
			}
			else
			{
				markerTransform
					.DOLocalRotate(
						new Vector3(0f, 0f, 360f),
						spinDuration,
						RotateMode.LocalAxisAdd)
					.SetEase(Ease.Linear)
					.SetLoops(-1, LoopType.Incremental)
					.SetLink(_instance);
			}
		}

		private static void SetLayerRecursively(GameObject obj, int layer)
		{
			obj.layer = layer;

			var transform = obj.transform;

			for (int i = 0; i < transform.childCount; i++)
			{
				SetLayerRecursively(transform.GetChild(i).gameObject, layer);
			}
		}

		private void OnDestroy()
		{
			if (_instance != null)
				_instance.transform.DOKill();
		}
	}
}