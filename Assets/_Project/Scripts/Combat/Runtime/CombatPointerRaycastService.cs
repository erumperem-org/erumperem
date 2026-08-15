using UnityEngine;

namespace Erumperem.Combat.Runtime
{
    /// <summary>
    /// Shared pointer → world raycast for <see cref="CombatCapsuleTag"/> resolution (AUDITORIA DRY #53).
    /// </summary>
    public sealed class CombatPointerRaycastService
    {
        public const float DefaultWorldRaycastDistance = 200f;

        private Camera _mainCamera;
        private float _raycastMaxDistance = DefaultWorldRaycastDistance;
        private LayerMask _raycastLayerMask = Physics.DefaultRaycastLayers;

        public void Configure(
            Camera mainCamera,
            float raycastMaxDistance = DefaultWorldRaycastDistance,
            LayerMask? raycastLayerMask = null)
        {
            _mainCamera = mainCamera;
            _raycastMaxDistance = raycastMaxDistance;
            if (raycastLayerMask.HasValue)
            {
                _raycastLayerMask = raycastLayerMask.Value;
            }
        }

        public void SetMainCamera(Camera mainCamera) => _mainCamera = mainCamera;

        public Camera MainCamera
        {
            get
            {
                RefreshMainCameraIfMissing();
                return _mainCamera;
            }
        }

        public void RefreshMainCameraIfMissing()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }
        }

        public bool TryRaycastCombatCapsuleTag(
            Vector2 screenPosition,
            out CombatCapsuleTag capsuleTag,
            out RaycastHit raycastHit)
        {
            capsuleTag = null;
            raycastHit = default;

            RefreshMainCameraIfMissing();
            if (_mainCamera == null)
            {
                return false;
            }

            var ray = _mainCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out raycastHit, _raycastMaxDistance, _raycastLayerMask))
            {
                return false;
            }

            capsuleTag = raycastHit.collider.GetComponentInParent<CombatCapsuleTag>();
            return capsuleTag != null && !string.IsNullOrEmpty(capsuleTag.combatantId);
        }

        public bool TryRaycastCombatCapsuleTagFromInputManager(out CombatCapsuleTag capsuleTag)
        {
            capsuleTag = null;
            if (InputManager.Instance == null ||
                !InputManager.Instance.TryGetPointerScreenPosition(out var pointerScreenPosition))
            {
                return false;
            }

            return TryRaycastCombatCapsuleTag(pointerScreenPosition, out capsuleTag, out _);
        }
    }
}
