using System;
using System.Collections;
using TMPro;
using UnityEngine;
using DG.Tweening;
using Erumperem.Combat.Runtime;

namespace Erumperem.Combat.UI
{
    /// <summary>
    /// Apresenta avisos temporários na tela quando cheats de combate são ativados, desativados ou executados.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CombatCheatNotificationPresenter : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private TextMeshProUGUI notificationText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Configurações de Animação")]
        [SerializeField] private float displayDuration = 2.5f;
        [SerializeField] private float fadeDuration = 0.3f;
        [SerializeField] private Vector3 punchScale = new(0.08f, 0.08f, 0f);
        [SerializeField] private float punchDuration = 0.22f;

        [Header("Formatação de Texto")]
        [SerializeField] private string activatedFormat = "<color=#FF3333>[CHEAT]</color> {0}: <color=#4ADE80>ATIVADO</color>";
        [SerializeField] private string deactivatedFormat = "<color=#FF3333>[CHEAT]</color> {0}: <color=#FF3333>DESATIVADO</color>";
        [SerializeField] private string executedFormat = "<color=#FF3333>[CHEAT]</color> {0}: <color=#38BDF8>EXECUTADO</color>";

        private Coroutine _hideRoutine;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void OnEnable()
        {
            CombatDebugCheatController.OnCheatToggled += HandleCheatToggled;
            CombatDebugCheatController.OnCheatExecuted += HandleCheatExecuted;
        }

        private void OnDisable()
        {
            CombatDebugCheatController.OnCheatToggled -= HandleCheatToggled;
            CombatDebugCheatController.OnCheatExecuted -= HandleCheatExecuted;

            KillAllTweensAndRoutines();
        }

        private void HandleCheatToggled(string cheatName, bool active)
        {
            string message = active
                ? string.Format(activatedFormat, cheatName)
                : string.Format(deactivatedFormat, cheatName);

            ShowNotification(message);
        }

        private void HandleCheatExecuted(string cheatName)
        {
            string message = string.Format(executedFormat, cheatName);
            ShowNotification(message);
        }

        private void ShowNotification(string message)
        {
            if (notificationText == null)
            {
                return;
            }

            notificationText.text = message;

            KillAllTweensAndRoutines();

            canvasGroup.DOFade(1f, fadeDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject);

            transform.DOPunchScale(punchScale, punchDuration, 8, 0.5f)
                .SetLink(gameObject);

            _hideRoutine = StartCoroutine(HideAfterDelayRoutine());
        }

        private IEnumerator HideAfterDelayRoutine()
        {
            yield return new WaitForSeconds(displayDuration);

            canvasGroup.DOFade(0f, fadeDuration)
                .SetEase(Ease.InQuad)
                .SetLink(gameObject);
        }

        private void KillAllTweensAndRoutines()
        {
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            canvasGroup.DOKill(false);
            transform.DOKill(false);
            transform.localScale = Vector3.one;
        }
    }
}