using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Erumperem.Combat;

namespace Erumperem.UI
{
    /// <summary>
    /// Vincula um Slider e um Toggle opcional à configuração global de velocidade de combate.
    /// Permite alternar entre ajuste contínuo (esparso) e discreto (múltiplos fixos).
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public sealed class CombatSpeedSlider : MonoBehaviour
    {
        [Header("Componentes")]
        [SerializeField] private Slider speedSlider;
        [SerializeField] private TextMeshProUGUI speedTextLabel;

        [Tooltip("Opcional: Permite ao jogador alternar entre passos fixos e valores livres/esparsos.")]
        [SerializeField] private Toggle snapToStepsToggle;

        [Header("Limites de Velocidade")]
        [SerializeField] private float minSpeed = 0.5f;
        [SerializeField] private float maxSpeed = 4.0f;

        [Header("Configuração de Passos")]
        [Tooltip("Incremento de velocidade ao usar passos fixos (ex: 0.5f para 0.5, 1.0, 1.5, 2.0...).")]
        [SerializeField] private float stepIncrement = 0.5f;

        private const string SnapPrefKey = "CombatSpeedSnapToSteps";
        private bool _isUpdatingValue;

        private void Awake()
        {
            if (speedSlider == null)
            {
                speedSlider = GetComponent<Slider>();
            }
        }

        private void Start()
        {
            InitializeToggle();
            InitializeSlider();
        }

        private void OnEnable()
        {
            if (speedSlider != null)
            {
                speedSlider.value = CombatSpeedSettings.SpeedMultiplier;
                UpdateTextLabel(speedSlider.value);
            }
            if (snapToStepsToggle != null)
            {
                snapToStepsToggle.isOn = PlayerPrefs.GetInt(SnapPrefKey, 0) == 1;
            }
        }

        private void InitializeToggle()
        {
            if (snapToStepsToggle == null) return;

            // Carrega a preferência de snap do jogador
            snapToStepsToggle.isOn = PlayerPrefs.GetInt(SnapPrefKey, 0) == 1;
            snapToStepsToggle.onValueChanged.RemoveListener(HandleToggleChanged);
            snapToStepsToggle.onValueChanged.AddListener(HandleToggleChanged);
        }

        private void InitializeSlider()
        {
            if (speedSlider == null) return;

            speedSlider.minValue = minSpeed;
            speedSlider.maxValue = maxSpeed;

            float savedSpeed = CombatSpeedSettings.SpeedMultiplier;
            speedSlider.value = savedSpeed;

            UpdateTextLabel(savedSpeed);

            speedSlider.onValueChanged.RemoveListener(HandleSpeedChanged);
            speedSlider.onValueChanged.AddListener(HandleSpeedChanged);
        }

        private void HandleToggleChanged(bool isOn)
        {
            PlayerPrefs.SetInt(SnapPrefKey, isOn ? 1 : 0);
            PlayerPrefs.Save();

            if (speedSlider != null)
            {
                // Ajusta imediatamente a posição do slider ao novo modo ao alternar o toggle
                HandleSpeedChanged(speedSlider.value);
            }
        }

        private void HandleSpeedChanged(float value)
        {
            // Impede loops infinitos ao modificar o valor do slider programaticamente
            if (_isUpdatingValue) return;

            float finalValue;
            bool shouldSnap = snapToStepsToggle != null && snapToStepsToggle.isOn;

            if (shouldSnap)
            {
                // Arredonda para o múltiplo mais próximo do incremento definido (ex: 0.5)
                finalValue = Mathf.Round(value / stepIncrement) * stepIncrement;
                finalValue = Mathf.Clamp(finalValue, minSpeed, maxSpeed);

                // Move o handle visual do slider para a posição discreta correspondente
                if (!Mathf.Approximately(speedSlider.value, finalValue))
                {
                    _isUpdatingValue = true;
                    speedSlider.value = finalValue;
                    _isUpdatingValue = false;
                }
            }
            else
            {
                // Arredonda para uma casa decimal (x1.3, x2.7, etc.) para visualização limpa
                finalValue = Mathf.Round(value * 10f) / 10f;
            }

            CombatSpeedSettings.SpeedMultiplier = finalValue;
            UpdateTextLabel(finalValue);
        }

        private void UpdateTextLabel(float value)
        {
            if (speedTextLabel != null)
            {
                speedTextLabel.text = value.ToString("F1") + "x";
            }
        }

        private void OnDestroy()
        {
            if (speedSlider != null)
            {
                speedSlider.onValueChanged.RemoveListener(HandleSpeedChanged);
            }
            if (snapToStepsToggle != null)
            {
                snapToStepsToggle.onValueChanged.RemoveListener(HandleToggleChanged);
            }
        }
    }
}