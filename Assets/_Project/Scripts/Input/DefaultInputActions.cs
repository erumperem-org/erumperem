using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

/// <summary>
/// InputActions padrão do projeto criadas por código.
/// Gameplay: movimento, ponteiro, cliques, skills 1-7 e cheats de cena (F1-F3).
/// </summary>
public sealed class DefaultInputActions : IInputActionCollection, IDisposable
{
    private readonly InputActionAsset _asset;
    private readonly InputActionMap _gameplayMap;

    private readonly InputAction _moveAction;
    private readonly InputAction _pointerPositionAction;
    private readonly InputAction _leftClickAction;
    private readonly InputAction _rightClickAction;
    private readonly InputAction _skillSlot1Action;
    private readonly InputAction _skillSlot2Action;
    private readonly InputAction _skillSlot3Action;
    private readonly InputAction _skillSlot4Action;
    private readonly InputAction _skillSlot5Action;
    private readonly InputAction _skillSlot6Action;
    private readonly InputAction _skillSlot7Action;
    private readonly InputAction _sceneCheat1Action;
    private readonly InputAction _sceneCheat2Action;
    private readonly InputAction _sceneCheat3Action;
    private readonly InputAction _sceneCheat4Action;
    private readonly InputAction _sceneCheat5Action;

    public DefaultInputActions()
    {
        _asset = ScriptableObject.CreateInstance<InputActionAsset>();
        _gameplayMap = new InputActionMap("Gameplay");
        _asset.AddActionMap(_gameplayMap);

        _moveAction = _gameplayMap.AddAction("Move", InputActionType.Value);
        _moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        _moveAction.AddBinding("<Gamepad>/leftStick");

        _pointerPositionAction = _gameplayMap.AddAction("PointerPosition", InputActionType.Value);
        _pointerPositionAction.AddBinding("<Pointer>/position");

        _leftClickAction = _gameplayMap.AddAction("LeftClick", InputActionType.Button);
        _leftClickAction.AddBinding("<Mouse>/leftButton");

        _rightClickAction = _gameplayMap.AddAction("RightClick", InputActionType.Button);
        _rightClickAction.AddBinding("<Mouse>/rightButton");

        _skillSlot1Action = _gameplayMap.AddAction("SkillSlot1", InputActionType.Button);
        _skillSlot1Action.AddBinding("<Keyboard>/1");
        _skillSlot2Action = _gameplayMap.AddAction("SkillSlot2", InputActionType.Button);
        _skillSlot2Action.AddBinding("<Keyboard>/2");
        _skillSlot3Action = _gameplayMap.AddAction("SkillSlot3", InputActionType.Button);
        _skillSlot3Action.AddBinding("<Keyboard>/3");
        _skillSlot4Action = _gameplayMap.AddAction("SkillSlot4", InputActionType.Button);
        _skillSlot4Action.AddBinding("<Keyboard>/4");
        _skillSlot5Action = _gameplayMap.AddAction("SkillSlot5", InputActionType.Button);
        _skillSlot5Action.AddBinding("<Keyboard>/5");
        _skillSlot6Action = _gameplayMap.AddAction("SkillSlot6", InputActionType.Button);
        _skillSlot6Action.AddBinding("<Keyboard>/6");
        _skillSlot7Action = _gameplayMap.AddAction("SkillSlot7", InputActionType.Button);
        _skillSlot7Action.AddBinding("<Keyboard>/7");

        _sceneCheat1Action = _gameplayMap.AddAction("SceneCheat1", InputActionType.Button);
        _sceneCheat1Action.AddBinding("<Keyboard>/f1");
        _sceneCheat2Action = _gameplayMap.AddAction("SceneCheat2", InputActionType.Button);
        _sceneCheat2Action.AddBinding("<Keyboard>/f2");
        _sceneCheat3Action = _gameplayMap.AddAction("SceneCheat3", InputActionType.Button);
        _sceneCheat3Action.AddBinding("<Keyboard>/f3");
        _sceneCheat4Action = _gameplayMap.AddAction("SceneCheat4", InputActionType.Button);
        _sceneCheat4Action.AddBinding("<Keyboard>/f4");
        _sceneCheat5Action = _gameplayMap.AddAction("SceneCheat5", InputActionType.Button);
        _sceneCheat5Action.AddBinding("<Keyboard>/f5");
    }

    public InputBinding? bindingMask
    {
        get => _asset.bindingMask;
        set => _asset.bindingMask = value;
    }

    public ReadOnlyArray<InputDevice>? devices
    {
        get => _asset.devices;
        set => _asset.devices = value;
    }

    public ReadOnlyArray<InputControlScheme> controlSchemes => _asset.controlSchemes;

    public bool Contains(InputAction action) => _asset.Contains(action);
    public IEnumerator<InputAction> GetEnumerator() => _asset.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public void Enable() => _asset.Enable();
    public void Disable() => _asset.Disable();

    public void Dispose()
    {
        if (_asset != null)
        {
            UnityEngine.Object.Destroy(_asset);
        }
    }

    public GameplayActions Gameplay => new(this);

    public struct GameplayActions
    {
        private readonly DefaultInputActions _wrapper;

        public GameplayActions(DefaultInputActions wrapper) => _wrapper = wrapper;

        public InputAction Move => _wrapper._moveAction;
        public InputAction PointerPosition => _wrapper._pointerPositionAction;
        public InputAction LeftClick => _wrapper._leftClickAction;
        public InputAction RightClick => _wrapper._rightClickAction;
        public InputAction SkillSlot1 => _wrapper._skillSlot1Action;
        public InputAction SkillSlot2 => _wrapper._skillSlot2Action;
        public InputAction SkillSlot3 => _wrapper._skillSlot3Action;
        public InputAction SkillSlot4 => _wrapper._skillSlot4Action;
        public InputAction SkillSlot5 => _wrapper._skillSlot5Action;
        public InputAction SkillSlot6 => _wrapper._skillSlot6Action;
        public InputAction SkillSlot7 => _wrapper._skillSlot7Action;
        public InputAction SceneCheat1 => _wrapper._sceneCheat1Action;
        public InputAction SceneCheat2 => _wrapper._sceneCheat2Action;
        public InputAction SceneCheat3 => _wrapper._sceneCheat3Action;
        public InputAction SceneCheat4 => _wrapper._sceneCheat4Action;
        public InputAction SceneCheat5 => _wrapper._sceneCheat5Action;  
    }
}
