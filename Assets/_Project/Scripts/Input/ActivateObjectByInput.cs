using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Erumperem.Input
{
    public class ActivateObjectByInput : MonoBehaviour
    {
        public enum PanelAnimationMode
        {
            Animator,
            DOTween,
            AnimatorAndDOTween
        }

        public enum DotweenAnimationStyle
        {
            None,
            Slide,
            Scale,
            Rotate,
            SlideAndScale,
            SlideAndRotate,
            ScaleAndRotate,
            SlideScaleAndRotate
        }

        public enum PanelSlideDirection
        {
            Left,
            Right,
            Up,
            Down
        }

        public enum PanelRotationDirection
        {
            Clockwise,
            CounterClockwise
        }

        [System.Serializable]
        public sealed class DotweenAnimationSettings
        {
            [Tooltip("Combinação de movimentos executados pelo DOTween.")]
            public DotweenAnimationStyle style = DotweenAnimationStyle.Slide;

            [Min(0f)]
            public float duration = 0.25f;

            public Ease ease = Ease.OutCubic;

            [Tooltip("Executa a animação mesmo quando o Time.timeScale estiver em zero.")]
            public bool useUnscaledTime;

            [Header("Slide")]
            [Tooltip("Direção do movimento durante esta transição. Na abertura, o painel vem do lado oposto e termina nesta direção; no fechamento, ele sai nesta direção.")]
            public PanelSlideDirection slideDirection = PanelSlideDirection.Right;

            [Min(0f)]
            public float slideDistance = 500f;

            [Header("Scale")]
            [Tooltip("Multiplicador da escala base usado no início da abertura e no fim do fechamento.")]
            public Vector3 scaleMultiplier = new(0.8f, 0.8f, 1f);

            [Header("Rotate")]
            [Tooltip("Graus de rotação usados no início da abertura e no fim do fechamento.")]
            public float rotationDegrees = 15f;

            public PanelRotationDirection rotationDirection = PanelRotationDirection.Clockwise;

            [Header("Alpha opcional")]
            [Tooltip("Quando habilitado, usa o CanvasGroup configurado para animar o alpha junto com os outros movimentos.")]
            public bool animateAlpha;

            [Range(0f, 1f)]
            public float hiddenAlpha;
        }

        [System.Serializable]
        public class PanelBinding
        {
            [Header("Input")]
            public Key activationKey = Key.E;

            [Header("Panel")]
            public GameObject panelObject;

            [Tooltip("Animator principal do painel. Mantido para compatibilidade com as configurações existentes.")]
            public Animator animator;

            [Header("Animators adicionais")]
            [Tooltip("Animators adicionais, como um Animator de máscara. Todos recebem os mesmos triggers do Animator principal.")]
            public List<Animator> additionalAnimators = new();

            [Header("Animation mode")]
            [Tooltip("Animator mantém o comportamento original. DOTween usa as configurações abaixo. AnimatorAndDOTween executa os dois em paralelo.")]
            public PanelAnimationMode animationMode = PanelAnimationMode.Animator;

            [Header("Animator")]
            public string openTrigger = "Open";
            public string closeTrigger = "Close";

            [Header("DOTween target")]
            [Tooltip("RectTransform que será animado. Se vazio, usa o RectTransform do próprio painel.")]
            public RectTransform tweenTarget;

            [Tooltip("CanvasGroup opcional usado somente quando animateAlpha estiver habilitado.")]
            public CanvasGroup canvasGroup;

            [Tooltip("Configuração aplicada ao abrir. Para deslizar para a direita, escolha Right aqui.")]
            public DotweenAnimationSettings openTween = new();

            [Tooltip("Configuração aplicada ao fechar. Para deslizar para a esquerda, escolha Left aqui.")]
            public DotweenAnimationSettings closeTween = new()
            {
                slideDirection = PanelSlideDirection.Left
            };

            [Header("Extra")]
            [Tooltip("Painéis que serão fechados quando este abrir")]
            public List<GameObject> panelsToDisable = new();

            [System.NonSerialized]
            private RectTransform _resolvedTweenTarget;

            [System.NonSerialized]
            private bool _animationDefaultsCached;

            [System.NonSerialized]
            private Vector2 _baseAnchoredPosition;

            [System.NonSerialized]
            private Vector3 _baseLocalScale;

            [System.NonSerialized]
            private Vector3 _baseLocalEulerAngles;

            [System.NonSerialized]
            private float _baseAlpha = 1f;

            [System.NonSerialized]
            private Sequence _activeTween;

            [System.NonSerialized]
            private int _animationVersion;

            public RectTransform ResolvedTweenTarget => _resolvedTweenTarget;
            public bool HasCachedAnimationDefaults => _animationDefaultsCached;
            public Vector2 BaseAnchoredPosition => _baseAnchoredPosition;
            public Vector3 BaseLocalScale => _baseLocalScale;
            public Vector3 BaseLocalEulerAngles => _baseLocalEulerAngles;
            public float BaseAlpha => _baseAlpha;
            public Sequence ActiveTween => _activeTween;
            public int AnimationVersion => _animationVersion;

            /// <summary>
            /// Captura a posição visível do painel para servir como estado final da abertura.
            /// </summary>
            public void CacheAnimationDefaults()
            {
                openTween ??= new DotweenAnimationSettings();
                closeTween ??= new DotweenAnimationSettings
                {
                    slideDirection = PanelSlideDirection.Left
                };

                if (panelObject == null)
                {
                    return;
                }

                RectTransform resolvedTweenTarget = tweenTarget != null
                    ? tweenTarget
                    : panelObject.GetComponent<RectTransform>();

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

                if (canvasGroup == null)
                {
                    canvasGroup = panelObject.GetComponent<CanvasGroup>();
                }

                if (canvasGroup != null)
                {
                    _baseAlpha = canvasGroup.alpha;
                }

                _animationDefaultsCached = true;
            }

            /// <summary>
            /// Cancela a animação atual e cria um identificador para a nova transição.
            /// </summary>
            public int StartNewAnimation()
            {
                _animationVersion++;
                _activeTween?.Kill(false);
                _activeTween = null;
                return _animationVersion;
            }

            /// <summary>
            /// Guarda a sequência DOTween ativa para que uma nova transição possa cancelá-la.
            /// </summary>
            public void SetActiveTween(Sequence tween)
            {
                _activeTween = tween;
            }
        }

        [Header("Panels")]
        [SerializeField]
        private List<PanelBinding> _panelBindings = new();

        [Header("Animator")]
        [Tooltip("Tempo usado para esperar a animação de fechamento do Animator antes de desativar o painel.")]
        [SerializeField]
        private float _closeAnimationDuration = 0.25f;

        private readonly List<InputAction> _inputActions = new();

        protected virtual void Awake()
        {
            foreach (var binding in _panelBindings)
            {
                binding?.CacheAnimationDefaults();
            }
        }

        protected virtual void OnEnable()
        {
            foreach (var binding in _panelBindings)
            {
                if (binding == null)
                {
                    continue;
                }

                var action = BuildInputAction(binding.activationKey);

                action.performed += _ =>
                {
                    HandlePanelActivation(binding);
                };

                action.Enable();
                _inputActions.Add(action);
            }
        }

        protected virtual void OnDisable()
        {
            foreach (var action in _inputActions)
            {
                action.Disable();
                action.Dispose();
            }

            _inputActions.Clear();

            foreach (var binding in _panelBindings)
            {
                binding?.StartNewAnimation();
            }
        }

        private void HandlePanelActivation(PanelBinding targetBinding)
        {
            if (targetBinding == null || targetBinding.panelObject == null)
            {
                Debug.LogWarning("Painel não configurado.", this);
                return;
            }

            bool isAlreadyActive = targetBinding.panelObject.activeSelf;

            // Fecha todos os outros painéis.
            foreach (var binding in _panelBindings)
            {
                if (binding == null || binding.panelObject == null)
                {
                    continue;
                }

                if (binding.panelObject == targetBinding.panelObject)
                {
                    continue;
                }

                ClosePanel(binding);
            }

            // Fecha painéis extras configurados.
            foreach (var extraPanel in targetBinding.panelsToDisable)
            {
                if (extraPanel != null)
                {
                    extraPanel.SetActive(false);
                }
            }

            // Toggle do painel atual.
            if (isAlreadyActive)
            {
                ClosePanel(targetBinding);
            }
            else
            {
                OpenPanel(targetBinding);
            }
        }

        private void OpenPanel(PanelBinding binding)
        {
            if (binding == null || binding.panelObject == null)
            {
                return;
            }

            binding.CacheAnimationDefaults();
            binding.StartNewAnimation();
            binding.panelObject.SetActive(true);

            bool usesDotween = UsesDotween(binding);
            if (usesDotween)
            {
                PlayDotweenAnimation(binding, binding.openTween, true);
            }

            if (UsesAnimator(binding))
            {
                TriggerOpenAnimators(binding);
            }
        }

        private async void ClosePanel(PanelBinding binding)
        {
            if (binding == null || binding.panelObject == null || !binding.panelObject.activeSelf)
            {
                return;
            }

            binding.CacheAnimationDefaults();
            int animationVersion = binding.StartNewAnimation();
            float waitDuration = 0f;

            if (UsesDotween(binding))
            {
                bool tweenStarted = PlayDotweenAnimation(binding, binding.closeTween, false);
                if (tweenStarted)
                {
                    waitDuration = binding.closeTween.duration;
                }
            }

            if (UsesAnimator(binding) && TriggerCloseAnimators(binding))
            {
                waitDuration = Mathf.Max(waitDuration, _closeAnimationDuration);
            }

            if (waitDuration > 0f)
            {
                await Awaitable.WaitForSecondsAsync(waitDuration);
            }

            if (binding.AnimationVersion != animationVersion || binding.panelObject == null)
            {
                return;
            }

            binding.panelObject.SetActive(false);
        }

        private bool PlayDotweenAnimation(
            PanelBinding binding,
            DotweenAnimationSettings settings,
            bool opening)
        {
            if (binding.ResolvedTweenTarget == null || settings == null)
            {
                return false;
            }

            if (!binding.HasCachedAnimationDefaults)
            {
                binding.CacheAnimationDefaults();
            }

            RectTransform target = binding.ResolvedTweenTarget;
            Sequence sequence = DOTween.Sequence();
            float duration = Mathf.Max(0f, settings.duration);
            bool hasAnimation = false;

            if (opening)
            {
                ApplyOpeningState(binding, settings);
            }

            if (IncludesSlide(settings.style))
            {
                Vector2 destination = opening
                    ? binding.BaseAnchoredPosition
                    : binding.BaseAnchoredPosition + GetSlideDisplacement(settings);

                sequence.Join(target.DOAnchorPos(destination, duration));
                hasAnimation = true;
            }

            if (IncludesScale(settings.style))
            {
                Vector3 destination = opening
                    ? binding.BaseLocalScale
                    : Vector3.Scale(binding.BaseLocalScale, settings.scaleMultiplier);

                sequence.Join(target.DOScale(destination, duration));
                hasAnimation = true;
            }

            if (IncludesRotation(settings.style))
            {
                Vector3 destination = opening
                    ? binding.BaseLocalEulerAngles
                    : binding.BaseLocalEulerAngles + GetRotationOffset(settings);

                sequence.Join(target.DOLocalRotate(destination, duration));
                hasAnimation = true;
            }

            if (settings.animateAlpha && binding.canvasGroup != null)
            {
                float destination = opening ? binding.BaseAlpha : settings.hiddenAlpha;
                sequence.Join(binding.canvasGroup.DOFade(destination, duration));
                hasAnimation = true;
            }

            if (!hasAnimation || duration <= 0f)
            {
                ApplyFinalState(binding, settings, opening);
                sequence.Kill(false);
                return false;
            }

            sequence.SetEase(settings.ease);
            sequence.SetUpdate(settings.useUnscaledTime);
            binding.SetActiveTween(sequence);
            return true;
        }

        private static void ApplyOpeningState(PanelBinding binding, DotweenAnimationSettings settings)
        {
            RectTransform target = binding.ResolvedTweenTarget;

            if (IncludesSlide(settings.style))
            {
                target.anchoredPosition = binding.BaseAnchoredPosition - GetSlideDisplacement(settings);
            }

            if (IncludesScale(settings.style))
            {
                target.localScale = Vector3.Scale(binding.BaseLocalScale, settings.scaleMultiplier);
            }

            if (IncludesRotation(settings.style))
            {
                target.localEulerAngles = binding.BaseLocalEulerAngles + GetRotationOffset(settings);
            }

            if (settings.animateAlpha && binding.canvasGroup != null)
            {
                binding.canvasGroup.alpha = settings.hiddenAlpha;
            }
        }

        private static Vector2 GetSlideDisplacement(DotweenAnimationSettings settings)
        {
            return settings.slideDirection switch
            {
                PanelSlideDirection.Left => Vector2.left * settings.slideDistance,
                PanelSlideDirection.Right => Vector2.right * settings.slideDistance,
                PanelSlideDirection.Up => Vector2.up * settings.slideDistance,
                PanelSlideDirection.Down => Vector2.down * settings.slideDistance,
                _ => Vector2.zero
            };
        }

        private static void ApplyFinalState(
            PanelBinding binding,
            DotweenAnimationSettings settings,
            bool opening)
        {
            RectTransform target = binding.ResolvedTweenTarget;
            if (target != null)
            {
                if (IncludesSlide(settings.style))
                {
                    target.anchoredPosition = opening
                        ? binding.BaseAnchoredPosition
                        : binding.BaseAnchoredPosition + GetSlideDisplacement(settings);
                }

                if (IncludesScale(settings.style))
                {
                    target.localScale = opening
                        ? binding.BaseLocalScale
                        : Vector3.Scale(binding.BaseLocalScale, settings.scaleMultiplier);
                }

                if (IncludesRotation(settings.style))
                {
                    target.localEulerAngles = opening
                        ? binding.BaseLocalEulerAngles
                        : binding.BaseLocalEulerAngles + GetRotationOffset(settings);
                }
            }

            if (settings.animateAlpha && binding.canvasGroup != null)
            {
                binding.canvasGroup.alpha = opening ? binding.BaseAlpha : settings.hiddenAlpha;
            }
        }

        private static Vector3 GetRotationOffset(DotweenAnimationSettings settings)
        {
            float degrees = Mathf.Abs(settings.rotationDegrees);
            if (settings.rotationDirection == PanelRotationDirection.Clockwise)
            {
                degrees *= -1f;
            }

            return new Vector3(0f, 0f, degrees);
        }

        private static bool IncludesSlide(DotweenAnimationStyle style)
        {
            return style == DotweenAnimationStyle.Slide
                || style == DotweenAnimationStyle.SlideAndScale
                || style == DotweenAnimationStyle.SlideAndRotate
                || style == DotweenAnimationStyle.SlideScaleAndRotate;
        }

        private static bool IncludesScale(DotweenAnimationStyle style)
        {
            return style == DotweenAnimationStyle.Scale
                || style == DotweenAnimationStyle.SlideAndScale
                || style == DotweenAnimationStyle.ScaleAndRotate
                || style == DotweenAnimationStyle.SlideScaleAndRotate;
        }

        private static bool IncludesRotation(DotweenAnimationStyle style)
        {
            return style == DotweenAnimationStyle.Rotate
                || style == DotweenAnimationStyle.SlideAndRotate
                || style == DotweenAnimationStyle.ScaleAndRotate
                || style == DotweenAnimationStyle.SlideScaleAndRotate;
        }

        private static bool UsesAnimator(PanelBinding binding)
        {
            return binding.animationMode == PanelAnimationMode.Animator
                || binding.animationMode == PanelAnimationMode.AnimatorAndDOTween;
        }

        private static bool TriggerOpenAnimators(PanelBinding binding)
        {
            bool hasAnimator = false;

            if (binding.animator != null)
            {
                binding.animator.ResetTrigger(binding.closeTrigger);
                binding.animator.SetTrigger(binding.openTrigger);
                hasAnimator = true;
            }

            if (binding.additionalAnimators == null)
            {
                return hasAnimator;
            }

            foreach (var animator in binding.additionalAnimators)
            {
                if (animator == null)
                {
                    continue;
                }

                animator.ResetTrigger(binding.closeTrigger);
                animator.SetTrigger(binding.openTrigger);
                hasAnimator = true;
            }

            return hasAnimator;
        }

        private static bool TriggerCloseAnimators(PanelBinding binding)
        {
            bool hasAnimator = false;

            if (binding.animator != null)
            {
                binding.animator.ResetTrigger(binding.openTrigger);
                binding.animator.SetTrigger(binding.closeTrigger);
                hasAnimator = true;
            }

            if (binding.additionalAnimators == null)
            {
                return hasAnimator;
            }

            foreach (var animator in binding.additionalAnimators)
            {
                if (animator == null)
                {
                    continue;
                }

                animator.ResetTrigger(binding.openTrigger);
                animator.SetTrigger(binding.closeTrigger);
                hasAnimator = true;
            }

            return hasAnimator;
        }

        private static bool UsesDotween(PanelBinding binding)
        {
            return binding.animationMode == PanelAnimationMode.DOTween
                || binding.animationMode == PanelAnimationMode.AnimatorAndDOTween;
        }

        private static InputAction BuildInputAction(Key activationKey)
        {
            var keyboardBindingPath =
                $"<Keyboard>/{activationKey.ToString().ToLowerInvariant()}";

            var inputAction = new InputAction(
                name: $"Panel_{activationKey}",
                type: InputActionType.Button);

            inputAction.AddBinding(keyboardBindingPath);

            return inputAction;
        }
    }

    [AddComponentMenu("Input/Panel Input Controller")]
    public sealed class PanelInputController : ActivateObjectByInput
    {
    }
}
