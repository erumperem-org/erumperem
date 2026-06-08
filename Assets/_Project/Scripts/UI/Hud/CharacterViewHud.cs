using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Exibe os membros da party em HUD usando slots já existentes na cena.
///
/// Cada <see cref="CharacterSlot"/> é um conjunto de referências diretas
/// (Image, Slider, TMP_Text + seu GameObject raiz) arrastado pelo inspetor.
/// Não há instanciação em runtime.
///
/// Hierarquia de exibição: Main → Companion → Resting (ordem de Playables).
/// Slots excedentes são desativados.
/// </summary>
public sealed class CharacterViewHud : MonoBehaviour
{
    // ── Tipos ─────────────────────────────────────────────────────────────

    private enum CharacterType { Main, Companion, Resting }

    /// <summary>
    /// Conjunto de componentes que formam um slot de HUD na cena.
    /// Todos os campos são arrastados pelo inspetor.
    /// </summary>
    [Serializable]
    private sealed class CharacterSlot
    {
        [Tooltip("Raiz do slot — é ativada/desativada conforme necessário.")]
        public GameObject Root;

        [Tooltip("Image que exibe o ícone do personagem.")]
        public Image Icon;

        [Tooltip("Slider que representa a barra de vida (value entre 0 e 1).")]
        public Slider HealthBar;

        [Tooltip("Label com o nome do personagem.")]
        public TMP_Text NameLabel;

        // ── Estado interno ────────────────────────────────────────────────

        /// <summary>Personagem atualmente vinculado a este slot.</summary>
        [NonSerialized] public PlayableCharacter BoundCharacter;

        /// <summary>Delegate registrado em <see cref="PlayableHealthBar.OnHealthChanged"/>.</summary>
        [NonSerialized] public Action<float, float, float> HealthHandler;
    }

    // ── Inspetor ──────────────────────────────────────────────────────────

    [Header("Dependências")]
    [SerializeField] private PlayableCharactersManager _manager;

    [Header("Slots (ordem: Main, Companion, Resting…)")]
    [SerializeField] private List<CharacterSlot> _slots;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        // Garante que todos os slots começam ocultos.
        foreach (var slot in _slots)
            slot.Root?.SetActive(false);
    }

    private void OnEnable()
    {
        if (_manager == null) return;
        _manager.OnMainChanged      += _ => RefreshAll();
        _manager.OnCompanionChanged += _ => RefreshAll();
        RefreshAll();
    }

    private void OnDisable()
    {
        if (_manager == null) return;
        _manager.OnMainChanged      -= _ => RefreshAll();
        _manager.OnCompanionChanged -= _ => RefreshAll();

        foreach (var slot in _slots)
            UnbindHealth(slot);
    }

    // ── Refresh ───────────────────────────────────────────────────────────

    private void RefreshAll()
    {
        if (_manager == null) return;

        var ordered = BuildDisplayOrder();

        for (int i = 0; i < _slots.Count; i++)
        {
            if (i < ordered.Count)
                BindSlot(_slots[i], ordered[i]);
            else
                HideSlot(_slots[i]);
        }
    }

    /// <summary>
    /// Ordem: Main → Companion → Resting (sequência original de Playables).
    /// </summary>
    private List<PlayableCharacter> BuildDisplayOrder()
    {
        var result = new List<PlayableCharacter>();

        if (_manager.Main      is PlayableCharacter main)      result.Add(main);
        if (_manager.Companion is PlayableCharacter companion)  result.Add(companion);

        foreach (var pc in _manager.Playables)
            if (pc.CurrentState == PlayableCharacterState.Resting)
                result.Add(pc);

        return result;
    }

    // ── Bind / Unbind ─────────────────────────────────────────────────────

    private void BindSlot(CharacterSlot slot, PlayableCharacter character)
    {
        // Mesmo personagem → só atualiza HP para evitar flicker.
        if (slot.BoundCharacter == character)
        {
            RefreshHealth(slot);
            return;
        }

        UnbindHealth(slot);

        slot.BoundCharacter = character;
        slot.Root?.SetActive(true);

        if (slot.Icon      != null) slot.Icon.sprite    = character.Icon;
        if (slot.NameLabel != null) slot.NameLabel.text = character.CharacterName;

        RefreshHealth(slot);

        if (character.HealthBar != null)
        {
            slot.HealthHandler = (_, current, max) =>
            {
                if (slot.HealthBar == null) return;
                slot.HealthBar.maxValue = max;
                slot.HealthBar.value    = current;
            };
            character.HealthBar.OnHealthChanged += slot.HealthHandler;
        }
    }

    private void HideSlot(CharacterSlot slot)
    {
        UnbindHealth(slot);
        slot.BoundCharacter = null;
        slot.Root?.SetActive(false);
    }

    private void UnbindHealth(CharacterSlot slot)
    {
        if (slot.BoundCharacter?.HealthBar != null && slot.HealthHandler != null)
        {
            slot.BoundCharacter.HealthBar.OnHealthChanged -= slot.HealthHandler;
            slot.HealthHandler = null;
        }
    }

    private static void RefreshHealth(CharacterSlot slot)
    {
        if (slot.HealthBar == null || slot.BoundCharacter?.HealthBar == null) return;
        slot.HealthBar.maxValue = slot.BoundCharacter.HealthBar.MaxHealth;
        slot.HealthBar.value    = slot.BoundCharacter.HealthBar.CurrentHealth;
    }
}