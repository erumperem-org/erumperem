using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Erumperem.Progression
{
    /// <summary>
    /// Dev cheat: five clicks on the skill-tree Level UI set the shared skill level to max (12) and persist it.
    /// </summary>
    public sealed class SharedSkillLevelClickCheat : MonoBehaviour, IPointerClickHandler
    {
        private const int RequiredClickCount = 5;
        private const float ClickSequenceTimeoutSeconds = 2f;
        private const string HitAreaChildName = "LevelClickCheatHitArea";

        private int _clickCountInSequence;
        private float _lastClickUnscaledTime;

        /// <summary>
        /// Ensures a full-rect transparent hit target under the Level root and attaches this cheat.
        /// </summary>
        public static void EnsureBoundToLevelRoot(Transform levelRoot)
        {
            if (levelRoot == null)
            {
                return;
            }

            var hitAreaTransform = levelRoot.Find(HitAreaChildName);
            if (hitAreaTransform == null)
            {
                var hitAreaGameObject = new GameObject(HitAreaChildName, typeof(RectTransform));
                hitAreaTransform = hitAreaGameObject.transform;
                hitAreaTransform.SetParent(levelRoot, false);
                hitAreaGameObject.layer = levelRoot.gameObject.layer;

                var hitAreaRectTransform = hitAreaGameObject.GetComponent<RectTransform>();
                hitAreaRectTransform.anchorMin = Vector2.zero;
                hitAreaRectTransform.anchorMax = Vector2.one;
                hitAreaRectTransform.offsetMin = Vector2.zero;
                hitAreaRectTransform.offsetMax = Vector2.zero;
                hitAreaRectTransform.SetAsLastSibling();
            }

            var hitAreaImage = hitAreaTransform.GetComponent<Image>();
            if (hitAreaImage == null)
            {
                hitAreaImage = hitAreaTransform.gameObject.AddComponent<Image>();
            }

            hitAreaImage.color = new Color(0f, 0f, 0f, 0f);
            hitAreaImage.raycastTarget = true;

            if (hitAreaTransform.GetComponent<SharedSkillLevelClickCheat>() == null)
            {
                hitAreaTransform.gameObject.AddComponent<SharedSkillLevelClickCheat>();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var nowUnscaledTime = Time.unscaledTime;
            if (nowUnscaledTime - _lastClickUnscaledTime > ClickSequenceTimeoutSeconds)
            {
                _clickCountInSequence = 0;
            }

            _lastClickUnscaledTime = nowUnscaledTime;
            _clickCountInSequence++;

            if (_clickCountInSequence < RequiredClickCount)
            {
                return;
            }

            _clickCountInSequence = 0;
            ApplyCheatSetSharedSkillLevelToMax();
        }

        private static void ApplyCheatSetSharedSkillLevelToMax()
        {
            var progressionService = PlayerProgressionService.Instance
                ?? Object.FindFirstObjectByType<PlayerProgressionService>(FindObjectsInactive.Include);

            if (progressionService == null)
            {
                Debug.LogWarning("[SharedSkillLevelClickCheat] PlayerProgressionService not found.");
                return;
            }

            var maxSharedSkillLevel = progressionService.MaxSkillPoints;
            if (progressionService.TrySetSharedSkillLevel(maxSharedSkillLevel))
            {
                Debug.Log(
                    $"Cheat: shared skill level set to {maxSharedSkillLevel}/{progressionService.MaxSkillPoints} " +
                    "(persisted).");
                return;
            }

            Debug.Log(
                $"Cheat: shared skill level already {progressionService.GetSharedSkillLevel()}/" +
                $"{progressionService.MaxSkillPoints}.");
        }
    }
}
