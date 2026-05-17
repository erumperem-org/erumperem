// =============================================================================
// InputReader.cs
// Lê o Input System ANTIGO da Unity (Input Manager) e escreve os valores
// no PlayerContext.
//
// RESPONSABILIDADE ÚNICA:
//   Traduzir eventos de hardware em dados de contexto.
//   Não contém lógica de gameplay — apenas leitura e escrita de dados brutos.
//
// COMO CONFIGURAR:
//   Edit > Project Settings > Input Manager
//
// AXES NECESSÁRIOS:
//   Horizontal
//   Vertical
//
// TECLAS PADRÃO:
//   Jump     → Space
//   Crouch   → LeftControl
//   Interact → E
//   UseItem  → F
//   Switch1  → Alpha1
//   Switch2  → Alpha2
//   Switch3  → Alpha3
//
// FLAGS DE "PRESSED":
//   JumpPressed, InteractPressed e UseItemPressed são true APENAS no frame
//   em que o botão é pressionado. O InputReader os reseta no final de cada
//   frame via LateUpdate (após o PlayerController.Update ter lido os valores).
// =============================================================================

using UnityEngine;
using CharacterSystem.Core;

namespace CharacterSystem.Input
{
    /// <summary>
    /// Lê o Input Manager antigo da Unity e popula o PlayerContext.
    /// Deve ser atualizado ANTES do PlayerController.Update().
    /// </summary>
    public class InputReader : MonoBehaviour
    {
        // ── Referências ──────────────────────────────────────────────────────

        /// <summary>
        /// Contexto compartilhado. Injetado pelo PlayerController no Start.
        /// </summary>
        public PlayerContext Context { private get; set; }

        // ── Evento de troca de personagem ────────────────────────────────────

        /// <summary>
        /// Disparado quando o jogador pressiona 1, 2 ou 3.
        /// O CharacterSwitcher assina este evento.
        /// Parâmetro: índice do personagem (0, 1, 2).
        /// </summary>
        public event System.Action<int> OnCharacterSwitchRequested;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Update()
        {
            if (Context == null) return;

            // Lê todos os inputs e escreve no contexto
            ReadLocomotionInput();
            ReadInteractionInput();
            ReadUseItemInput();
            ReadSwitchInput();
        }

        private void LateUpdate()
        {
            if (Context == null) return;

            // Reseta flags "pressed" — válidas apenas no frame de pressão
            Context.JumpPressed     = false;
            Context.InteractPressed = false;
            Context.UseItemPressed  = false;
        }

        // ── Leitura de Input ─────────────────────────────────────────────────

        /// <summary>Lê inputs de movimento, pulo e agachar.</summary>
        private void ReadLocomotionInput()
        {
            Context.MoveInput = new Vector2(
                UnityEngine.Input.GetAxisRaw("Horizontal"),
                UnityEngine.Input.GetAxisRaw("Vertical")
            );

            Context.CrouchHeld = UnityEngine.Input.GetKey(KeyCode.LeftControl);

            // JumpPressed é true apenas no frame de pressão
            if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
                Context.JumpPressed = true;
        }

        /// <summary>Lê input de interação.</summary>
        private void ReadInteractionInput()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
                Context.InteractPressed = true;
        }

        /// <summary>Lê input de uso de item.</summary>
        private void ReadUseItemInput()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F))
                Context.UseItemPressed = true;
        }

        /// <summary>Lê inputs de troca de personagem (teclas 1, 2, 3).</summary>
        private void ReadSwitchInput()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
                OnCharacterSwitchRequested?.Invoke(0);

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
                OnCharacterSwitchRequested?.Invoke(1);

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3))
                OnCharacterSwitchRequested?.Invoke(2);
        }
    }
}