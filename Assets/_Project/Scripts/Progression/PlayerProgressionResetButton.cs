using UnityEngine;

namespace Erumperem.Progression
{
    /// <summary>Liga o OnClick do botão ao reset da árvore do personagem actual em <see cref="SkillTreeView"/>.</summary>
    public sealed class PlayerProgressionResetButton : MonoBehaviour
    {
        [SerializeField] private SkillTreeView _skillTreeView;

        [Tooltip("Usado só se SkillTreeView estiver vazio. Evita ResetAllCharacters no fluxo normal.")]
        [SerializeField] private string _fallbackCharacterId = "wulfric";

        [Tooltip("Debug: apaga TODAS as entradas do save.")]
        [SerializeField] private bool _resetEntireFile;

        private void Awake()
        {
            _skillTreeView ??= FindFirstObjectByType<SkillTreeView>(FindObjectsInactive.Include);
        }

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

            if (_resetEntireFile)
            {
                service.ResetAllCharacters();
                return;
            }

            var characterId = !string.IsNullOrWhiteSpace(_skillTreeView?.CurrentProgressionCharacterId)
                ? _skillTreeView.CurrentProgressionCharacterId
                : _fallbackCharacterId;

            service.ResetCharacter(characterId);
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
