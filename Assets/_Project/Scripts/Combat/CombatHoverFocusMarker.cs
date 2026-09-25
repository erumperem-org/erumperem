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
	/// Marcador e feedback sonoro exibido sobre combatentes focados.
	/// Suporta canal de Mouse e canal de Teclado/Gamepad de forma unificada e sem conflito de frames.
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

		public Vector3 MarkerOffset => markerOffset;

		public bool IsHighlighting(string combatantId) => isActiveAndEnabled
			&& _instance != null && _instance.activeInHierarchy
			&& string.Equals(_lastCombatantId, combatantId, System.StringComparison.Ordinal);

		private GameObject _instance;
		private Vector3 _baseLocalScale = Vector3.one;
		private Quaternion _baseLocalRotation = Quaternion.identity;
		private string _lastCombatantId;
		private readonly CombatPointerRaycastService _pointerRaycast = new();

		// Canal Externo (Teclado / Gamepad)
		private bool _hasExternalTarget;
		private Vector3 _externalPosition;
		private string _externalCombatantId;

		// Filtros acústicos anti-spam e histerese de borda
		private string _lastAudioCombatantId;
		private float _lastAudioPlayTime = -1f;
		private float _lastExitTime = -1f;
		private const float MinAudioInterval = 0.08f;
		private const float ReenterGracePeriod = 0.35f;

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
			ClearExternalTarget();
		}

		private void LateUpdate()
		{
			EnsureCreated();

			if (_instance == null || !isActiveAndEnabled)
				return;

			// 1. Canal do Mouse: se o cursor estiver sobre uma unidade válida, tem prioridade
			if (_pointerRaycast.TryRaycastCombatCapsuleTagFromInputManager(out var capsuleTag) &&
			    !string.IsNullOrEmpty(capsuleTag.combatantId) &&
			    capsuleTag.isActiveAndEnabled)
			{
				if (showOnEnemies || !capsuleTag.combatantId.StartsWith("enemy", System.StringComparison.OrdinalIgnoreCase))
				{
					var unitRoot = capsuleTag.transform;
					if (unitRoot.gameObject.activeInHierarchy)
					{
						var topWorldY = CombatUnitColliderVerticalExtents.TryGetTopWorldY(
							unitRoot,
							out var colliderTopWorldY)
							? colliderTopWorldY
							: unitRoot.position.y;

						var markerPosition = unitRoot.position;
						markerPosition.y = topWorldY;
						markerPosition += markerOffset;

						PresentAt(markerPosition, capsuleTag.combatantId);
						return;
					}
				}
			}

			// 2. Canal Externo (Teclado / Gamepad): se o mouse está no vazio, mantém o alvo do teclado
			if (_hasExternalTarget && !string.IsNullOrEmpty(_externalCombatantId))
			{
				PresentAt(_externalPosition, _externalCombatantId);
				return;
			}

			// 3. Nenhum alvo sob o mouse e nenhum alvo ativo no teclado: oculta o marcador
			Hide();
		}

		public void PresentExternal(Vector3 position, string combatantId)
		{
			_hasExternalTarget = true;
			_externalPosition = position;
			_externalCombatantId = combatantId;
		}

		public void ClearExternalTarget()
		{
			_hasExternalTarget = false;
			_externalCombatantId = null;
		}

		public void EnsureCreated()
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

		public void PresentAt(Vector3 position, string combatantId)
		{
			EnsureCreated();

			_instance.SetActive(true);

			var markerTransform = _instance.transform;
			markerTransform.position = position;

			if (string.Equals(_lastCombatantId, combatantId, System.StringComparison.Ordinal))
				return;

			_lastCombatantId = combatantId;

			PlayAppearJuice(markerTransform);
			PlayHoverAudio(combatantId);
		}

		public void Hide()
		{
			_lastCombatantId = null;
			_lastExitTime = Time.unscaledTime;

			if (_instance == null)
				return;

			var markerTransform = _instance.transform;

			markerTransform.DOKill();
			markerTransform.localScale = _baseLocalScale;
			markerTransform.localRotation = _baseLocalRotation;

			_instance.SetActive(false);
		}

		private void PlayHoverAudio(string combatantId)
		{
			float now = Time.unscaledTime;

			// Histerese de borda: ignora oscilações na borda do colisor dentro de 0.35s
			if (string.Equals(_lastAudioCombatantId, combatantId, System.StringComparison.Ordinal) &&
			    (now - _lastExitTime) < ReenterGracePeriod)
			{
				return;
			}

			// Cadência mínima absoluta entre sons
			if (now - _lastAudioPlayTime < MinAudioInterval)
			{
				return;
			}

			_lastAudioCombatantId = combatantId;
			_lastAudioPlayTime = now;

			if (AudioManager.instance != null)
			{
				AudioManager.instance.PlaySFX("CharacterHover");
			}
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

//fiz umas mudanças nesses códigos de hover marker p evitar uns erros de áudio que estavam acontecendo
