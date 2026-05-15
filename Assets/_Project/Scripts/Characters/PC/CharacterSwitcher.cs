// =============================================================================
// CharacterSwitcher.cs
// Gerencia a troca entre os 3 personagens jogáveis em runtime.
//
// RESPONSABILIDADE:
//   - Receber o índice do personagem desejado (via InputReader.OnCharacterSwitchRequested)
//   - Destruir o mesh visual atual e instanciar o novo
//   - Trocar o AnimatorController via AnimationBridge.SwapAnimator()
//   - Atualizar ctx.ActiveCharacterData com os stats do novo personagem
//   - Preservar o estado das camadas (a troca é transparente para a State Machine)
//
// O QUE NÃO MUDA AO TROCAR:
//   - O GameObject raiz do player (posição, rotação, CharacterController)
//   - As camadas da State Machine (o estado atual é preservado)
//   - O PlayerContext e seus flags
//
// POR QUÊ O ANIMATOR É NO FILHO:
//   O Animator e o mesh visual ficam em um GameObject filho do player root.
//   Isso permite destruir e recriar o filho sem afetar o CharacterController
//   e a State Machine, que vivem no root.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using CharacterSystem.Core;
using CharacterSystem.Character;
using UnityEngine.Playables;

namespace CharacterSystem.Character
{
    /// <summary>
    /// Gerencia a troca visual e de dados entre os personagens disponíveis.
    /// Deve ser inicializado pelo PlayerController após as camadas.
    /// </summary>
    public class CharacterSwitcher
    {
        // ── Dados ────────────────────────────────────────────────────────────

        private readonly IReadOnlyList<PlayableCharacterData> _characters;
        private int _currentIndex = 0;

        // ── Referências ──────────────────────────────────────────────────────

        /// <summary>Transform pai onde o mesh filho será instanciado.</summary>
        private readonly Transform _meshRoot;

        private GameObject _currentMeshInstance;

        // ── Construtor ───────────────────────────────────────────────────────

        /// <summary>
        /// Cria o switcher.
        /// </summary>
        /// <param name="characters">Lista de CharacterData na ordem 1, 2, 3.</param>
        /// <param name="meshRoot">Transform que será pai do prefab de mesh.</param>
        public CharacterSwitcher(IReadOnlyList<PlayableCharacterData> characters, Transform meshRoot)
        {
            _characters = characters;
            _meshRoot = meshRoot;
        }

        // ── API Pública ──────────────────────────────────────────────────────

        /// <summary>
        /// Inicializa com o primeiro personagem (índice 0).
        /// Chamado pelo PlayerController.Start().
        /// </summary>
        public void Initialize(PlayerContext ctx, GameObject toSwap)
        {
            SwitchTo(0, ctx);
        }

        /// <summary>
        /// Troca para o personagem no índice especificado.
        /// Seguro chamar com o índice atual — é ignorado silenciosamente.
        /// </summary>
        /// <param name="index">Índice do personagem (0-based).</param>
        /// <param name="ctx">Contexto compartilhado para atualizar stats e bridge.</param>
        public void SwitchTo(int index, PlayerContext ctx)
        {
            // Valida o índice
            if (index < 0 || index >= _characters.Count)
            {
                Debug.LogWarning($"[CharacterSwitcher] Índice inválido: {index}. " +
                                 $"Total de personagens: {_characters.Count}");
                return;
            }

            // Ignora se já é o personagem atual
            if (index == _currentIndex && _currentMeshInstance != null) return;

            _currentIndex = index;
            var data = _characters[index];

            // 1. Troca os dados de stats no contexto
            ctx.ActiveCharacterData = data;

            // 2. Troca o AnimatorController (preserva estado atual das animações)
            ctx.AnimationBridge.SwapAnimator(data.AnimatorController);

            // 3. Troca o mesh visual
            SwapMesh(data);

            Debug.Log($"[CharacterSwitcher] Personagem ativo: {data.CharacterName}");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Atualiza o objeto "swapable" com base no CharacterData,
        /// sem instanciar ou destruir.
        /// </summary>
        /// <summary>
/// Destrói o mesh atual e instancia um novo baseado no CharacterData,
/// usando o "swapable" como base/pai.
/// </summary>
private void SwapMesh(PlayableCharacterData data)
{
    // if (data == null || swapable == null)
    // {
    //     Debug.LogWarning("[CharacterSwitcher] Data ou Swapable é null.");
    //     return;
    // }

    // // 🔹 Destrói instância anterior
    // if (_currentMeshInstance != null)
    //     Object.Destroy(_currentMeshInstance);

    // if (data.CharacterMeshPrefab == null)
    // {
    //     Debug.LogWarning($"[CharacterSwitcher] CharacterData '{data.CharacterName}' não possui prefab.");
    //     return;
    // }

    // // 🔹 Usa o transform do swapable como base
    // Transform parent = swapable.transform;

    // // 🔥 Instancia como filho do swapable
    // _currentMeshInstance = GameObject.Instantiate(
    //     data.CharacterMeshPrefab,
    //     parent.position,
    //     parent.rotation,
    //     parent
    // );

    // // 🔹 Zera transform local
    // var t = _currentMeshInstance.transform;
    // t.localPosition = Vector3.zero;
    // t.localRotation = Quaternion.identity;
    // t.localScale = Vector3.one;

    // // 🔹 (Opcional mas MUITO importante) alinhar Animator
    // var newAnimator = _currentMeshInstance.GetComponent<Animator>();
    // var baseAnimator = swapable.GetComponentInChildren<Animator>();

    // if (newAnimator != null && baseAnimator != null)
    // {
    //     newAnimator.runtimeAnimatorController = baseAnimator.runtimeAnimatorController;
    //     newAnimator.avatar = baseAnimator.avatar;
    // }
}
    }
}
