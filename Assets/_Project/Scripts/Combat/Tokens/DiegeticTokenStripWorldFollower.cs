using UnityEngine;

namespace Erumperem.Combat.Tokens
{
    /// <summary>
    /// When the token strip lives under a <b>shared</b> scene canvas (e.g. <c>TokensCanvas</c>), this keeps
    /// the strip aligned to a unit in world space every frame (Screen Space or World Space canvas).
    /// </summary>
    public sealed class DiegeticTokenStripWorldFollower : MonoBehaviour
    {
        private Transform _followTarget;
        private Vector3 _offsetInFollowLocalSpace;
        private bool _faceMainCamera;
        private RectTransform _rectTransform;
        private RectTransform _parentRect;
        private Canvas _rootCanvas;

        public void Initialize(
            Transform followTarget,
            Vector3 offsetInFollowLocalSpace,
            bool faceMainCamera)
        {
            _followTarget = followTarget;
            _offsetInFollowLocalSpace = offsetInFollowLocalSpace;
            _faceMainCamera = faceMainCamera;
        }

        private void Awake()
        {
            _rectTransform = transform as RectTransform;
        }

        private void LateUpdate()
        {
            if (_followTarget == null)
            {
                return;
            }

            var worldPosition = _followTarget.TransformPoint(_offsetInFollowLocalSpace);

            if (_rootCanvas == null)
            {
                _rootCanvas = GetComponentInParent<Canvas>();
            }

            if (_rootCanvas == null)
            {
                transform.position = worldPosition;
                ApplyBillboard();
                return;
            }

            if (_rootCanvas.renderMode == RenderMode.WorldSpace)
            {
                if (_rectTransform != null)
                {
                    _rectTransform.position = worldPosition;
                }
                else
                {
                    transform.position = worldPosition;
                }

                ApplyBillboard();
                return;
            }

            var eventCamera = _rootCanvas.renderMode == RenderMode.ScreenSpaceCamera
                ? _rootCanvas.worldCamera
                : Camera.main;
            if (eventCamera == null)
            {
                return;
            }

            var screenPoint = eventCamera.WorldToScreenPoint(worldPosition);
            if (screenPoint.z < 0f)
            {
                if (gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (_parentRect == null)
            {
                _parentRect = transform.parent as RectTransform;
            }

            var localSpaceParent = _parentRect != null
                ? _parentRect
                : (RectTransform)_rootCanvas.transform;

            var cameraForRectUtility = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : eventCamera;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                localSpaceParent,
                screenPoint,
                cameraForRectUtility,
                out var localPoint);
            if (_rectTransform != null)
            {
                _rectTransform.localPosition = localPoint;
            }

            ApplyBillboard();
        }

        private void ApplyBillboard()
        {
            if (!_faceMainCamera || Camera.main == null)
            {
                return;
            }

            var targetTransform = _rectTransform != null ? (Transform)_rectTransform : transform;
            targetTransform.rotation = Camera.main.transform.rotation;
        }
    }
}
