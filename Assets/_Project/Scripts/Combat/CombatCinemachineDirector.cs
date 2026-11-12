using System.Collections.Generic;
using DG.Tweening;
using Game.Core.Presentation;
using Unity.Cinemachine;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Câmera do Action Shot: Actor Focus (blend suave até o ator), Target Focus (snap quase seco
    /// enquadrando todos os afetados) e Return (blend suave para o plano geral).
    /// A câmera de zoom não tem Body/Aim: a pose é calculada aqui, mantendo a rotação autorada no prefab.
    /// </summary>
    public sealed class CombatCinemachineDirector : MonoBehaviour
    {
        private const string TargetSnapTweenId = "CombatActionShotTargetSnap";

        [Tooltip("Plano geral da batalha (Follow/LookAt = empty ou rig da arena no Inspector).")]
        [SerializeField] private CinemachineCamera wideBattleCamera;

        [Tooltip("Close-up do Action Shot. Sem Body/Aim; a rotação autorada define a direção do olhar.")]
        [SerializeField] private CinemachineCamera actionZoomCamera;

        [Tooltip("Brain da Main Camera. Vazio = tenta Camera.main.")]
        [SerializeField] private CinemachineBrain cinemachineBrain;

        [Header("Prioridades")]
        [SerializeField] private int wideBattlePriority = 10;
        [SerializeField] private int actionZoomPriorityActive = 20;
        [SerializeField] private int actionZoomPriorityIdle;

        [Header("Enquadramento")]
        [Tooltip("Altura acima do pivot do combatente que fica no centro da tela (ajuste até o ator ficar centralizado).")]
        [SerializeField] private float lookHeight = 1.2f;

        [Tooltip("Distância da câmera ao ator no Actor Focus. Menor = zoom mais forte.")]
        [SerializeField] private float actorShotDistance = 3f;

        [Tooltip("Distância mínima no Target Focus (um único alvo).")]
        [SerializeField] private float targetShotMinimumDistance = 7f;

        [Tooltip("Margem multiplicativa para caber todos os afetados (1 = colado na borda).")]
        [SerializeField] private float targetShotPadding = 1.35f;

        [Header("Tempos (divididos pela velocidade de combate)")]
        [SerializeField] private float actorFocusBlendSeconds = 0.45f;
        [SerializeField] private float targetSnapSeconds = 0.06f;
        [SerializeField] private float returnBlendSeconds = 0.6f;

        public bool IsConfigured => wideBattleCamera != null && actionZoomCamera != null;

        private Quaternion _shotRotation;
        private CinemachineBlendDefinition _savedDefaultBlend;
        private bool _brainBlendOverrideActive;

        private void Awake()
        {
            if (cinemachineBrain == null && Camera.main != null)
            {
                cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
            }

            if (wideBattleCamera != null)
            {
                wideBattleCamera.Priority = wideBattlePriority;
            }

            if (actionZoomCamera != null)
            {
                _shotRotation = actionZoomCamera.transform.rotation;
                actionZoomCamera.Priority = actionZoomPriorityIdle;
            }
        }

        private void OnDisable()
        {
            DOTween.Kill(TargetSnapTweenId, false);
            if (actionZoomCamera != null)
            {
                actionZoomCamera.Priority = actionZoomPriorityIdle;
            }

            RestoreBrainBlend();
        }

        /// <summary>Passo 1: blend suave do plano geral até o ator.</summary>
        public void BeginActorFocus(Transform actorVisualRoot)
        {
            if (actionZoomCamera == null || actorVisualRoot == null)
            {
                return;
            }

            DOTween.Kill(TargetSnapTweenId, false);
            var lookPoint = actorVisualRoot.position + Vector3.up * lookHeight;
            actionZoomCamera.transform.SetPositionAndRotation(
                lookPoint - _shotRotation * Vector3.forward * actorShotDistance,
                _shotRotation);

            SetBrainBlend(actorFocusBlendSeconds);
            actionZoomCamera.Priority = actionZoomPriorityActive;
        }

        /// <summary>Passo 3: snap quase seco enquadrando todos os combatentes afetados.</summary>
        public void BeginTargetFocus(IReadOnlyList<Transform> affectedVisualRoots)
        {
            if (actionZoomCamera == null || affectedVisualRoots == null || affectedVisualRoots.Count == 0)
            {
                return;
            }

            var points = new List<System.Numerics.Vector3>(affectedVisualRoots.Count);
            foreach (var root in affectedVisualRoots)
            {
                if (root == null)
                {
                    continue;
                }

                var lookPoint = root.position + Vector3.up * lookHeight;
                points.Add(new System.Numerics.Vector3(lookPoint.x, lookPoint.y, lookPoint.z));
            }

            if (points.Count == 0)
            {
                return;
            }

            var forward = _shotRotation * Vector3.forward;
            var up = _shotRotation * Vector3.up;
            var distance = ActionShotFraming.RequiredDistance(
                points,
                new System.Numerics.Vector3(forward.x, forward.y, forward.z),
                new System.Numerics.Vector3(up.x, up.y, up.z),
                actionZoomCamera.Lens.FieldOfView,
                ResolveAspect(),
                targetShotPadding,
                targetShotMinimumDistance);
            var center = ActionShotFraming.Center(points);
            var shotPosition = new Vector3(center.X, center.Y, center.Z) - forward * distance;

            DOTween.Kill(TargetSnapTweenId, false);
            actionZoomCamera.transform
                .DOMove(shotPosition, targetSnapSeconds / SpeedMultiplier)
                .SetEase(Ease.OutCubic)
                .SetId(TargetSnapTweenId)
                .SetLink(actionZoomCamera.gameObject);
        }

        /// <summary>Passo 5: blend suave de volta ao plano geral.</summary>
        public void EndActionFocus()
        {
            if (actionZoomCamera == null)
            {
                return;
            }

            SetBrainBlend(returnBlendSeconds);
            actionZoomCamera.Priority = actionZoomPriorityIdle;
            if (wideBattleCamera != null)
            {
                wideBattleCamera.Priority = wideBattlePriority;
            }
        }

        private static float SpeedMultiplier => Mathf.Max(0.2f, CombatSpeedSettings.SpeedMultiplier);

        private float ResolveAspect()
        {
            var outputCamera = cinemachineBrain != null ? cinemachineBrain.OutputCamera : Camera.main;
            return outputCamera != null ? outputCamera.aspect : 16f / 9f;
        }

        // O Brain lê o DefaultBlend no frame em que a câmera ativa muda, por isso ele fica sobrescrito
        // durante o combate e só é restaurado ao desativar o diretor.
        private void SetBrainBlend(float seconds)
        {
            if (cinemachineBrain == null)
            {
                return;
            }

            if (!_brainBlendOverrideActive)
            {
                _savedDefaultBlend = cinemachineBrain.DefaultBlend;
                _brainBlendOverrideActive = true;
            }

            cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                Mathf.Max(0f, seconds / SpeedMultiplier));
        }

        private void RestoreBrainBlend()
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
