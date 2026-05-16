using System.Collections.Generic;
using Erumperem.Combat.Tokens;
using UnityEngine;

namespace Erumperem.Combat.HealthBars
{
    /// <summary>
    /// Spawns one <see cref="HealthBarHudView"/> per combatant when the combat session is ready and
    /// keeps each instance world-anchored under the unit (or under a shared scene canvas via
    /// <see cref="DiegeticTokenStripWorldFollower"/>). The <see cref="CombatPrototypeController"/> never
    /// sees this binder — it only learns of the session readiness via <see cref="CombatSessionHub"/>.
    /// </summary>
    [DefaultExecutionOrder(25)]
    public sealed class CombatHealthBarsBinder : MonoBehaviour
    {
        [SerializeField] private CombatSessionHub sessionHub;

        [Tooltip("Prefab raiz com (no próprio GO ou num filho) o HealthBarHudView. " +
                 "Recomendado: World Space Canvas com Slider + (opcional) Image de trail.")]
        [SerializeField] private GameObject healthBarRootPrefab;

        [Tooltip("Opcional: arrasta o HealthBarsCanvas (ou um filho RectTransform). As barras são instanciadas como filhas " +
                 "deste e seguem cada unidade via DiegeticTokenStripWorldFollower (world position por frame). " +
                 "Vazio = parent directo sob o root da unidade.")]
        [SerializeField] private RectTransform sharedHealthBarParent;

        [Tooltip("Só usado quando 'Shared Health Bar Parent' é nulo: nome de um filho debaixo do root da unidade " +
                 "(ex.: HealthBarAnchor). Não use o nome de um Canvas aqui — use 'Shared Health Bar Parent'.")]
        [SerializeField] private string healthBarAnchorChildName = "";

        [Tooltip("Offset extra em espaço local do alvo, somado à base Y do collider da unidade.")]
        [SerializeField] private Vector3 healthBarLocalOffset = new(0f, 0.15f, 0f);

        [Tooltip("Roda a barra para olhar para a Main Camera todo o frame (billboard simples). " +
                 "Desligue se o 'Shared Health Bar Parent' for um Canvas em Screen Space (Overlay/Camera) — " +
                 "nesse caso o canvas já está sempre virado ao ecrã.")]
        [SerializeField] private bool faceMainCameraEachFrame = true;

        private CombatPrototypeController _combatSession;
        private readonly List<Transform> _spawnedHealthBarRoots = new();
        private Camera _mainCamera;

        private void OnEnable()
        {
            if (sessionHub == null)
            {
                return;
            }

            sessionHub.OnCombatSessionReadyForUi += HandleCombatSessionReadyForUi;
            sessionHub.OnCombatSessionClosed += HandleCombatSessionClosed;
        }

        private void OnDisable()
        {
            if (sessionHub == null)
            {
                return;
            }

            sessionHub.OnCombatSessionReadyForUi -= HandleCombatSessionReadyForUi;
            sessionHub.OnCombatSessionClosed -= HandleCombatSessionClosed;
            TearDownSpawnedHealthBars();
        }

        private void LateUpdate()
        {
            if (!faceMainCameraEachFrame || _spawnedHealthBarRoots.Count == 0)
            {
                return;
            }

            // Em Screen Space (Overlay/Camera) o canvas já está sempre virado para o ecrã,
            // logo o billboard manual é redundante e até distorce. Saímos cedo.
            if (sharedHealthBarParent != null)
            {
                var sharedParentCanvas = sharedHealthBarParent.GetComponentInParent<Canvas>();
                if (sharedParentCanvas != null && sharedParentCanvas.renderMode != RenderMode.WorldSpace)
                {
                    return;
                }
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            if (_mainCamera == null)
            {
                return;
            }

            foreach (var healthBarRoot in _spawnedHealthBarRoots)
            {
                if (healthBarRoot == null)
                {
                    continue;
                }

                if (healthBarRoot.GetComponent<DiegeticTokenStripWorldFollower>() != null)
                {
                    continue;
                }

                healthBarRoot.rotation = _mainCamera.transform.rotation;
            }
        }

        private void HandleCombatSessionReadyForUi(CombatPrototypeController controller)
        {
            TearDownSpawnedHealthBars();
            if (controller == null || healthBarRootPrefab == null)
            {
                return;
            }

            _combatSession = controller;
            var battleState = controller.BattleState;
            if (battleState == null)
            {
                return;
            }

            foreach (var combatant in battleState.GetAllCombatants())
            {
                var unitVisualRoot = controller.TryGetUnitVisualRoot(combatant.Identity.Id);
                if (unitVisualRoot == null)
                {
                    continue;
                }

                var followTarget = ResolveFollowTargetUnderUnit(unitVisualRoot);
                var hierarchyParent = sharedHealthBarParent != null ? sharedHealthBarParent : followTarget;
                var healthBarOffsetFromColliderBottom = CombatUnitColliderVerticalExtents.ComposeLocalOffsetAnchoredToColliderBottom(
                    followTarget,
                    healthBarLocalOffset);

                var healthBarInstance = Instantiate(healthBarRootPrefab, hierarchyParent);
                healthBarInstance.transform.localRotation = Quaternion.identity;

                if (sharedHealthBarParent != null)
                {
                    healthBarInstance.transform.localPosition = Vector3.zero;
                    var follower = healthBarInstance.GetComponent<DiegeticTokenStripWorldFollower>();
                    if (follower == null)
                    {
                        follower = healthBarInstance.AddComponent<DiegeticTokenStripWorldFollower>();
                    }

                    follower.Initialize(followTarget, healthBarOffsetFromColliderBottom, faceMainCameraEachFrame);
                }
                else
                {
                    healthBarInstance.transform.localPosition = healthBarOffsetFromColliderBottom;
                }

                healthBarInstance.name = $"HealthBar_{combatant.Identity.Id}";

                var hudView = healthBarInstance.GetComponentInChildren<HealthBarHudView>(true);
                if (hudView == null)
                {
                    Debug.LogWarning(
                        $"{nameof(CombatHealthBarsBinder)}: prefab '{healthBarRootPrefab.name}' não tem {nameof(HealthBarHudView)}.",
                        healthBarInstance);
                    Destroy(healthBarInstance);
                    continue;
                }

                hudView.Configure(_combatSession, combatant.Identity.Id);
                _spawnedHealthBarRoots.Add(healthBarInstance.transform);
            }
        }

        private void HandleCombatSessionClosed()
        {
            _combatSession = null;
            TearDownSpawnedHealthBars();
        }

        private void TearDownSpawnedHealthBars()
        {
            foreach (var healthBarRoot in _spawnedHealthBarRoots)
            {
                if (healthBarRoot != null)
                {
                    Destroy(healthBarRoot.gameObject);
                }
            }

            _spawnedHealthBarRoots.Clear();
        }

        private Transform ResolveFollowTargetUnderUnit(Transform unitVisualRoot)
        {
            if (string.IsNullOrWhiteSpace(healthBarAnchorChildName))
            {
                return unitVisualRoot;
            }

            var anchor = FindDescendantNamed(unitVisualRoot, healthBarAnchorChildName);
            if (anchor == null)
            {
                Debug.LogWarning(
                    $"{nameof(CombatHealthBarsBinder)}: não foi encontrado descendente '{healthBarAnchorChildName}' " +
                    $"sob '{unitVisualRoot.name}'. A usar o root da unidade.",
                    unitVisualRoot);
                return unitVisualRoot;
            }

            return anchor;
        }

        private static Transform FindDescendantNamed(Transform root, string targetName)
        {
            for (var childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                var child = root.GetChild(childIndex);
                if (child.name == targetName)
                {
                    return child;
                }

                var nested = FindDescendantNamed(child, targetName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
