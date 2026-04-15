using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Erumperem.Combat
{
    /// <summary>
    /// Instancia o prefab <see cref="notificationPanelPrefab"/> no runtime dentro de <see cref="notificationsContainer"/>.
    /// Entrada com DOTween (slide + escala); punch opcional; expiração; remoção com subida em DOTween.
    /// </summary>
    public sealed class CombatLogStackView : MonoBehaviour
    {
        private const string NotificationTextObjectName = "NotificationText";

        [FormerlySerializedAs("stackParent")]
        [Tooltip("RectTransform com Vertical Layout Group (ex.: NotificationsContainer).")]
        [SerializeField] private RectTransform notificationsContainer;

        [FormerlySerializedAs("linePrefab")]
        [Tooltip("Prefab do projeto (ex.: NotificationPanel), não uma instância na cena.")]
        [SerializeField] private GameObject notificationPanelPrefab;

        [SerializeField] private float lifetimeSeconds = 20f;

        [Header("Entrada (DOTween)")]
        [Tooltip("Deslocamento inicial em anchoredPosition antes de deslizar para a posição final do layout.")]
        [SerializeField] private Vector2 spawnSlideOffset = new(180f, 0f);
        [SerializeField] private float spawnStartScale = 0.88f;
        [SerializeField] private float spawnEntryDuration = 0.32f;
        [SerializeField] private bool skipSlideWhenOffsetZero = true;

        [Header("Punch após entrada")]
        [SerializeField] private Vector3 spawnPunchScale = new(0.1f, 0.1f, 0.1f);
        [SerializeField] private float spawnPunchDuration = 0.22f;
        [SerializeField] private int spawnPunchVibrato = 5;
        [SerializeField] private float spawnPunchElasticity = 0.45f;

        [Header("Subida ao remover")]
        [SerializeField] private float slideUpDuration = 0.35f;

        private readonly Queue<GameObject> _removalQueue = new();
        private bool _removalRoutineRunning;
        private int _layoutSuspendCount;

        public void Push(string message)
        {
            if (notificationsContainer == null || notificationPanelPrefab == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            var instance = Instantiate(notificationPanelPrefab, notificationsContainer);
            instance.transform.SetAsLastSibling();

            var textGo = instance.transform.Find(NotificationTextObjectName);
            if (textGo != null && textGo.TryGetComponent<TextMeshProUGUI>(out var tmp))
            {
                tmp.text = message;
            }
            else if (instance.TryGetComponent<TextMeshProUGUI>(out var rootTmp))
            {
                rootTmp.text = message;
            }
            else
            {
                var anyTmp = instance.GetComponentInChildren<TextMeshProUGUI>(true);
                if (anyTmp != null)
                {
                    anyTmp.text = message;
                }
            }

            var panelRect = instance.transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.DOKill(false);
                StartCoroutine(AnimateSpawnEntry(panelRect));
            }

            StartCoroutine(ExpireAfterLifetime(instance));
        }

        private bool NeedsLayoutSuspendForSpawn =>
            !skipSlideWhenOffsetZero || spawnSlideOffset.sqrMagnitude > 0.0001f ||
            Mathf.Abs(spawnStartScale - 1f) > 0.0001f;

        private IEnumerator AnimateSpawnEntry(RectTransform panelRect)
        {
            var suspendedLayout = false;
            if (NeedsLayoutSuspendForSpawn)
            {
                SuspendLayout();
                suspendedLayout = true;
            }

            try
            {
                yield return null;
                if (panelRect == null)
                {
                    yield break;
                }

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(notificationsContainer);

                var endPos = panelRect.anchoredPosition;
                if (NeedsLayoutSuspendForSpawn)
                {
                    panelRect.anchoredPosition = endPos + spawnSlideOffset;
                    panelRect.localScale = Vector3.one * Mathf.Max(0.01f, spawnStartScale);
                }

                if (NeedsLayoutSuspendForSpawn)
                {
                    if (spawnEntryDuration > 0f)
                    {
                        var entry = DOTween.Sequence();
                        entry.SetTarget(panelRect);
                        entry.Join(panelRect.DOAnchorPos(endPos, spawnEntryDuration).SetEase(Ease.OutCubic));
                        entry.Join(panelRect.DOScale(Vector3.one, spawnEntryDuration).SetEase(Ease.OutCubic));
                        yield return entry.WaitForCompletion(true);
                    }
                    else
                    {
                        panelRect.anchoredPosition = endPos;
                        panelRect.localScale = Vector3.one;
                    }
                }

                if (panelRect != null && spawnPunchDuration > 0f)
                {
                    panelRect.DOPunchScale(
                        spawnPunchScale,
                        spawnPunchDuration,
                        spawnPunchVibrato,
                        spawnPunchElasticity);
                }
            }
            finally
            {
                if (suspendedLayout)
                {
                    ResumeLayout();
                }
            }
        }

        private void SuspendLayout()
        {
            if (notificationsContainer == null)
            {
                return;
            }

            var vlg = notificationsContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                return;
            }

            _layoutSuspendCount++;
            if (_layoutSuspendCount == 1)
            {
                vlg.enabled = false;
            }
        }

        private void ResumeLayout()
        {
            if (notificationsContainer == null)
            {
                return;
            }

            var vlg = notificationsContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                return;
            }

            _layoutSuspendCount = Mathf.Max(0, _layoutSuspendCount - 1);
            if (_layoutSuspendCount != 0)
            {
                return;
            }

            vlg.enabled = true;
            LayoutRebuilder.ForceRebuildLayoutImmediate(notificationsContainer);
        }

        private IEnumerator ExpireAfterLifetime(GameObject panel)
        {
            yield return new WaitForSeconds(lifetimeSeconds);
            if (panel == null)
            {
                yield break;
            }

            EnqueueRemoval(panel);
        }

        private void EnqueueRemoval(GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            _removalQueue.Enqueue(panel);
            if (!_removalRoutineRunning)
            {
                StartCoroutine(ProcessRemovalQueue());
            }
        }

        private IEnumerator ProcessRemovalQueue()
        {
            _removalRoutineRunning = true;
            try
            {
                while (_removalQueue.Count > 0)
                {
                    var panel = _removalQueue.Dequeue();
                    if (panel == null)
                    {
                        continue;
                    }

                    yield return AnimateRemovalAndDestroy(panel);
                }
            }
            finally
            {
                _removalRoutineRunning = false;
            }
        }

        private IEnumerator AnimateRemovalAndDestroy(GameObject panel)
        {
            if (notificationsContainer == null)
            {
                Destroy(panel);
                yield break;
            }

            panel.transform.DOKill(false);

            var survivors = new List<RectTransform>();
            var oldAnchored = new List<Vector2>();
            for (var i = 0; i < notificationsContainer.childCount; i++)
            {
                var child = notificationsContainer.GetChild(i) as RectTransform;
                if (child == null || child.gameObject == panel)
                {
                    continue;
                }

                survivors.Add(child);
                oldAnchored.Add(child.anchoredPosition);
            }

            Destroy(panel);
            yield return null;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(notificationsContainer);

            var vlg = notificationsContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.enabled = false;
            }

            var targets = new List<Vector2>(survivors.Count);
            foreach (var rt in survivors)
            {
                targets.Add(rt.anchoredPosition);
            }

            for (var i = 0; i < survivors.Count; i++)
            {
                survivors[i].anchoredPosition = oldAnchored[i];
            }

            if (survivors.Count == 0)
            {
                if (vlg != null)
                {
                    vlg.enabled = true;
                    LayoutRebuilder.ForceRebuildLayoutImmediate(notificationsContainer);
                }

                yield break;
            }

            var slide = DOTween.Sequence();
            for (var i = 0; i < survivors.Count; i++)
            {
                slide.Join(survivors[i].DOAnchorPos(targets[i], slideUpDuration).SetEase(Ease.OutQuad));
            }

            yield return slide.WaitForCompletion(true);

            if (vlg != null)
            {
                vlg.enabled = true;
                LayoutRebuilder.ForceRebuildLayoutImmediate(notificationsContainer);
            }
        }

        private void OnDisable()
        {
            if (notificationsContainer == null)
            {
                return;
            }

            for (var i = 0; i < notificationsContainer.childCount; i++)
            {
                notificationsContainer.GetChild(i).DOKill(false);
            }

            _layoutSuspendCount = 0;
            var vlg = notificationsContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.enabled = true;
            }
        }
    }
}
