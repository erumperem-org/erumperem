using UnityEngine;

namespace Erumperem.Progression
{
    /// <summary>Liga o OnClick do botão a <see cref="ResetSkillsSave"/>.</summary>
    public sealed class PlayerProgressionResetButton : MonoBehaviour
    {
        [Tooltip("Personagem cujo save de árvore se limpa (ex.: wulfric). Ignorado se _resetEntireFile = true.")]
        [SerializeField] private string _characterId = "wulfric";

        [Tooltip("Se verdadeiro, apaga TODAS as entradas em vez de só o personagem acima.")]
        [SerializeField] private bool _resetEntireFile;

        public void ResetSkillsSave()
        {
            var service = ResolveOrCreateService();
            if (service == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerProgressionResetButton)}: não foi possível obter um {nameof(PlayerProgressionService)}.",
                    this);
                return;
            }

            if (_resetEntireFile || string.IsNullOrWhiteSpace(_characterId))
            {
                service.ResetAllCharacters();
            }
            else
            {
                service.ResetCharacter(_characterId);
            }
        }

        private static PlayerProgressionService ResolveOrCreateService()
        {
            if (PlayerProgressionService.Instance != null)
            {
                return PlayerProgressionService.Instance;
            }

            var existing = FindFirstObjectByType<PlayerProgressionService>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            var serviceRoot = new GameObject(nameof(PlayerProgressionService));
            return serviceRoot.AddComponent<PlayerProgressionService>();
        }
    }
}
