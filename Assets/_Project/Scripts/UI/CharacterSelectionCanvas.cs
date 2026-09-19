using DG.Tweening;
using Erumperem.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Canvas dinâmico de seleção de personagem.
///
/// Uso:
///   1. Crie um Canvas filho do NPC de interação.
///   2. Arraste este componente para o Canvas.
///   3. Preencha as referências no Inspector.
///   4. Chame Open(character) quando o jogador interagir com o NPC.
///
/// Layout esperado:
///   Canvas
///   └── Panel
///       ├── TxtCharacterName   (TextMeshProUGUI)
///       ├── TxtCurrentState    (TextMeshProUGUI)
///       ├── BtnSetMain         (Button)
///       ├── BtnSetCompanion    (Button)
///       └── BtnClose           (Button)
/// </summary>
public sealed class CharacterSelectionCanvas : MonoBehaviour
{
    [Header("Referências de UI")]
    [SerializeField] public GameObject _panel;
    [SerializeField] private TextMeshProUGUI _txtCharacterName;
    [SerializeField] private TextMeshProUGUI _txtCurrentState;
    [SerializeField] private Button _btnSetMain;
    [SerializeField] private Button _btnSetCompanion;
    [SerializeField] private Button _btnClose;

    [Header("Textos dos botões (opcional)")]
    [SerializeField] private TextMeshProUGUI _btnMainLabel;
    [SerializeField] private TextMeshProUGUI _btnCompanionLabel;

    [Header("Dependências")]
    [SerializeField] private PlayableCharactersManager _manager;

    [Header("Animação DOTween")]
    [Tooltip("RectTransform animado. Se vazio, usa o RectTransform do próprio painel.")]
    [SerializeField] private RectTransform _tweenTarget;

    [Tooltip("CanvasGroup usado somente quando animateAlpha estiver habilitado nas configurações.")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [Tooltip("Configuração de entrada do painel. Por padrão, ele desliza vindo da esquerda.")]
    [SerializeField] private ActivateObjectByInput.DotweenAnimationSettings _openTween = new();

    [Tooltip("Configuração de saída do painel. Por padrão, ele desliza para a esquerda.")]
    [SerializeField] private ActivateObjectByInput.DotweenAnimationSettings _closeTween = new()
    {
        slideDirection = ActivateObjectByInput.PanelSlideDirection.Left
    };

    private PlayableCharacter _current;
    private RectTransform _resolvedTweenTarget;
    private Vector2 _baseAnchoredPosition;
    private Vector3 _baseLocalScale;
    private Vector3 _baseLocalEulerAngles;
    private float _baseAlpha = 1f;
    private Sequence _activeTween;
    private int _animationVersion;
    private bool _animationDefaultsCached;

    private void Awake()
    {
        CacheAnimationDefaults();

        if (_btnSetMain != null)
        {
            _btnSetMain.onClick.AddListener(OnClickSetMain);
        }

        if (_btnSetCompanion != null)
        {
            _btnSetCompanion.onClick.AddListener(OnClickSetCompanion);
        }

        if (_btnClose != null)
        {
            _btnClose.onClick.AddListener(Close);
        }

        if (_panel != null)
        {
            _panel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (_btnSetMain != null)
        {
            _btnSetMain.onClick.RemoveListener(OnClickSetMain);
        }

        if (_btnSetCompanion != null)
        {
            _btnSetCompanion.onClick.RemoveListener(OnClickSetCompanion);
        }

        if (_btnClose != null)
        {
            _btnClose.onClick.RemoveListener(Close);
        }

        KillAnimation();
    }

    /// <summary>
    /// Abre o canvas configurado para o personagem informado.
    /// Chamado pelo NPC de interação.
    /// </summary>
    public void Open(PlayableCharacter character)
    {
        if (character == null || _panel == null)
        {
            return;
        }

        CacheAnimationDefaults();
        _current = character;
        Refresh();
        _panel.SetActive(true);
        PlayOpenAnimation();
    }

    /// <summary>
    /// Fecha o painel usando a animação de saída configurada.
    /// </summary>
    public async void Close()
    {
        if (_panel == null || !_panel.activeSelf)
        {
            _current = null;
            return;
        }

        int animationVersion = StartNewAnimation();
        _current = null;

        if (!PlayCloseAnimation())
        {
            _panel.SetActive(false);
            return;
        }

        float duration = Mathf.Max(0f, _closeTween.duration);
        if (duration > 0f)
        {
            await Awaitable.WaitForSecondsAsync(duration);
        }

        if (animationVersion == _animationVersion && _panel != null)
        {
            _panel.SetActive(false);
        }
    }

    private void PlayOpenAnimation()
    {
        int animationVersion = StartNewAnimation();
        ApplyOpeningState(_openTween);
        Sequence sequence = BuildTween(_openTween, true);

        if (sequence == null || animationVersion != _animationVersion)
        {
            return;
        }

        _activeTween = sequence;
    }

    private bool PlayCloseAnimation()
    {
        Sequence sequence = BuildTween(_closeTween, false);
        if (sequence == null)
        {
            ApplyFinalState(_closeTween, false);
            return false;
        }

        _activeTween = sequence;
        return true;
    }

    private Sequence BuildTween(
        ActivateObjectByInput.DotweenAnimationSettings settings,
        bool opening)
    {
        if (_resolvedTweenTarget == null || settings == null)
        {
            return null;
        }

        float duration = Mathf.Max(0f, settings.duration);
        bool hasAnimation = false;
        Sequence sequence = DOTween.Sequence();

        if (IncludesSlide(settings.style))
        {
            Vector2 destination = opening
                ? _baseAnchoredPosition
                : _baseAnchoredPosition + GetSlideDisplacement(settings);

            sequence.Join(_resolvedTweenTarget.DOAnchorPos(destination, duration));
            hasAnimation = true;
        }

        if (IncludesScale(settings.style))
        {
            Vector3 destination = opening
                ? _baseLocalScale
                : Vector3.Scale(_baseLocalScale, settings.scaleMultiplier);

            sequence.Join(_resolvedTweenTarget.DOScale(destination, duration));
            hasAnimation = true;
        }

        if (IncludesRotation(settings.style))
        {
            Vector3 destination = opening
                ? _baseLocalEulerAngles
                : _baseLocalEulerAngles + GetRotationOffset(settings);

            sequence.Join(_resolvedTweenTarget.DOLocalRotate(destination, duration));
            hasAnimation = true;
        }

        if (settings.animateAlpha && _canvasGroup != null)
        {
            float destination = opening ? _baseAlpha : settings.hiddenAlpha;
            sequence.Join(_canvasGroup.DOFade(destination, duration));
            hasAnimation = true;
        }

        if (!hasAnimation || duration <= 0f)
        {
            sequence.Kill(false);
            return null;
        }

        sequence.SetEase(settings.ease);
        sequence.SetUpdate(settings.useUnscaledTime);
        return sequence;
    }

    private void CacheAnimationDefaults()
    {
        _openTween ??= new ActivateObjectByInput.DotweenAnimationSettings();
        _closeTween ??= new ActivateObjectByInput.DotweenAnimationSettings
        {
            slideDirection = ActivateObjectByInput.PanelSlideDirection.Left
        };

        if (_panel == null)
        {
            return;
        }

        RectTransform resolvedTweenTarget = _tweenTarget != null
            ? _tweenTarget
            : _panel.GetComponent<RectTransform>();

        if (_animationDefaultsCached && _resolvedTweenTarget == resolvedTweenTarget)
        {
            return;
        }

        _resolvedTweenTarget = resolvedTweenTarget;

        if (_resolvedTweenTarget != null)
        {
            _baseAnchoredPosition = _resolvedTweenTarget.anchoredPosition;
            _baseLocalScale = _resolvedTweenTarget.localScale;
            _baseLocalEulerAngles = _resolvedTweenTarget.localEulerAngles;
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = _panel.GetComponent<CanvasGroup>();
        }

        if (_canvasGroup != null)
        {
            _baseAlpha = _canvasGroup.alpha;
        }

        _animationDefaultsCached = true;
    }

    private int StartNewAnimation()
    {
        _animationVersion++;
        KillAnimation();
        return _animationVersion;
    }

    private void KillAnimation()
    {
        _activeTween?.Kill(false);
        _activeTween = null;

        if (_resolvedTweenTarget != null)
        {
            _resolvedTweenTarget.DOKill(false);
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.DOKill(false);
        }
    }

    private void ApplyOpeningState(ActivateObjectByInput.DotweenAnimationSettings settings)
    {
        if (_resolvedTweenTarget == null || settings == null)
        {
            return;
        }

        if (IncludesSlide(settings.style))
        {
            _resolvedTweenTarget.anchoredPosition = _baseAnchoredPosition - GetSlideDisplacement(settings);
        }

        if (IncludesScale(settings.style))
        {
            _resolvedTweenTarget.localScale = Vector3.Scale(_baseLocalScale, settings.scaleMultiplier);
        }

        if (IncludesRotation(settings.style))
        {
            _resolvedTweenTarget.localEulerAngles = _baseLocalEulerAngles + GetRotationOffset(settings);
        }

        if (settings.animateAlpha && _canvasGroup != null)
        {
            _canvasGroup.alpha = settings.hiddenAlpha;
        }
    }

    private void ApplyFinalState(
        ActivateObjectByInput.DotweenAnimationSettings settings,
        bool opening)
    {
        if (_resolvedTweenTarget != null && settings != null)
        {
            if (IncludesSlide(settings.style))
            {
                _resolvedTweenTarget.anchoredPosition = opening
                    ? _baseAnchoredPosition
                    : _baseAnchoredPosition + GetSlideDisplacement(settings);
            }

            if (IncludesScale(settings.style))
            {
                _resolvedTweenTarget.localScale = opening
                    ? _baseLocalScale
                    : Vector3.Scale(_baseLocalScale, settings.scaleMultiplier);
            }

            if (IncludesRotation(settings.style))
            {
                _resolvedTweenTarget.localEulerAngles = opening
                    ? _baseLocalEulerAngles
                    : _baseLocalEulerAngles + GetRotationOffset(settings);
            }
        }

        if (settings != null && settings.animateAlpha && _canvasGroup != null)
        {
            _canvasGroup.alpha = opening ? _baseAlpha : settings.hiddenAlpha;
        }
    }

    private static Vector2 GetSlideDisplacement(
        ActivateObjectByInput.DotweenAnimationSettings settings)
    {
        return settings.slideDirection switch
        {
            ActivateObjectByInput.PanelSlideDirection.Left => Vector2.left * settings.slideDistance,
            ActivateObjectByInput.PanelSlideDirection.Right => Vector2.right * settings.slideDistance,
            ActivateObjectByInput.PanelSlideDirection.Up => Vector2.up * settings.slideDistance,
            ActivateObjectByInput.PanelSlideDirection.Down => Vector2.down * settings.slideDistance,
            _ => Vector2.zero
        };
    }

    private static Vector3 GetRotationOffset(
        ActivateObjectByInput.DotweenAnimationSettings settings)
    {
        float degrees = Mathf.Abs(settings.rotationDegrees);
        if (settings.rotationDirection == ActivateObjectByInput.PanelRotationDirection.Clockwise)
        {
            degrees *= -1f;
        }

        return new Vector3(0f, 0f, degrees);
    }

    private static bool IncludesSlide(ActivateObjectByInput.DotweenAnimationStyle style)
    {
        return style == ActivateObjectByInput.DotweenAnimationStyle.Slide
            || style == ActivateObjectByInput.DotweenAnimationStyle.SlideAndScale
            || style == ActivateObjectByInput.DotweenAnimationStyle.SlideAndRotate
            || style == ActivateObjectByInput.DotweenAnimationStyle.SlideScaleAndRotate;
    }

    private static bool IncludesScale(ActivateObjectByInput.DotweenAnimationStyle style)
    {
        return style == ActivateObjectByInput.DotweenAnimationStyle.Scale
            || style == ActivateObjectByInput.DotweenAnimationStyle.SlideAndScale
            || style == ActivateObjectByInput.DotweenAnimationStyle.ScaleAndRotate
            || style == ActivateObjectByInput.DotweenAnimationStyle.SlideScaleAndRotate;
    }

    private static bool IncludesRotation(ActivateObjectByInput.DotweenAnimationStyle style)
    {
        return style == ActivateObjectByInput.DotweenAnimationStyle.Rotate
            || style == ActivateObjectByInput.DotweenAnimationStyle.SlideAndRotate
            || style == ActivateObjectByInput.DotweenAnimationStyle.ScaleAndRotate
            || style == ActivateObjectByInput.DotweenAnimationStyle.SlideScaleAndRotate;
    }

    private void OnClickSetMain()
    {
        if (_current == null || _manager == null)
        {
            return;
        }

        _manager.SetState(PlayableCharacterState.Main, _current);
        Close();
    }

    private void OnClickSetCompanion()
    {
        if (_current == null || _manager == null)
        {
            return;
        }

        _manager.SetState(PlayableCharacterState.Companion, _current);
        Close();
    }

    private void Refresh()
    {
        if (_current == null)
        {
            return;
        }

        if (_txtCurrentState != null)
        {
            _txtCurrentState.text = $"Current state: {_current.CurrentState}";
        }

        if (_btnSetMain != null)
        {
            _btnSetMain.interactable = _current.CurrentState != PlayableCharacterState.Main;
        }

        if (_btnSetCompanion != null)
        {
            _btnSetCompanion.interactable = _current.CurrentState != PlayableCharacterState.Companion;
        }

        if (_btnMainLabel != null)
        {
            _btnMainLabel.text = "Set as Main";
        }

        if (_btnCompanionLabel != null)
        {
            _btnCompanionLabel.text = "Set as Companion";
        }
    }
}
