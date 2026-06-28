using System;
using Core.Exploration.Items;
using Core.Exploration.Items.Currencies;
using Erumperem.Progression;
using Services.DebugUtilities;
using Services.IO;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopLevelUpButton : MonoBehaviour
{
    // ── Visualização ──────────────────────────────────────────────────────

    [Header("Visualização")]
    [SerializeField] private TMPro.TMP_Text _priceText;
    [SerializeField] private TMPro.TMP_Text _levelText;
    [SerializeField] private Image icon;
    [SerializeField] private Button _button;

    [Header("Persistência")]
    [SerializeField] private string _saveId = "shop_levelup_default";

    private IFileService _fileService;
    private string SaveDirectory => Application.persistentDataPath + "/ShopState";

    // ── Estado ────────────────────────────────────────────────────────────

    /// <summary>Nível atual (0 = nenhum nível comprado ainda).</summary>
    private int _currentLevel;
    public int pointsTogive;
    private const int MaxLevel = 12;
    private const int TierSize = 4;
    private static readonly int[] TierPrices = { 500, 1000, 1500, 2000 };

    private enum Tier { Rare, Epic, Legendary }
    public ScriptableObject rareCurrency, epicCurrency, legendaryCurrency;
    public PlayerInventorySystem inventorySystem;
    public PlayerProgressionService playerProgression;

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

    private void OnEnable() => RefreshUI();

    // ── Click ─────────────────────────────────────────────────────────────

    private async void OnClick()
    {
        if (_currentLevel >= MaxLevel)
            return;

        var tier = GetCurrentTier();
        var price = GetCurrentPrice();
        var currency = GetCurrentCurrency(tier);

        if (currency is not IStorageable item)
            return;

        if (inventorySystem.GetAmount(item) < price)
        {
            LoggerService.PrintLogMessage(
                LogLevel.Debug,
                "Fundos insuficientes",
                LogCategory.Gameplay);
            return;
        }

        inventorySystem.RemoveItems(new System.Collections.Generic.Dictionary<IStorageable, int>
    {
        { item, price }
    });

        _currentLevel++;

        OnLevelUp(_currentLevel, tier, price);

        try
        {
            await SaveStateAsync();
        }
        catch (Exception ex)
        {
            LoggerService.PrintLogMessage(
                LogLevel.Error,
                ex.Message,
                LogCategory.SaveSystem);
        }

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
        playerProgression.TrySetSharedSkillLevel(2);
    }

    // ── Progressão ────────────────────────────────────────────────────────

    private Tier GetCurrentTier() => (_currentLevel / TierSize) switch
    {
        0 => Tier.Rare,
        1 => Tier.Epic,
        _ => Tier.Legendary
    };

    private ScriptableObject GetCurrentCurrency(Tier tier)
    {
        switch (tier)
        {
            case Tier.Rare: return rareCurrency;
            case Tier.Epic: return epicCurrency;
            case Tier.Legendary: return legendaryCurrency;
        }
        return null;
    }
    private int GetCurrentPrice() => TierPrices[_currentLevel % TierSize];

    // ── UI ────────────────────────────────────────────────────────────────

    private void RefreshUI()
    {
        if (_currentLevel >= MaxLevel)
        {
            _button.interactable = false;
            if (_priceText) _priceText.text = "MAX";
            if (_levelText) _levelText.text = $"Level {MaxLevel}/{MaxLevel}";
            icon.sprite = null;
            return;
        }
        var reference = GetCurrentCurrency(GetCurrentTier());
        if(reference is AnomalousArtifact anomalousArtifact)
        {
            icon.sprite = anomalousArtifact.Sprite;
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
                fileName: _saveId + ".sav",
                filePath: SaveDirectory
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