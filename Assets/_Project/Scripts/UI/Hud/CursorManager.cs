using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public enum CursorType
{
    Normal,
    Hover,
    Click,
    Disabled,
    Attack,
    Interact
}

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance;

    [System.Serializable]
    public class CursorState
    {
        public CursorType stateType;

        [Header("Static Cursor")]
        public Texture2D staticTexture;

        [Header("Animated Cursor")]
        public bool animated;

        public Texture2D[] frames;

        [Min(0.01f)]
        public float frameRate = 0.08f;

        [Header("Settings")]
        public Vector2 hotspot;
    }

    [Header("Cursor States")]
    [SerializeField]
    private List<CursorState> cursorStates = new();

    private Dictionary<CursorType, CursorState> stateLookup;

    private Coroutine animationRoutine;

    private CursorType currentState;

    private bool isHoveringUI;

    private readonly List<RaycastResult> raycastResults = new();

    private PointerEventData pointerEventData;

    #region Unity Methods

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        stateLookup = new Dictionary<CursorType, CursorState>();

        foreach (CursorState state in cursorStates)
        {
            if (!stateLookup.ContainsKey(state.stateType))
            {
                stateLookup.Add(state.stateType, state);
            }
        }
    }

    private void Start()
    {
        SetState(CursorType.Normal);
    }

    private void Update()
    {
        if (Mouse.current == null)
            return;

        HandleUIHover();
        HandleUIClick();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    #endregion

    #region Scene Management

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetState(CursorType.Normal);
    }

    #endregion

    #region UI Detection

    private void HandleUIHover()
    {
        bool hoveringNow = IsHoveringInteractableUI();

        if (hoveringNow && !isHoveringUI)
        {
            isHoveringUI = true;

            if (!Mouse.current.leftButton.isPressed)
            {
                SetState(CursorType.Hover);
            }
        }
        else if (!hoveringNow && isHoveringUI)
        {
            isHoveringUI = false;

            if (!Mouse.current.leftButton.isPressed)
            {
                SetState(CursorType.Normal);
            }
        }
    }

    private void HandleUIClick()
    {
        if (!isHoveringUI)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            SetState(CursorType.Click);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            SetState(CursorType.Hover);
        }
    }

    private bool IsHoveringInteractableUI()
    {
        if (EventSystem.current == null)
            return false;

        pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.position = Mouse.current.position.ReadValue();

        raycastResults.Clear();

        EventSystem.current.RaycastAll(
            pointerEventData,
            raycastResults
        );

        foreach (RaycastResult result in raycastResults)
        {
            Selectable selectable =
                result.gameObject.GetComponentInParent<Selectable>();

            if (selectable != null &&
                selectable.IsInteractable())
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Cursor State

    public void SetState(CursorType stateType)
    {
        if (currentState == stateType)
            return;

        if (!stateLookup.TryGetValue(stateType, out CursorState state))
        {
            Debug.LogWarning($"Cursor State '{stateType}' não encontrado.");
            return;
        }

        currentState = stateType;

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        if (state.animated &&
            state.frames != null &&
            state.frames.Length > 0)
        {
            animationRoutine =
                StartCoroutine(AnimateCursor(state));
        }
        else
        {
            Cursor.SetCursor(
                state.staticTexture,
                state.hotspot,
                CursorMode.Auto
            );
        }
    }

    private IEnumerator AnimateCursor(CursorState state)
    {
        int frameIndex = 0;

        while (true)
        {
            Cursor.SetCursor(
                state.frames[frameIndex],
                state.hotspot,
                CursorMode.Auto
            );

            frameIndex++;

            if (frameIndex >= state.frames.Length)
            {
                frameIndex = 0;
            }

            yield return new WaitForSeconds(state.frameRate);
        }
    }

    public CursorType GetCurrentState()
    {
        return currentState;
    }

    #endregion

    #region Utilities

    public void ShowCursor()
    {
        Cursor.visible = true;
    }

    public void HideCursor()
    {
        Cursor.visible = false;
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    #endregion
}