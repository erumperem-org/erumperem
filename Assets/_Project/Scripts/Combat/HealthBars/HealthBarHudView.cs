using DG.Tweening;
using Game.Core.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Erumperem.Combat.HealthBars
{
    /// <summary>
    /// HUD diegética por unidade. Visibilidade vem do <see cref="CombatHealthBarsBinder"/> (aliados, alvo selecionado, hover)
    /// e permanece ligada enquanto animações de trail/punch correm. Em <c>LateUpdate</c> sincroniza HP.
    /// O fill principal salta para o HP real. O trail fica para trás (amarelo no dano, verde na cura) e faz catch-up com DOTween.
    /// Não conhece o <see cref="CombatPrototypeController"/> directamente; só lê <see cref="Combatant.Health"/>
    /// via referência fornecida em <see cref="Configure"/>.
    /// </summary>
    [DefaultExecutionOrder(35)]
    public sealed class HealthBarHudView : MonoBehaviour
    {
        [Header("Bindings (Slider obrigatório)")]
        [Tooltip("Slider standard cuja value (0..1) representa o HP corrente. Animado em DOValue.")]
        [SerializeField] private Slider _currentHpSlider;

        [Tooltip("Imagem do fill do slider. Se vazio, é resolvido a partir de Slider.fillRect na Awake (para mudar a cor).")]
        [SerializeField] private Image _currentHpFillImage;

        [Tooltip("Opcional: Image extra (Type=Filled, FillMethod=Horizontal, FillOrigin=Left) por trás do slider, " +
                 "que mostra o trail de dano (amarelo) ou de cura (verde), atrasado em relação ao fill. " +
                 "Sem ela, o componente continua a funcionar mas sem efeito de trail.")]
        [SerializeField] private Image _trailingFillImage;

        [Tooltip("Opcional: TMP que mostra HP actual / HP máximo (ex.: 75/100). Auto-resolvido por nome 'HealthBarText'.")]
        [SerializeField] private TextMeshProUGUI _healthBarTextLabel;

        [Tooltip("Opcional: TMP flutuante acima da barra com o range de dano previsto. Auto-resolvido por 'DamagePreviewFloatingText'.")]
        [SerializeField] private TextMeshProUGUI _damagePreviewFloatingTextLabel;

        [Tooltip("Opcional: ícone de caveira quando o golpe garante morte ao acertar. Auto-resolvido por 'KillPreviewSkull'.")]
        [SerializeField] private GameObject _lethalKillSkullIndicator;

        [Header("Cores (LERP via troca directa de cor)")]
        [SerializeField] private Color _currentHpColor = new(0.85f, 0.18f, 0.18f, 1f);
        [SerializeField] private Color _damageTrailColor = new(0.95f, 0.86f, 0.12f, 1f);
        [SerializeField] private Color _healingTrailColor = new(0.35f, 0.85f, 0.4f, 1f);

        [Header("Durações (segundos)")]
        [Tooltip("Tempo em que o trail fica parado no valor antigo antes de começar o catch-up.")]
        [SerializeField] private float _trailHoldSeconds = 0.18f;

        [Tooltip("Duração do catch-up do trail (amarelo no dano, verde na cura) até o HP real.")]
        [SerializeField] private float _trailCatchUpSeconds = 0.6f;

        [Header("Juice (DOPunchScale ao perder vida)")]
        [Tooltip("Aplicado no transform raiz da barra ao receber dano. (0,0,0) desactiva.")]
        [SerializeField] private Vector3 _damagePunchScale = new(0.10f, 0.18f, 0f);
        [SerializeField] private float _damagePunchDuration = 0.28f;
        [SerializeField] private int _damagePunchVibrato = 8;
        [SerializeField] private float _damagePunchElasticity = 0.45f;

        [Header("Comportamento")]
        [Tooltip("Após morte, esconde a barra só quando o HP visual chegou a 0 e as animações de trail/punch terminaram.")]
        [SerializeField] private bool _hideWhenCombatantDead = true;

        [Tooltip("Controla alpha da barra no hover. Se vazio, é criado/resolvido no Awake.")]
        [SerializeField] private CanvasGroup _visibilityCanvasGroup;

        private const string CurrentHpTweenId = "HealthBarCurrentHpTween";
        private const string TrailingHpTweenId = "HealthBarTrailingHpTween";
        private const string DamagePunchTweenId = "HealthBarDamagePunchTween";

        private CombatPrototypeController _combatSession;
        private string _combatantId = "";
        private float _lastSyncedHpPercent = -1f;
        private bool _hasInitializedFromBattleState;
        private int _lastDisplayedCurrentHp = -1;
        private int _lastDisplayedMaxHp = -1;
        private bool _hasActiveSkillDamagePreview;
        private int _previewMinHpAfterHit;
        private int _previewMaxHpAfterHit;
        private bool _binderRequestsVisible;

        /// <summary>Trail ou punch DOTween ainda em execução — a barra deve permanecer visível.</summary>
        public bool IsHealthBarPresentationActive =>
            DOTween.IsTweening(GetTweenIdScopedToInstance(TrailingHpTweenId))
            || DOTween.IsTweening(GetTweenIdScopedToInstance(DamagePunchTweenId));

        public void Configure(CombatPrototypeController combatSession, string combatantId)
        {
            _combatSession = combatSession;
            _combatantId = combatantId ?? "";
            _hasInitializedFromBattleState = false;
            _lastSyncedHpPercent = -1f;
            _lastDisplayedCurrentHp = -1;
            _lastDisplayedMaxHp = -1;
            _binderRequestsVisible = false;
            ClearSkillDamagePreview();
            EnsureVisibilityCanvasGroup();
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            ApplyHoverVisibilityState(forceHidden: true);
        }

        public void SetHoverVisible(bool isBinderRequestedVisible)
        {
            var binderVisibilityChanged = _binderRequestsVisible != isBinderRequestedVisible;
            if (!binderVisibilityChanged && !NeedsVisibilityRefresh())
            {
                return;
            }

            _binderRequestsVisible = isBinderRequestedVisible;
            if (!isBinderRequestedVisible && !IsHealthBarPresentationActive)
            {
                ClearSkillDamagePreview();
            }
            else if (isBinderRequestedVisible)
            {
                _hasInitializedFromBattleState = false;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            ApplyHoverVisibilityState();
        }

        public void SetSkillDamagePreview(
            int minDamageOnHit,
            int maxDamageOnHit,
            int minHpAfterHit,
            int maxHpAfterHit,
            bool isGuaranteedKillOnHit)
        {
            _hasActiveSkillDamagePreview = true;
            _previewMinHpAfterHit = minHpAfterHit;
            _previewMaxHpAfterHit = maxHpAfterHit;

            if (_lethalKillSkullIndicator != null)
            {
                _lethalKillSkullIndicator.SetActive(isGuaranteedKillOnHit);
            }
        }

        public void ClearSkillDamagePreview()
        {
            if (!_hasActiveSkillDamagePreview)
            {
                return;
            }

            _hasActiveSkillDamagePreview = false;
            _lastDisplayedCurrentHp = -1;
            _lastDisplayedMaxHp = -1;

            if (_damagePreviewFloatingTextLabel != null)
            {
                _damagePreviewFloatingTextLabel.gameObject.SetActive(false);
            }

            if (_lethalKillSkullIndicator != null)
            {
                _lethalKillSkullIndicator.SetActive(false);
            }

            RestoreBarsAfterSkillDamagePreview();
        }

        /// <summary>Força re-sync das barras após alteração externa de HP (ex.: cheat F9).</summary>
        public void InvalidateHealthDisplayCache()
        {
            _hasInitializedFromBattleState = false;
            _lastSyncedHpPercent = -1f;
            _lastDisplayedCurrentHp = -1;
            _lastDisplayedMaxHp = -1;
        }

        private void Awake()
        {
            EnsureVisibilityCanvasGroup();
            ResolveFillImageFromSliderIfMissing();
            ResolveHealthBarTextLabelIfMissing();
            ResolveDamagePreviewWidgetsIfMissing();
            ApplyInitialColors();
            DisableFloatingDamagePreviewTextPermanently();
            ClearSkillDamagePreview();
        }

        private void OnDestroy()
        {
            KillAllTweens();
        }

        private void OnDisable()
        {
            KillAllTweens();
        }

        private void LateUpdate()
        {
            if (_combatSession == null || string.IsNullOrEmpty(_combatantId))
            {
                return;
            }

            if (!_combatSession.IsBattleOngoing)
            {
                return;
            }

            var combatant = _combatSession.FindCombatantById(_combatantId);
            if (combatant == null)
            {
                return;
            }

            ApplyHoverVisibilityState();

            SyncHealthBarTextLabel(combatant);

            if (_hasActiveSkillDamagePreview)
            {
                ApplySkillDamagePreviewToSlider(combatant);
                return;
            }

            var isPresentingDeathHealthDrop = IsPresentingDeathHealthDrop(combatant);
            var shouldPresentHealthBar = _binderRequestsVisible
                                         || IsHealthBarPresentationActive
                                         || isPresentingDeathHealthDrop;
            var targetHpPercent = ComputeHpPercentSafely(combatant);

            if (!shouldPresentHealthBar)
            {
                if (_hasInitializedFromBattleState
                    && !Mathf.Approximately(targetHpPercent, _lastSyncedHpPercent))
                {
                    if (targetHpPercent < _lastSyncedHpPercent)
                    {
                        AnimateDamageTransition(targetHpPercent);
                    }
                    else
                    {
                        AnimateHealingTransition(targetHpPercent);
                    }

                    _lastSyncedHpPercent = targetHpPercent;
                }
                else
                {
                    SyncHiddenHealthState(combatant);
                }

                ApplyHoverVisibilityState();
                return;
            }

            if (!_hasInitializedFromBattleState)
            {
                SnapBarsToValue(targetHpPercent);
                _lastSyncedHpPercent = targetHpPercent;
                _hasInitializedFromBattleState = true;
                return;
            }

            if (Mathf.Approximately(targetHpPercent, _lastSyncedHpPercent))
            {
                return;
            }

            if (targetHpPercent < _lastSyncedHpPercent)
            {
                AnimateDamageTransition(targetHpPercent);
            }
            else
            {
                AnimateHealingTransition(targetHpPercent);
            }

            _lastSyncedHpPercent = targetHpPercent;
        }

        private void AnimateDamageTransition(float targetHpPercent)
        {
            SnapCurrentFillToPercent(targetHpPercent);
            PlayDelayedTrailCatchUp(targetHpPercent, _damageTrailColor, keepHigherTrailValue: true);
            PlayDamagePunch();
        }

        private void AnimateHealingTransition(float targetHpPercent)
        {
            SnapCurrentFillToPercent(targetHpPercent);
            PlayDelayedTrailCatchUp(targetHpPercent, _healingTrailColor, keepHigherTrailValue: false);
        }

        private void SnapCurrentFillToPercent(float targetHpPercent)
        {
            DOTween.Kill(GetTweenIdScopedToInstance(CurrentHpTweenId), false);

            if (_currentHpFillImage != null)
            {
                _currentHpFillImage.color = _currentHpColor;
            }

            if (_currentHpSlider != null)
            {
                _currentHpSlider.value = targetHpPercent;
            }
        }

        private void PlayDelayedTrailCatchUp(float targetHpPercent, Color trailColor, bool keepHigherTrailValue)
        {
            DOTween.Kill(GetTweenIdScopedToInstance(TrailingHpTweenId), false);

            if (_trailingFillImage == null)
            {
                return;
            }

            _trailingFillImage.color = trailColor;
            var heldFillAmount = keepHigherTrailValue
                ? Mathf.Max(_trailingFillImage.fillAmount, _lastSyncedHpPercent)
                : Mathf.Min(_trailingFillImage.fillAmount, _lastSyncedHpPercent);
            _trailingFillImage.fillAmount = heldFillAmount;

            var catchUpSeconds = Mathf.Max(0.05f, _trailCatchUpSeconds);
            var holdSeconds = Mathf.Max(0f, _trailHoldSeconds);
            DOTween.Sequence()
                .AppendInterval(holdSeconds)
                .Append(_trailingFillImage.DOFillAmount(targetHpPercent, catchUpSeconds).SetEase(Ease.OutCubic))
                .SetId(GetTweenIdScopedToInstance(TrailingHpTweenId))
                .SetLink(gameObject);
        }

        private void PlayDamagePunch()
        {
            if (_damagePunchScale == Vector3.zero || _damagePunchDuration <= 0f)
            {
                return;
            }

            DOTween.Kill(GetTweenIdScopedToInstance(DamagePunchTweenId), false);
            transform
                .DOPunchScale(_damagePunchScale, _damagePunchDuration, _damagePunchVibrato, _damagePunchElasticity)
                .SetId(GetTweenIdScopedToInstance(DamagePunchTweenId))
                .SetLink(gameObject);
        }

        private void SnapBarsToValue(float hpPercent)
        {
            DOTween.Kill(GetTweenIdScopedToInstance(CurrentHpTweenId), false);
            DOTween.Kill(GetTweenIdScopedToInstance(TrailingHpTweenId), false);

            if (_currentHpSlider != null)
            {
                _currentHpSlider.value = hpPercent;
            }

            if (_trailingFillImage != null)
            {
                _trailingFillImage.fillAmount = hpPercent;
                _trailingFillImage.color = _damageTrailColor;
            }

            if (_currentHpFillImage != null)
            {
                _currentHpFillImage.color = _currentHpColor;
            }
        }

        private static float ComputeHpPercentSafely(Combatant combatant)
        {
            if (combatant.Health.MaxHp <= 0)
            {
                return 0f;
            }

            var raw = (float)combatant.Health.CurrentHp / combatant.Health.MaxHp;
            return Mathf.Clamp01(raw);
        }

        private void ResolveFillImageFromSliderIfMissing()
        {
            if (_currentHpFillImage != null || _currentHpSlider == null || _currentHpSlider.fillRect == null)
            {
                return;
            }

            _currentHpFillImage = _currentHpSlider.fillRect.GetComponent<Image>();
        }

        private void ResolveHealthBarTextLabelIfMissing()
        {
            if (_healthBarTextLabel != null)
            {
                return;
            }

            var textLabels = GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
            foreach (var textLabel in textLabels)
            {
                if (textLabel.name == "HealthBarText")
                {
                    _healthBarTextLabel = textLabel;
                    return;
                }
            }
        }

        private void SyncHealthBarTextLabel(Combatant combatant)
        {
            if (_healthBarTextLabel == null || combatant == null)
            {
                return;
            }

            var currentHp = combatant.Health.CurrentHp;
            var maxHp = combatant.Health.MaxHp;
            if (currentHp == _lastDisplayedCurrentHp && maxHp == _lastDisplayedMaxHp)
            {
                return;
            }

            _lastDisplayedCurrentHp = currentHp;
            _lastDisplayedMaxHp = maxHp;

            if (_hasActiveSkillDamagePreview)
            {
                _healthBarTextLabel.text = FormatHealthTextWithDamagePreview(currentHp, maxHp);
                return;
            }

            _healthBarTextLabel.text = $"{currentHp}/{maxHp}";
        }

        private string FormatHealthTextWithDamagePreview(int currentHp, int maxHp)
        {
            if (_previewMinHpAfterHit == _previewMaxHpAfterHit)
            {
                return $"{currentHp}/{maxHp} → {_previewMinHpAfterHit}/{maxHp}";
            }

            return $"{currentHp}/{maxHp} → {_previewMinHpAfterHit}–{_previewMaxHpAfterHit}/{maxHp}";
        }

        private void ApplySkillDamagePreviewToSlider(Combatant combatant)
        {
            if (_currentHpSlider == null || combatant.Health.MaxHp <= 0)
            {
                return;
            }

            DOTween.Kill(GetTweenIdScopedToInstance(CurrentHpTweenId), false);
            DOTween.Kill(GetTweenIdScopedToInstance(TrailingHpTweenId), false);

            var actualHpPercent = ComputeHpPercentSafely(combatant);
            var previewHpPercent = Mathf.Clamp01((float)_previewMaxHpAfterHit / combatant.Health.MaxHp);
            _currentHpSlider.value = previewHpPercent;

            if (_trailingFillImage != null)
            {
                _trailingFillImage.color = _damageTrailColor;
                _trailingFillImage.fillAmount = actualHpPercent;
            }

            if (_currentHpFillImage != null)
            {
                _currentHpFillImage.color = _currentHpColor;
            }
        }

        private void RestoreBarsAfterSkillDamagePreview()
        {
            if (_combatSession == null || string.IsNullOrEmpty(_combatantId))
            {
                return;
            }

            var combatant = _combatSession.FindCombatantById(_combatantId);
            if (combatant == null)
            {
                return;
            }

            var actualHpPercent = ComputeHpPercentSafely(combatant);
            SnapBarsToValue(actualHpPercent);
        }

        private void ResolveDamagePreviewWidgetsIfMissing()
        {
            if (_damagePreviewFloatingTextLabel == null)
            {
                var textLabels = GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
                foreach (var textLabel in textLabels)
                {
                    if (textLabel == _healthBarTextLabel)
                    {
                        continue;
                    }

                    if (textLabel.name is "DamagePreviewFloatingText" or "DamagePreviewText" or "DamageEstimateText" or "Text (TMP)")
                    {
                        _damagePreviewFloatingTextLabel = textLabel;
                        break;
                    }
                }
            }

            if (_lethalKillSkullIndicator == null)
            {
                foreach (var candidateName in new[] { "KillEstimateIcon", "KillPreviewSkull", "LethalKillSkull", "SkullKillIcon", "Skull" })
                {
                    var skullTransform = FindDescendantTransform(transform, candidateName);
                    if (skullTransform != null)
                    {
                        _lethalKillSkullIndicator = skullTransform.gameObject;
                        break;
                    }
                }
            }
        }

        private void EnsureVisibilityCanvasGroup()
        {
            if (_visibilityCanvasGroup != null)
            {
                return;
            }

            _visibilityCanvasGroup = GetComponent<CanvasGroup>();
            if (_visibilityCanvasGroup == null)
            {
                _visibilityCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _visibilityCanvasGroup.blocksRaycasts = false;
            _visibilityCanvasGroup.interactable = false;
        }

        private void ApplyHoverVisibilityState(bool forceHidden = false)
        {
            EnsureVisibilityCanvasGroup();
            var shouldShow = !forceHidden && ShouldShowHealthBar();
            _visibilityCanvasGroup.alpha = shouldShow ? 1f : 0f;
            _visibilityCanvasGroup.blocksRaycasts = false;
            _visibilityCanvasGroup.interactable = false;
        }

        private bool NeedsVisibilityRefresh()
        {
            EnsureVisibilityCanvasGroup();
            var targetAlpha = ShouldShowHealthBar() ? 1f : 0f;
            return !Mathf.Approximately(_visibilityCanvasGroup.alpha, targetAlpha);
        }

        private bool ShouldShowHealthBar()
        {
            if (_combatSession == null || string.IsNullOrEmpty(_combatantId))
            {
                return _binderRequestsVisible || IsHealthBarPresentationActive;
            }

            var combatant = _combatSession.FindCombatantById(_combatantId);
            return _binderRequestsVisible
                   || IsHealthBarPresentationActive
                   || IsPresentingDeathHealthDrop(combatant);
        }

        private bool IsPresentingDeathHealthDrop(Combatant combatant)
        {
            if (!_hideWhenCombatantDead || combatant == null || !combatant.Health.IsDead)
            {
                return false;
            }

            if (!_hasInitializedFromBattleState)
            {
                return false;
            }

            const float zeroHpPercentEpsilon = 0.001f;
            return _lastSyncedHpPercent > zeroHpPercentEpsilon || IsHealthBarPresentationActive;
        }

        private void SyncHiddenHealthState(Combatant combatant)
        {
            var targetHpPercent = ComputeHpPercentSafely(combatant);
            if (!_hasInitializedFromBattleState ||
                !Mathf.Approximately(targetHpPercent, _lastSyncedHpPercent))
            {
                SnapBarsToValue(targetHpPercent);
                _lastSyncedHpPercent = targetHpPercent;
                _hasInitializedFromBattleState = true;
            }
        }

        private void DisableFloatingDamagePreviewTextPermanently()
        {
            if (_damagePreviewFloatingTextLabel == null)
            {
                return;
            }

            _damagePreviewFloatingTextLabel.gameObject.SetActive(false);
        }

        private static Transform FindDescendantTransform(Transform root, string targetName)
        {
            for (var childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                var child = root.GetChild(childIndex);
                if (child.name == targetName)
                {
                    return child;
                }

                var nested = FindDescendantTransform(child, targetName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void ApplyInitialColors()
        {
            if (_currentHpFillImage != null)
            {
                _currentHpFillImage.color = _currentHpColor;
            }

            if (_trailingFillImage != null)
            {
                _trailingFillImage.color = _damageTrailColor;
            }
        }

        /// <summary>Tweens são scoped ao GameObject — evita conflito quando há várias barras na cena.</summary>
        private string GetTweenIdScopedToInstance(string baseTweenId) =>
            $"{baseTweenId}_{GetInstanceID()}";

        private void KillAllTweens()
        {
            DOTween.Kill(GetTweenIdScopedToInstance(CurrentHpTweenId), false);
            DOTween.Kill(GetTweenIdScopedToInstance(TrailingHpTweenId), false);
            DOTween.Kill(GetTweenIdScopedToInstance(DamagePunchTweenId), false);
        }
    }
}
