using System.Collections.Generic;
using Erumperem.Combat;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat.Tokens
{
    /// <summary>
    /// Spawns one <see cref="DiegeticTokenStripPresenter"/> per combatant and keeps it in sync with
    /// <see cref="Combatant.Tokens"/> (Game.Core).
    /// Parent options: (1) under each unit / optional anchor child, or (2) under a shared scene canvas
    /// (e.g. <c>TokensCanvas</c>) with <see cref="DiegeticTokenStripWorldFollower"/>.
    /// </summary>
    [DefaultExecutionOrder(25)]
    public sealed class CombatDiegeticTokenStripsBinder : MonoBehaviour
    {
        [SerializeField] private CombatSessionHub sessionHub;
        [SerializeField] private TokenVisualCatalog tokenVisualCatalog;
        [Tooltip("Root prefab: World Space Canvas (or child) with DiegeticTokenStripPresenter + HorizontalLayoutGroup content.")]
        [SerializeField] private GameObject diegeticStripRootPrefab;

        [Tooltip(
            "Optional: drag the scene TokensCanvas (or a child RectTransform under it). Strips spawn as children here " +
            "and follow units via world position each frame. Leave empty to parent strips directly under each unit.")]
        [SerializeField] private RectTransform sharedStripParent;

        [Tooltip(
            "Only when shared strip parent is null: search this name under each unit root (e.g. TokenStripAnchor). " +
            "Do not use your scene canvas name here — use Shared Strip Parent instead.")]
        [SerializeField] private string stripAnchorChildName = "";

        [Tooltip(
            "Offset extra em espaço local do alvo, somado ao topo Y do collider da unidade.")]
        [SerializeField] private Vector3 stripLocalOffset = new(0f, 0.35f, 0f);

        [Tooltip("Roda a tira de tokens para olhar para a Main Camera todo o frame (billboard simples). " +
                 "Desligue se o 'Shared Strip Parent' for um Canvas em Screen Space (Overlay/Camera) — " +
                 "nesse caso o canvas já está sempre virado ao ecrã.")]
        [SerializeField] private bool faceMainCameraEachFrame;

        private CombatPrototypeController _combatSession;
        private readonly List<DiegeticTokenStripPresenter> _presenters = new();
        private readonly List<Transform> _stripRoots = new();
        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        private void RefreshMainCameraIfMissing()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }
        }

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
            TearDownStrips();
        }

        private void LateUpdate()
        {
            if (_presenters.Count == 0 || _combatSession == null || !_combatSession.IsBattleOngoing)
            {
                return;
            }

            foreach (var presenter in _presenters)
            {
                if (presenter != null)
                {
                    presenter.RefreshFromBattleState();
                }
            }

            if (!faceMainCameraEachFrame)
            {
                return;
            }

            // Em Screen Space (Overlay/Camera) o canvas já está sempre virado para o ecrã,
            // logo o billboard manual é redundante e até distorce. Saímos cedo.
            if (sharedStripParent != null)
            {
                var sharedParentCanvas = sharedStripParent.GetComponentInParent<Canvas>();
                if (sharedParentCanvas != null && sharedParentCanvas.renderMode != RenderMode.WorldSpace)
                {
                    return;
                }
            }

            RefreshMainCameraIfMissing();

            if (_mainCamera == null)
            {
                return;
            }

            foreach (var stripRoot in _stripRoots)
            {
                if (stripRoot == null || stripRoot.GetComponent<DiegeticTokenStripWorldFollower>() != null)
                {
                    continue;
                }

                stripRoot.rotation = _mainCamera.transform.rotation;
            }
        }

        private void HandleCombatSessionReadyForUi(CombatPrototypeController controller)
        {
            TearDownStrips();
            if (controller == null || tokenVisualCatalog == null || diegeticStripRootPrefab == null)
            {
                return;
            }

            EnsureSharedStripCanvasReceivesPointerEvents();

            _combatSession = controller;
            var state = controller.BattleState;
            if (state == null)
            {
                return;
            }

            foreach (var combatant in state.GetAllCombatants())
            {
                var unitRoot = controller.TryGetUnitVisualRoot(combatant.Identity.Id);
                if (unitRoot == null)
                {
                    continue;
                }

                var followTarget = ResolveStripParent(unitRoot);
                var stripOffsetFromColliderTop = CombatUnitColliderVerticalExtents.ComposeLocalOffsetAnchoredToColliderTop(
                    followTarget,
                    stripLocalOffset);
                Transform hierarchyParent;
                if (sharedStripParent != null)
                {
                    hierarchyParent = sharedStripParent;
                }
                else
                {
                    hierarchyParent = followTarget;
                }

                var stripInstance = Instantiate(diegeticStripRootPrefab, hierarchyParent);
                stripInstance.transform.localRotation = Quaternion.identity;
                if (sharedStripParent != null)
                {
                    stripInstance.transform.localPosition = Vector3.zero;
                    var follower = stripInstance.GetComponent<DiegeticTokenStripWorldFollower>();
                    if (follower == null)
                    {
                        follower = stripInstance.AddComponent<DiegeticTokenStripWorldFollower>();
                    }

                    follower.Initialize(followTarget, stripOffsetFromColliderTop, faceMainCameraEachFrame);
                }
                else
                {
                    stripInstance.transform.localPosition = stripOffsetFromColliderTop;
                }

                stripInstance.name = $"DiegeticTokens_{combatant.Identity.Id}";

                var presenter = stripInstance.GetComponentInChildren<DiegeticTokenStripPresenter>(true);
                if (presenter == null)
                {
                    Debug.LogWarning(
                        $"{nameof(CombatDiegeticTokenStripsBinder)}: prefab is missing {nameof(DiegeticTokenStripPresenter)}.",
                        stripInstance);
                    Destroy(stripInstance);
                    continue;
                }

                presenter.Configure(controller, combatant.Identity.Id, tokenVisualCatalog);
                presenter.RefreshFromBattleState();
                _presenters.Add(presenter);
                _stripRoots.Add(stripInstance.transform);
            }
        }

        private void HandleCombatSessionClosed()
        {
            _combatSession = null;
            TearDownStrips();
        }

        private void EnsureSharedStripCanvasReceivesPointerEvents()
        {
            if (sharedStripParent == null)
            {
                return;
            }

            var sharedCanvas = sharedStripParent.GetComponentInParent<Canvas>();
            if (sharedCanvas == null || sharedCanvas.renderMode != RenderMode.WorldSpace)
            {
                return;
            }

            if (sharedCanvas.worldCamera == null)
            {
                RefreshMainCameraIfMissing();
                sharedCanvas.worldCamera = _mainCamera;
            }
        }

        private void TearDownStrips()
        {
            foreach (var stripRoot in _stripRoots)
            {
                if (stripRoot != null)
                {
                    Destroy(stripRoot.gameObject);
                }
            }

            _stripRoots.Clear();
            _presenters.Clear();
        }

        /// <summary>
        /// Optional anchor under the unit (same name on ally/enemy prefabs). Does not reference a scene Canvas —
        /// the World Space canvas lives on <see cref="diegeticStripRootPrefab"/>.
        /// </summary>
        private Transform ResolveStripParent(Transform unitVisualRoot)
        {
            if (string.IsNullOrWhiteSpace(stripAnchorChildName))
            {
                return unitVisualRoot;
            }

            var anchor = FindDescendantNamed(unitVisualRoot, stripAnchorChildName);
            if (anchor == null)
            {
                Debug.LogWarning(
                    $"{nameof(CombatDiegeticTokenStripsBinder)}: no child named \"{stripAnchorChildName}\" under " +
                    $"{unitVisualRoot.name}; using unit root.",
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
