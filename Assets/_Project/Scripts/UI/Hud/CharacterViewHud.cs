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

    public enum CharacterType { Main, Companion, Resting }

    /// <summary>
    /// Conjunto de componentes que formam um slot de HUD na cena.
    /// Todos os campos são arrastados pelo inspetor.
    /// </summary>
    [Serializable]
    public sealed class CharacterSlot
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
    [SerializeField] public PlayableCharactersManager _manager;

    [Header("Slots (ordem: Main, Companion, Resting…)")]
    [SerializeField] public List<CharacterSlot> _slots;

    public Action<IPlayableCharacter> _onMainChangedHandler;
    public Action<IPlayableCharacter> _onCompanionChangedHandler;
    public Action _onExplorationStateAppliedHandler;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    public void Awake()
    {
        ResolveDependencies();

        if (_slots == null) return;

        foreach (var slot in _slots)
        {
            if (slot == null) continue;
            slot.Root?.SetActive(false);
        }
    }

    public void OnEnable()
    {
        ResolveDependencies();
        if (_manager == null || _slots == null || _slots.Count == 0) return;

        _onMainChangedHandler      ??= _ => RefreshAll();
        _onCompanionChangedHandler ??= _ => RefreshAll();
        _onExplorationStateAppliedHandler ??= RefreshAll;

        _manager.OnMainChanged      += _onMainChangedHandler;
        _manager.OnCompanionChanged += _onCompanionChangedHandler;
        ExplorationLoadContext.OnExplorationStateApplied += _onExplorationStateAppliedHandler;
        RefreshAll();
    }

    public void Start() => RefreshAll();

    public void OnDisable()
    {
        if (_manager != null)
        {
            if (_onMainChangedHandler != null)
                _manager.OnMainChanged -= _onMainChangedHandler;
            if (_onCompanionChangedHandler != null)
                _manager.OnCompanionChanged -= _onCompanionChangedHandler;
        }

        if (_onExplorationStateAppliedHandler != null)
        {
            ExplorationLoadContext.OnExplorationStateApplied -= _onExplorationStateAppliedHandler;
        }

        if (_slots == null) return;

        foreach (var slot in _slots)
        {
            if (slot == null) continue;
            UnbindHealth(slot);
        }
    }

    public void ResolveDependencies()
    {
        if (_manager == null)
            _manager = FindFirstObjectByType<PlayableCharactersManager>();

        if (_slots != null && _slots.Count > 0) return;

        _slots = BuildSlotsFromChildren();
    }

    public List<CharacterSlot> BuildSlotsFromChildren()
    {
        var discoveredSlots = new List<CharacterSlot>();
        var charactersContainer = transform.Find("Characters");
        if (charactersContainer == null) return discoveredSlots;

        foreach (Transform childTransform in charactersContainer)
        {
            if (!childTransform.name.StartsWith("ChracterInfo")) continue;

            var portraitTransform = childTransform.Find("Portrait");
            var healthTransform   = childTransform.Find("Health");
            var nameTransform     = childTransform.Find("CharacterName");

            discoveredSlots.Add(new CharacterSlot
            {
                Root      = childTransform.gameObject,
                Icon      = portraitTransform != null ? portraitTransform.GetComponent<Image>() : null,
                HealthBar = healthTransform != null ? healthTransform.GetComponent<Slider>() : null,
                NameLabel = nameTransform != null ? nameTransform.GetComponent<TMP_Text>() : null,
            });
        }

        return discoveredSlots;
    }

    // ── Refresh ───────────────────────────────────────────────────────────

    public void RefreshAll()
    {
        if (_manager == null || _slots == null || _slots.Count == 0) return;

        var ordered = BuildDisplayOrder();

        for (int slotIndex = 0; slotIndex < _slots.Count; slotIndex++)
        {
            var slot = _slots[slotIndex];
            if (slot == null) continue;

            if (slotIndex < ordered.Count)
                BindSlot(slot, ordered[slotIndex]);
            else
                HideSlot(slot);
        }
    }

    /// <summary>
    /// Ordem: Main → Companion → Resting (sequência original de Playables).
    /// </summary>
    public List<PlayableCharacter> BuildDisplayOrder()
    {
        var result = new List<PlayableCharacter>();

        if (_manager.Main      is PlayableCharacter main)      result.Add(main);
        if (_manager.Companion is PlayableCharacter companion)  result.Add(companion);

        foreach (var playableCharacter in _manager.Playables)
        {
            if (playableCharacter == null) continue;
            if (playableCharacter.CurrentState == PlayableCharacterState.Resting)
                result.Add(playableCharacter);
        }

        return result;
    }

    // ── Bind / Unbind ─────────────────────────────────────────────────────

    public void BindSlot(CharacterSlot slot, PlayableCharacter character)
    {
        if (slot == null || character == null) return;

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
        if (slot.NameLabel != null) slot.NameLabel.text = character._characterExplorationDisplayName;

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

    public void HideSlot(CharacterSlot slot)
    {
        UnbindHealth(slot);
        slot.BoundCharacter = null;
        slot.Root?.SetActive(false);
    }

    public void UnbindHealth(CharacterSlot slot)
    {
        if (slot.BoundCharacter?.HealthBar != null && slot.HealthHandler != null)
        {
            slot.BoundCharacter.HealthBar.OnHealthChanged -= slot.HealthHandler;
            slot.HealthHandler = null;
        }
    }

    public static void RefreshHealth(CharacterSlot slot)
    {
        if (slot.HealthBar == null || slot.BoundCharacter?.HealthBar == null) return;
        slot.HealthBar.maxValue = slot.BoundCharacter.HealthBar.MaxHealth;
        slot.HealthBar.value    = slot.BoundCharacter.HealthBar.CurrentHealth;
    }
}