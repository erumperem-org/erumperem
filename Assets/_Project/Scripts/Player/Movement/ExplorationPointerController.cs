using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Player
{
    [DisallowMultipleComponent]
    public sealed class ExplorationPointerController : MonoBehaviour
    {
        [SerializeField] private LayerMask _raycastLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(1f)] private float _rayDistance = 500f;
        [SerializeField, Min(0.1f)] private float _groundSampleRadius = 0.75f;
        [SerializeField, Min(0.1f)] private float _targetSampleRadius = 2f;
        [SerializeField, Min(0.1f)] private float _repathInterval = 0.35f;
        [SerializeField, Min(1f)] private float _stuckTimeout = 4f;

        private PlayableCharactersManager _manager;
        private PlayableCharacter _main;
        private PlayerInputReader _input;
        private InteractionOutline _hoverOutline;
        private Interactable _target;
        private Collider _targetCollider;
        private bool _hasCommand;
        private bool _isInteractionCommand;
        private Vector3 _lastProgressPosition;
        private Vector3 _lastTargetPosition;
        private float _lastProgressTime;
        private float _nextRepathTime;
        private ExplorationClickMarker _clickMarker;

        private void Awake()
        {
            _manager = GetComponent<PlayableCharactersManager>();
            _clickMarker = GetComponent<ExplorationClickMarker>();
            if (_clickMarker == null) _clickMarker = gameObject.AddComponent<ExplorationClickMarker>();
        }

        private void Update()
        {
            if (_manager == null) return;
            var current = _manager.Main as PlayableCharacter;
            if (_main != current)
            {
                CancelCommand();
                SetHover(null);
                _main = current;
            }
            if (_input != _manager.InputReader)
            {
                if (_input != null) _input.OnInteract -= CancelCommand;
                _input = _manager.InputReader;
                if (_input != null) _input.OnInteract += CancelCommand;
            }
            var mouse = Mouse.current;
            if (_main == null || !_main.isActiveAndEnabled || _main.MovementController == null
                || _main.DetectionSystem == null || _input == null || !_input.CanAcceptWorldInput
                || Time.timeScale <= 0f || !Application.isFocused || mouse == null)
            {
                CancelCommand();
                SetHover(null);
                return;
            }
            if (_input.MoveInput.sqrMagnitude > 0.01f || mouse.rightButton.wasPressedThisFrame)
                CancelCommand();

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (overUI)
            {
                SetHover(null);
                if (mouse.leftButton.wasPressedThisFrame) CancelCommand();
            }
            else
            {
                bool hitWorld = TryPick(mouse.position.ReadValue(), out var hit, out var interactable);
                bool eligible = interactable != null && interactable.isActiveAndEnabled
                    && interactable.CanInteract && interactable.CanShowInteractionFeedback;
                SetHover(eligible ? interactable : null);
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (hitWorld && _input.MoveInput.sqrMagnitude <= 0.01f)
                    {
                        if (eligible)
                        {
                            if (_main.DetectionSystem.IsInInteractionRange(interactable))
                            {
                                _target = interactable;
                                _clickMarker.Show(interactable.transform.position, Vector3.up);
                                InteractAndFinish();
                            }
                            else
                                BeginCommand(ApproachPoint(interactable, hit.collider), _targetSampleRadius,
                                    Vector3.up, interactable, hit.collider);
                        }
                        else if (interactable == null && hit.normal.y > 0.35f)
                            BeginCommand(hit.point, _groundSampleRadius, hit.normal);
                    }
                }
            }
            TickCommand();
        }

        private bool TryPick(Vector2 position, out RaycastHit picked, out Interactable target)
        {
            picked = default;
            target = null;
            var camera = Camera.main;
            if (camera == null) return false;
            var hits = Physics.RaycastAll(camera.ScreenPointToRay(position), _rayDistance,
                _raycastLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(_main.transform)) continue;
                var candidate = hit.collider.GetComponentInParent<Interactable>();
                if (candidate == null && hit.collider.isTrigger) continue;
                picked = hit;
                target = candidate;
                return true;
            }
            return false;
        }

        private void SetHover(Interactable target)
        {
            var outline = target != null ? target.GetComponent<InteractionOutline>() : null;
            if (target != null && outline == null)
                outline = target.gameObject.AddComponent<InteractionOutline>();
            if (_hoverOutline == outline) return;
            if (_hoverOutline != null) _hoverOutline.SetPointerHovered(false);
            _hoverOutline = outline;
            if (_hoverOutline != null) _hoverOutline.SetPointerHovered(true);
        }

        private Vector3 ApproachPoint()
            => ApproachPoint(_target, _targetCollider);

        private Vector3 ApproachPoint(Interactable target, Collider targetCollider)
        {
            var point = targetCollider != null
                ? targetCollider.ClosestPoint(_main.transform.position)
                : target.transform.position;
            point.y = target.transform.position.y;
            return point;
        }

        private void BeginCommand(Vector3 point, float sampleRadius, Vector3 normal,
            Interactable target = null, Collider targetCollider = null)
        {
            if (!_main.MovementController.TryMoveByClick(point, sampleRadius))
            {
                return;
            }
            _target = target;
            _targetCollider = targetCollider;
            _clickMarker.Show(_main.MovementController.ClickDestination, normal);
            _hasCommand = true;
            _isInteractionCommand = _target != null;
            _lastProgressPosition = _main.transform.position;
            _lastProgressTime = Time.time;
            _nextRepathTime = Time.time + _repathInterval;
            if (_target != null) _lastTargetPosition = _target.transform.position;
        }

        private void TickCommand()
        {
            if (!_hasCommand) return;
            if (_isInteractionCommand && _target == null)
            {
                CancelCommand();
                return;
            }
            if (_target != null)
            {
                if (!_target.isActiveAndEnabled || !_target.CanInteract
                    || _targetCollider == null || !_targetCollider.enabled)
                {
                    CancelCommand();
                    return;
                }
                if (_main.DetectionSystem.IsInInteractionRange(_target))
                {
                    InteractAndFinish();
                    return;
                }
                if (Time.time >= _nextRepathTime
                    && (_target.transform.position - _lastTargetPosition).sqrMagnitude > 0.09f)
                {
                    _nextRepathTime = Time.time + _repathInterval;
                    _lastTargetPosition = _target.transform.position;
                    if (!_main.MovementController.TryMoveByClick(ApproachPoint(), _targetSampleRadius))
                    {
                        CancelCommand();
                        return;
                    }
                }
            }
            if ((_targetCollider != null && _target == null) || !_main.MovementController.IsClickMoving)
            {
                CancelCommand();
                return;
            }
            if ((_main.transform.position - _lastProgressPosition).sqrMagnitude > 0.01f)
            {
                _lastProgressPosition = _main.transform.position;
                _lastProgressTime = Time.time;
            }
            else if (Time.time - _lastProgressTime >= _stuckTimeout) CancelCommand();
        }

        private void InteractAndFinish()
        {
            var target = _target;
            CancelCommand();
            _main.DetectionSystem.TryInteract(target);
        }

        private void CancelCommand()
        {
            if (_main != null && _main.MovementController != null)
                _main.MovementController.CancelClickMovement();
            _hasCommand = false;
            _isInteractionCommand = false;
            _target = null;
            _targetCollider = null;
        }

        private void OnDisable()
        {
            CancelCommand();
            SetHover(null);
            if (_input != null) _input.OnInteract -= CancelCommand;
            _input = null;
        }
    }
}
