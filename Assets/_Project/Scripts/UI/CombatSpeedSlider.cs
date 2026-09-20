using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Erumperem.Combat;

namespace Erumperem.UI
{
    /// <summary>
    /// Vincula o Slider e o Toggle à configuração de velocidade de combate.
    /// Não modifica Time.timeScale.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public sealed class CombatSpeedSlider : MonoBehaviour
    {
        [Header("Componentes")]
        [SerializeField] private Slider speedSlider;
        [SerializeField] private TextMeshProUGUI speedTextLabel;

        [Tooltip("Opcional: Permite alternar entre passos fixos e valores contínuos.")]
        [SerializeField] private Toggle snapToStepsToggle;

        [Header("Limites de Velocidade")]
        [SerializeField] private float minSpeed = 0.5f;
        [SerializeField] private float maxSpeed = 4.0f;

        [Header("Configuração de Passos")]
        [Tooltip("Incremento de velocidade ao usar passos fixos.")]
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
                HandleSpeedChanged(speedSlider.value);
            }
        }

        private void HandleSpeedChanged(float value)
        {
            if (_isUpdatingValue) return;

            float finalValue;
            bool shouldSnap = snapToStepsToggle != null && snapToStepsToggle.isOn;

            if (shouldSnap)
            {
                finalValue = Mathf.Round(value / stepIncrement) * stepIncrement;
                finalValue = Mathf.Clamp(finalValue, minSpeed, maxSpeed);

                if (!Mathf.Approximately(speedSlider.value, finalValue))
                {
                    _isUpdatingValue = true;
                    speedSlider.value = finalValue;
                    _isUpdatingValue = false;
                }
            }
            else
            {
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