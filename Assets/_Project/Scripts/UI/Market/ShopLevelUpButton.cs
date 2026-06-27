using System;
using Erumperem.Progression;
using Services.IO;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopLevelUpButton : MonoBehaviour
{
    // ── Visualização ──────────────────────────────────────────────────────

    [Header("Visualização")]
    [SerializeField] private TMPro.TMP_Text _priceText;
    [SerializeField] private TMPro.TMP_Text _levelText;
    [SerializeField] private Button         _button;

    [Header("Persistência")]
    [SerializeField] private string _saveId = "shop_levelup_default";

    private IFileService _fileService;
    private string SaveDirectory => Application.persistentDataPath + "/ShopState";

    // ── Estado ────────────────────────────────────────────────────────────

    /// <summary>Nível atual (0 = nenhum nível comprado ainda).</summary>
    private int _currentLevel;

    private const int MaxLevel  = 12;
    private const int TierSize  = 4;
    private static readonly int[] TierPrices = { 5, 10, 15, 20 };

    private enum Tier { Rare, Epic, Legendary }

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        _fileService = new FileService();
        _button.onClick.AddListener(OnClick);
    }

    private async void Start()
    {
        await LoadStateAsync();
        RefreshUI();
    }

    private void OnDestroy()
    {
        _button.onClick.RemoveListener(OnClick);
    }

    private void OnEnable()  => RefreshUI();

    // ── Click ─────────────────────────────────────────────────────────────

    private async void OnClick()
    {
        if (_currentLevel >= MaxLevel) return;

        OnLevelUp(_currentLevel, GetCurrentTier(), GetCurrentPrice());

        _currentLevel++;

        await SaveStateAsync();
        RefreshUI();
    }

    // ── Ponto de extensão ─────────────────────────────────────────────────

    /// <summary>
    /// Chamado ao confirmar um nível. Implemente aqui a lógica de negócio
    /// (debitar moeda, conceder recompensa, etc.).
    /// </summary>
    /// <param name="level">Nível que está sendo comprado (0-based).</param>
    /// <param name="tier">Tier atual (Rare / Epic / Legendary).</param>
    /// <param name="price">Preço cobrado neste nível.</param>
    private void OnLevelUp(int level, Tier tier, int price)
    {
    }

    // ── Progressão ────────────────────────────────────────────────────────

    private Tier GetCurrentTier() => (_currentLevel / TierSize) switch
    {
        0 => Tier.Rare,
        1 => Tier.Epic,
        _ => Tier.Legendary
    };

    private int GetCurrentPrice() => TierPrices[_currentLevel % TierSize];

    // ── UI ────────────────────────────────────────────────────────────────

    private void RefreshUI()
    {
        if (_currentLevel >= MaxLevel)
        {
            _button.interactable = false;
            if (_priceText) _priceText.text = "MAX";
            if (_levelText) _levelText.text = $"Level {MaxLevel}/{MaxLevel}";
            return;
        }

        if (_priceText) _priceText.text = GetCurrentPrice().ToString();
        if (_levelText) _levelText.text = $"Level {_currentLevel + 1}/{MaxLevel}";
        _button.interactable = true;
    }

    // ── Persistência ──────────────────────────────────────────────────────

    private async System.Threading.Tasks.Task SaveStateAsync()
    {
        try
        {
            await _fileService.WriteAsync(new FileData(
                fileContent: _currentLevel.ToString(),
                fileName:    _saveId + ".sav",
                filePath:    SaveDirectory
            ));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ShopLevelUpButton] Falha ao salvar '{_saveId}': {e.Message}");
        }
    }

    private async System.Threading.Tasks.Task LoadStateAsync()
    {
        try
        {
            if (!await _fileService.ExistsAsync(_saveId + ".sav", SaveDirectory)) return;

            FileData data = await _fileService.ReadAsync(_saveId + ".sav", SaveDirectory);

            if (int.TryParse(data._fileContent, out int saved))
                _currentLevel = Mathf.Clamp(saved, 0, MaxLevel);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ShopLevelUpButton] Falha ao carregar '{_saveId}': {e.Message}");
        }
    }
}