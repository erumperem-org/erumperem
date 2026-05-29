using DG.Tweening;
using Game.Core.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Erumperem.Combat.HealthBars
{
    /// <summary>
    /// HUD diegética por unidade. Faz poll do HP em <c>LateUpdate</c> e dispara animações DOTween:
    /// vermelho = HP actual (slider), laranja = trail de dano (lento), verde = trail de cura (rápido).
    /// Não conhece o <see cref="CombatPrototypeController"/> directamente; só lê <see cref="Combatant.Health"/>
    /// via referência fornecida em <see cref="Configure"/>.
    /// </summary>
    public sealed class HealthBarHudView : MonoBehaviour
    {
        [Header("Bindings (Slider obrigatório)")]
        [Tooltip("Slider standard cuja value (0..1) representa o HP corrente. Animado em DOValue.")]
        [SerializeField] private Slider _currentHpSlider;

        [Tooltip("Imagem do fill do slider. Se vazio, é resolvido a partir de Slider.fillRect na Awake (para mudar a cor).")]
        [SerializeField] private Image _currentHpFillImage;

        [Tooltip("Opcional: Image extra (Type=Filled, FillMethod=Horizontal, FillOrigin=Left) por trás do slider, " +
                 "que mostra o trail de dano (laranja, lento) ou de cura (verde, rápido). " +
                 "Sem ela, o componente continua a funcionar mas sem efeito de trail.")]
        [SerializeField] private Image _trailingFillImage;

        [Tooltip("Opcional: TMP que mostra HP actual / HP máximo (ex.: 75/100). Auto-resolvido por nome 'HealthBarText'.")]
        [SerializeField] private TextMeshProUGUI _healthBarTextLabel;

        [Header("Cores (LERP via troca directa de cor)")]
        [SerializeField] private Color _currentHpColor = new(0.85f, 0.18f, 0.18f, 1f);
        [SerializeField] private Color _damageTrailColor = new(0.95f, 0.55f, 0.15f, 1f);
        [SerializeField] private Color _healingTrailColor = new(0.35f, 0.85f, 0.4f, 1f);

        [Header("Durações (segundos)")]
        [Tooltip("Duração do tween rápido. Dano = slider corrente desce rápido. Cura = trail verde sobe rápido.")]
        [SerializeField] private float _fastTweenSeconds = 0.18f;

        [Tooltip("Duração do tween lento. Dano = trail laranja desce devagar. Cura = slider corrente sobe devagar.")]
        [SerializeField] private float _slowTweenSeconds = 0.6f;

        [Header("Juice (DOPunchScale ao perder vida)")]
        [Tooltip("Aplicado no transform raiz da barra ao receber dano. (0,0,0) desactiva.")]
        [SerializeField] private Vector3 _damagePunchScale = new(0.10f, 0.18f, 0f);
        [SerializeField] private float _damagePunchDuration = 0.28f;
        [SerializeField] private int _damagePunchVibrato = 8;
        [SerializeField] private float _damagePunchElasticity = 0.45f;

        [Header("Comportamento")]
        [Tooltip("Esconde a barra quando o combatente está marcado como morto (Health.IsDead).")]
        [SerializeField] private bool _hideWhenCombatantDead = true;

        private const string CurrentHpTweenId = "HealthBarCurrentHpTween";
        private const string TrailingHpTweenId = "HealthBarTrailingHpTween";
        private const string DamagePunchTweenId = "HealthBarDamagePunchTween";

        private CombatPrototypeController _combatSession;
        private string _combatantId = "";
        private float _lastSyncedHpPercent = -1f;
        private bool _hasInitializedFromBattleState;
        private int _lastDisplayedCurrentHp = -1;
        private int _lastDisplayedMaxHp = -1;

        public void Configure(CombatPrototypeController combatSession, string combatantId)
        {
            _combatSession = combatSession;
            _combatantId = combatantId ?? "";
            _hasInitializedFromBattleState = false;
            _lastSyncedHpPercent = -1f;
            _lastDisplayedCurrentHp = -1;
            _lastDisplayedMaxHp = -1;
        }

        private void Awake()
        {
            ResolveFillImageFromSliderIfMissing();
            ResolveHealthBarTextLabelIfMissing();
            ApplyInitialColors();
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

            if (combatant.Health.IsDead && _hideWhenCombatantDead)
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

            SyncHealthBarTextLabel(combatant);

            var targetHpPercent = ComputeHpPercentSafely(combatant);
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
            DOTween.Kill(GetTweenIdScopedToInstance(CurrentHpTweenId), false);
            DOTween.Kill(GetTweenIdScopedToInstance(TrailingHpTweenId), false);

            if (_trailingFillImage != null)
            {
                _trailingFillImage.color = _damageTrailColor;
                _trailingFillImage.fillAmount = Mathf.Max(_trailingFillImage.fillAmount, _lastSyncedHpPercent);
                _trailingFillImage
                    .DOFillAmount(targetHpPercent, _slowTweenSeconds)
                    .SetEase(Ease.OutCubic)
                    .SetId(GetTweenIdScopedToInstance(TrailingHpTweenId))
                    .SetLink(gameObject);
            }

            if (_currentHpFillImage != null)
            {
                _currentHpFillImage.color = _currentHpColor;
            }

            if (_currentHpSlider != null)
            {
                _currentHpSlider
                    .DOValue(targetHpPercent, _fastTweenSeconds)
                    .SetEase(Ease.OutQuad)
                    .SetId(GetTweenIdScopedToInstance(CurrentHpTweenId))
                    .SetLink(gameObject);
            }

            PlayDamagePunch();
        }

        private void AnimateHealingTransition(float targetHpPercent)
        {
            DOTween.Kill(GetTweenIdScopedToInstance(CurrentHpTweenId), false);
            DOTween.Kill(GetTweenIdScopedToInstance(TrailingHpTweenId), false);

            if (_trailingFillImage != null)
            {
                _trailingFillImage.color = _healingTrailColor;
                _trailingFillImage.fillAmount = Mathf.Min(_trailingFillImage.fillAmount, _lastSyncedHpPercent);
                _trailingFillImage
                    .DOFillAmount(targetHpPercent, _fastTweenSeconds)
                    .SetEase(Ease.OutQuad)
                    .SetId(GetTweenIdScopedToInstance(TrailingHpTweenId))
                    .SetLink(gameObject);
            }

            if (_currentHpFillImage != null)
            {
                _currentHpFillImage.color = _currentHpColor;
            }

            if (_currentHpSlider != null)
            {
                _currentHpSlider
                    .DOValue(targetHpPercent, _slowTweenSeconds)
                    .SetEase(Ease.InOutQuad)
                    .SetId(GetTweenIdScopedToInstance(CurrentHpTweenId))
                    .SetLink(gameObject);
            }
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
            _healthBarTextLabel.text = $"{currentHp}/{maxHp}";
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
