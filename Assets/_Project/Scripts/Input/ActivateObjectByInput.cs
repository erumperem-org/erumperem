using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Erumperem.Input
{
    public sealed class PanelInputController : MonoBehaviour
    {
        [System.Serializable]
        public class PanelBinding
        {
            [Header("Input")]
            public Key activationKey = Key.E;

            [Header("Panel")]
            public GameObject panelObject;

            [Tooltip("Animator do painel")]
            public Animator animator;

            [Header("Animation")]
            public string openTrigger = "Open";
            public string closeTrigger = "Close";

            [Header("Extra")]
            [Tooltip("Painéis que serão fechados quando este abrir")]
            public List<GameObject> panelsToDisable = new();
        }

        [Header("Panels")]
        [SerializeField]
        private List<PanelBinding> _panelBindings = new();

        [Header("Animation")]
        [Tooltip("Tempo para esperar a animação de fechamento")]
        [SerializeField]
        private float _closeAnimationDuration = 0.25f;

        private readonly List<InputAction> _inputActions = new();

        private void OnEnable()
        {
            foreach (var binding in _panelBindings)
            {
                var action = BuildInputAction(binding.activationKey);

                action.performed += _ =>
                {
                    HandlePanelActivation(binding);
                };

                action.Enable();
                _inputActions.Add(action);
            }
        }

        private void OnDisable()
        {
            foreach (var action in _inputActions)
            {
                action.Disable();
                action.Dispose();
            }

            _inputActions.Clear();
        }

        private void HandlePanelActivation(PanelBinding targetBinding)
        {
            if (targetBinding.panelObject == null)
            {
                Debug.LogWarning("Painel não configurado.", this);
                return;
            }

            bool isAlreadyActive = targetBinding.panelObject.activeSelf;

            // Fecha todos os outros painéis
            foreach (var binding in _panelBindings)
            {
                if (binding.panelObject == null)
                    continue;

                if (binding.panelObject == targetBinding.panelObject)
                    continue;

                ClosePanel(binding);
            }

            // Fecha painéis extras configurados
            foreach (var extraPanel in targetBinding.panelsToDisable)
            {
                if (extraPanel != null)
                {
                    extraPanel.SetActive(false);
                }
            }

            // Toggle do painel atual
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
            binding.panelObject.SetActive(true);

            if (binding.animator != null)
            {
                binding.animator.ResetTrigger(binding.closeTrigger);
                binding.animator.SetTrigger(binding.openTrigger);
            }
        }

        private async void ClosePanel(PanelBinding binding)
        {
            if (!binding.panelObject.activeSelf)
                return;

            if (binding.animator != null)
            {
                binding.animator.ResetTrigger(binding.openTrigger);
                binding.animator.SetTrigger(binding.closeTrigger);

                await Awaitable.WaitForSecondsAsync(_closeAnimationDuration);
            }

            if (binding.panelObject != null)
            {
                binding.panelObject.SetActive(false);
            }
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
}