using Unity.Cinemachine;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Wide + zoom de ação. Zoom usa pivot na posição do ator + offset mundo (ex. +15 X);
    /// <see cref="CinemachineCamera"/> foca o ator (LookAt). Opcional: blend do Brain mais rápido durante a ação.
    /// </summary>
    public sealed class CombatCinemachineDirector : MonoBehaviour
    {
        [Tooltip("Plano geral da batalha (Follow/LookAt = empty ou rig da arena no Inspector).")]
        [SerializeField] private CinemachineCamera wideBattleCamera;

        [Tooltip("Close-up reutilizado: Follow = pivot dinâmico; LookAt = ator (ou alvo).")]
        [SerializeField] private CinemachineCamera actionZoomCamera;

        [Tooltip("Brain da Main Camera. Vazio = tenta Camera.main.")]
        [SerializeField] private CinemachineBrain cinemachineBrain;

        [Header("Posição da câmera de ação")]
        [Tooltip("Offset mundo aplicado à posição do ator onde fica o pivot da câmera (ex.: +15 em X).")]
        [SerializeField] private Vector3 actionCameraWorldOffsetFromActor = new(15f, 0f, 0f);

        [Header("Prioridades")]
        [SerializeField] private int wideBattlePriority = 10;
        [SerializeField] private int actionZoomPriorityActive = 20;
        [SerializeField] private int actionZoomPriorityIdle;

        [Header("Velocidade do blend")]
        [Tooltip("Se ligado, DefaultBlend do Brain usa duração curta ao entrar/sair da câmera de ação.")]
        [SerializeField] private bool useFastActionBlends = true;

        [Tooltip("Duração (s) do blend para/de câmera de ação. Menor = transição mais rápida.")]
        [SerializeField] private float actionBlendDurationSeconds = 0.12f;

        public bool IsConfigured => wideBattleCamera != null && actionZoomCamera != null;

        private Transform _actionCameraPivot;
        private CinemachineBlendDefinition _savedDefaultBlend;
        private bool _brainBlendOverrideActive;

        private void Awake()
        {
            if (cinemachineBrain == null && Camera.main != null)
            {
                cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
            }

            EnsureActionCameraPivot();

            if (wideBattleCamera != null)
            {
                wideBattleCamera.Priority = wideBattlePriority;
            }

            if (actionZoomCamera != null)
            {
                actionZoomCamera.Priority = actionZoomPriorityIdle;
            }
        }

        private void OnDisable()
        {
            EndActionFocus();
        }

        private void EnsureActionCameraPivot()
        {
            if (_actionCameraPivot != null)
            {
                return;
            }

            var pivotObject = new GameObject("CombatActionCameraPivot");
            pivotObject.transform.SetParent(transform, false);
            _actionCameraPivot = pivotObject.transform;
        }

        /// <summary>
        /// Pivot na posição do ator + offset mundo; LookAt no ator ou override.
        /// </summary>
        public void BeginActionFocus(Transform actorVisualRoot, Transform lookAtOverride)
        {
            if (actionZoomCamera == null || actorVisualRoot == null)
            {
                return;
            }

            EnsureActionCameraPivot();

            _actionCameraPivot.position = actorVisualRoot.position + actionCameraWorldOffsetFromActor;
            _actionCameraPivot.rotation = Quaternion.identity;

            actionZoomCamera.Follow = _actionCameraPivot;
            actionZoomCamera.LookAt = lookAtOverride != null ? lookAtOverride : actorVisualRoot;

            ApplyFastBrainBlendIfNeeded();

            actionZoomCamera.Priority = actionZoomPriorityActive;
        }

        public void EndActionFocus()
        {
            RestoreBrainBlendIfNeeded();

            if (actionZoomCamera == null)
            {
                return;
            }

            actionZoomCamera.Priority = actionZoomPriorityIdle;
            if (wideBattleCamera != null)
            {
                wideBattleCamera.Priority = wideBattlePriority;
            }
        }

        private void ApplyFastBrainBlendIfNeeded()
        {
            if (!useFastActionBlends || cinemachineBrain == null || _brainBlendOverrideActive)
            {
                return;
            }

            _savedDefaultBlend = cinemachineBrain.DefaultBlend;
            var fastBlend = new CinemachineBlendDefinition(
                _savedDefaultBlend.Style,
                Mathf.Max(0f, actionBlendDurationSeconds));
            cinemachineBrain.DefaultBlend = fastBlend;
            _brainBlendOverrideActive = true;
        }

        private void RestoreBrainBlendIfNeeded()
        {
            if (!_brainBlendOverrideActive || cinemachineBrain == null)
            {
                return;
            }

            cinemachineBrain.DefaultBlend = _savedDefaultBlend;
            _brainBlendOverrideActive = false;
        }
    }
}
