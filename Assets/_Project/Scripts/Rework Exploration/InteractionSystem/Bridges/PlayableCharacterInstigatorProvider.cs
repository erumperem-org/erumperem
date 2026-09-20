using System;
using UnityEngine;
using Core.CharacterStats;

namespace InteractionSystem.Bridges
{
    /// <summary>
    /// Único ponto de contato entre o sistema de personagens jogáveis e o
    /// sistema de interação. Escuta PlayableCharacterController.OnCharacterEnteredInGame
    /// e republica como IInstigatorProvider.InstigatorChanged, para que
    /// sensores de interação nunca precisem conhecer PlayableCharacterController
    /// ou PlayableCharacters diretamente.
    ///
    /// Fica fora de Runtime/Editor de propósito: depende do assembly do
    /// projeto (Core.CharacterStats), não é parte portável do sistema.
    /// Colocar num GameObject de sistema/bootstrap da cena, com a
    /// referência ao PlayableCharacterController atribuída no Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayableCharacterInstigatorProvider : MonoBehaviour, IInstigatorProvider
    {
        [SerializeField] private PlayableCharacterController characterController;

        public GameObject Current { get; private set; }

        public event Action<GameObject> InstigatorChanged;

        private void OnEnable()
        {
            if (characterController == null)
            {
                Debug.LogError(
                    $"{nameof(PlayableCharacterInstigatorProvider)}: characterController não atribuído.",
                    this);
                return;
            }

            characterController.OnCharacterEnteredInGame += HandleCharacterEnteredInGame;

            // Cobre o caso do controller já ter concluído o load (Start async)
            // antes deste componente habilitar - sincroniza sem esperar a
            // próxima troca de personagem.
            if (characterController.InGameCharacter != null)
            {
                SetCurrent(characterController.InGameCharacter.gameObject);
            }
        }

        private void OnDisable()
        {
            if (characterController == null) return;
            characterController.OnCharacterEnteredInGame -= HandleCharacterEnteredInGame;
        }

        private void HandleCharacterEnteredInGame(PlayableCharacters character)
        {
            SetCurrent(character != null ? character.gameObject : null);
        }

        private void SetCurrent(GameObject instigator)
        {
            if (Current == instigator) return;
            Current = instigator;
            InstigatorChanged?.Invoke(Current);
        }
    }
}
