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
        [Tooltip("Sem mesh encontrada: altura acima do pivot do combatente para onde a câmera mira.")]
        [SerializeField] private float lookHeight = 1.2f;

        [Tooltip("Sem mesh encontrada: distância da câmera ao ator no Actor Focus.")]
        [SerializeField] private float actorShotDistance = 3.2f;

        [Tooltip("FOV vertical no Actor Focus. Menor = zoom mais forte (lente tele).")]
        [SerializeField, Range(10f, 90f)] private float actorShotFieldOfView = 32f;

        [Tooltip("Margem multiplicativa sobre o bounding box da mesh do ator aliado (1 = colado na borda).")]
        [SerializeField] private float actorShotPadding = 1.1f;

        [Tooltip("Sem mesh encontrada: distância no Actor Focus quando o ator é inimigo.")]
        [SerializeField] private float enemyActorShotDistance = 4.2f;

        [SerializeField, Range(10f, 90f)] private float enemyActorShotFieldOfView = 40f;

        [Tooltip("Actor Focus quando o ator é inimigo: um pouco menos de zoom que o dos aliados.")]
        [SerializeField] private float enemyActorShotPadding = 1.25f;

        [Tooltip("Distância mínima no Actor Focus, para combatentes minúsculos não encostarem na lente.")]
        [SerializeField] private float actorShotMinimumDistance = 1f;

        [Tooltip("FOV vertical no Target Focus; usado também no cálculo de enquadramento dos afetados.")]
        [SerializeField, Range(10f, 90f)] private float targetShotFieldOfView = 55f;

        [Tooltip("Distância mínima no Target Focus. O tamanho real vem do bounding box das meshes dos afetados.")]
        [SerializeField] private float targetShotMinimumDistance = 2f;

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
        public void BeginActorFocus(Transform actorVisualRoot, bool isEnemyActor)
        {
            if (actionZoomCamera == null || actorVisualRoot == null)
            {
                return;
            }

            DOTween.Kill(TargetSnapTweenId, false);
            SetZoomCameraFieldOfView(isEnemyActor ? enemyActorShotFieldOfView : actorShotFieldOfView);

            // O centro do enquadramento fica exatamente no eixo da câmera, então o ator aparece centralizado na tela.
            Vector3 shotPosition;
            if (TryGetMeshBounds(actorVisualRoot, out var bounds))
            {
                var points = new List<System.Numerics.Vector3>(8);
                AddBoundsCorners(bounds, points);
                shotPosition = FramePoints(
                    points,
                    isEnemyActor ? enemyActorShotPadding : actorShotPadding,
                    actorShotMinimumDistance);
            }
            else
            {
                var lookPoint = actorVisualRoot.position + Vector3.up * lookHeight;
                shotPosition = lookPoint
                    - _shotRotation * Vector3.forward * (isEnemyActor ? enemyActorShotDistance : actorShotDistance);
            }

            actionZoomCamera.transform.SetPositionAndRotation(shotPosition, _shotRotation);

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

            var points = new List<System.Numerics.Vector3>(affectedVisualRoots.Count * 8);
            foreach (var root in affectedVisualRoots)
            {
                if (root == null)
                {
                    continue;
                }

                if (TryGetMeshBounds(root, out var bounds))
                {
                    AddBoundsCorners(bounds, points);
                }
                else
                {
                    var lookPoint = root.position + Vector3.up * lookHeight;
                    points.Add(new System.Numerics.Vector3(lookPoint.x, lookPoint.y, lookPoint.z));
                }
            }

            if (points.Count == 0)
            {
                return;
            }

            SetZoomCameraFieldOfView(targetShotFieldOfView);
            var shotPosition = FramePoints(points, targetShotPadding, targetShotMinimumDistance);

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

        /// <summary>Posição da câmera (rotação fixa do prefab) que enquadra todos os pontos com o FOV atual.</summary>
        private Vector3 FramePoints(List<System.Numerics.Vector3> points, float padding, float minimumDistance)
        {
            var forward = _shotRotation * Vector3.forward;
            var up = _shotRotation * Vector3.up;
            var distance = ActionShotFraming.RequiredDistance(
                points,
                new System.Numerics.Vector3(forward.x, forward.y, forward.z),
                new System.Numerics.Vector3(up.x, up.y, up.z),
                actionZoomCamera.Lens.FieldOfView,
                ResolveAspect(),
                padding,
                minimumDistance);
            var center = ActionShotFraming.Center(points);
            return new Vector3(center.X, center.Y, center.Z) - forward * distance;
        }

        // Prefere SkinnedMeshRenderer (o corpo do combatente) para não incluir acessórios estáticos filhos do
        // visual root, como o marcador de turno; sem skinned mesh, usa os MeshRenderer.
        private static bool TryGetMeshBounds(Transform root, out Bounds bounds)
        {
            return TryEncapsulate(root.GetComponentsInChildren<SkinnedMeshRenderer>(), out bounds)
                || TryEncapsulate(root.GetComponentsInChildren<MeshRenderer>(), out bounds);
        }

        private static bool TryEncapsulate<TRenderer>(TRenderer[] renderers, out Bounds bounds)
            where TRenderer : Renderer
        {
            bounds = default;
            var found = false;
            foreach (var meshRenderer in renderers)
            {
                if (!meshRenderer.enabled)
                {
                    continue;
                }

                if (found)
                {
                    bounds.Encapsulate(meshRenderer.bounds);
                }
                else
                {
                    bounds = meshRenderer.bounds;
                    found = true;
                }
            }

            return found;
        }

        private static void AddBoundsCorners(Bounds bounds, List<System.Numerics.Vector3> points)
        {
            var min = bounds.min;
            var max = bounds.max;
            for (var corner = 0; corner < 8; corner++)
            {
                points.Add(new System.Numerics.Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z));
            }
        }

        private void SetZoomCameraFieldOfView(float fieldOfView)
        {
            var lens = actionZoomCamera.Lens;
            lens.FieldOfView = fieldOfView;
            actionZoomCamera.Lens = lens;
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
