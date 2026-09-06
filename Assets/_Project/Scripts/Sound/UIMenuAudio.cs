using UnityEngine;

namespace Systems.Audio
{
    /// <summary>
    /// Acople este script na raiz do GameObject do seu Menu (ex: Painel de Pausa, Inventário).
    /// O som tocará automaticamente baseando-se no ciclo de vida do objeto, ignorando
    /// se ele foi aberto por um clique, pela tecla ESC ou pela tecla I.
    /// </summary>
    public class UIMenuAudio : MonoBehaviour
    {
        [Header("Menu Sounds (Nomes no AudioManager)")]
        [Tooltip("Som a tocar quando o GameObject for ativado (Menu Abrir)")]
        public string onOpenSound = "MenuOpen";
        
        [Tooltip("Som a tocar quando o GameObject for desativado (Menu Fechar)")]
        public string onCloseSound = "MenuClose";

        private bool _isQuitting = false;

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void OnEnable()
        {
            // Toca o som global de UI através do AudioManager
            if (AudioManager.instance != null && !string.IsNullOrEmpty(onOpenSound))
            {
                AudioManager.instance.PlaySFX(onOpenSound);
            }
        }

        private void OnDisable()
        {
            // Evita tocar o som de fechar menu quando a cena inteira está a ser destruída/fechada
            if (_isQuitting) return;

            if (AudioManager.instance != null && !string.IsNullOrEmpty(onCloseSound))
            {
                AudioManager.instance.PlaySFX(onCloseSound);
            }
        }
    }
}