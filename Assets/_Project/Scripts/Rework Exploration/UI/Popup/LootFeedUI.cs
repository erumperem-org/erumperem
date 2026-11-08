using System;
using System.Collections.Generic;
using Core.Exploration.Items;
using Core.Inventory;
using UnityEngine;

namespace Core.UI.LootFeed
{
    /// <summary>
    /// Feed estilo Witcher 3: cada evento de inventário vira UM pop-up
    /// (instância do prefab) que se empilha verticalmente e some sozinho,
    /// no seu próprio tempo. Não há fila nem limite que derrube linhas.
    ///
    /// Em rajadas (ex.: baú com vários itens) as linhas são criadas todas
    /// de uma vez, mas aparecem em sequência via staggerInterval.
    /// </summary>
    public sealed class LootFeedUI : MonoBehaviour
    {
        [Serializable]
        private struct RarityColor
        {
            public ItemRarity rarity;
            public Color color;
        }

        [Header("Fontes")]
        [SerializeField] private List<InventorySystem> inventories = new();
        [Tooltip("Se a lista estiver vazia, procura todos os InventorySystem da cena ao habilitar.")]
        [SerializeField] private bool autoFindInventories = true;

        [Header("UI")]
        [Tooltip("Container com VerticalLayoutGroup. Se vazio, usa o próprio RectTransform.")]
        [SerializeField] private RectTransform container;
        [SerializeField] private LootFeedEntryView entryPrefab;
        [Tooltip("Novas linhas entram no topo; senão, embaixo (padrão Witcher 3: mais antigas em cima).")]
        [SerializeField] private bool newestOnTop = false;
        [Min(0)]
        [SerializeField] private int prewarmCount = 8;
        [Header("Posição")]
        [Tooltip("Deslocamento aplicado ao container ao iniciar. Y positivo = sobe.")]
        [SerializeField] private Vector2 anchoredOffset = Vector2.zero;

        [Header("Tempo")]
        [Tooltip("Tempo que cada pop-up fica visível (após terminar de aparecer).")]
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.4f;
        [Tooltip("Tempo que a altura leva para colapsar após sumir (as outras linhas deslizam).")]
        [SerializeField] private float collapseDuration = 0.25f;
        [Tooltip("Atraso entre pop-ups criados na mesma rajada. 0 = todos aparecem juntos.")]
        [SerializeField] private float staggerInterval = 0.15f;

        [Header("Filtros")]
        [SerializeField] private bool showInserted = true;
        [SerializeField] private bool showRemoved = true;
        [SerializeField] private bool showRejected = true;
        [Tooltip("Desligado = um pop-up por evento. Ligado = soma na linha existente do mesmo item/tipo.")]
        [SerializeField] private bool mergeSameEntries = false;

        [Header("Cores por tipo de evento")]
        [SerializeField] private Color insertedColor = new Color(0.45f, 0.9f, 0.5f);
        [SerializeField] private Color removedColor = new Color(0.95f, 0.4f, 0.4f);
        [SerializeField] private Color rejectedColor = new Color(1f, 0.7f, 0.2f);

        [Header("Cor do nome por raridade (opcional)")]
        [SerializeField] private Color defaultNameColor = Color.white;
        [SerializeField] private List<RarityColor> rarityColors = new();

        [Header("Textos")]
        [SerializeField] private string insertedFormat = "+{0}";
        [SerializeField] private string removedFormat = "-{0}";
        [SerializeField] private string rejectedFormat = "x{0}";
        [SerializeField] private string insertedCaption = "";
        [SerializeField] private string removedCaption = "";
        [SerializeField] private string rejectedCaption = "Inventário cheio";

        private readonly List<LootFeedEntryView> _active = new();
        private readonly Stack<LootFeedEntryView> _pool = new();
        private readonly HashSet<InventorySystem> _subscribed = new();

        private float _nextAppearTime;

        private RectTransform Root => container != null ? container : (RectTransform)transform;

        private void Awake()
        {
            Root.anchoredPosition += anchoredOffset;
            Prewarm();
        }

        private void OnEnable()
        {
            if (autoFindInventories && inventories.Count == 0)
            {
                foreach (var inv in FindObjectsByType<InventorySystem>(FindObjectsSortMode.None))
                    inventories.Add(inv);
            }

            foreach (var inv in inventories)
                Subscribe(inv);
        }

        private void OnDisable()
        {
            foreach (var inv in _subscribed)
            {
                if (inv == null) continue;
                inv.OnInventoryChanged -= HandleChanged;
                inv.OnItemRejected -= HandleRejected;
            }
            _subscribed.Clear();

            for (int i = _active.Count - 1; i >= 0; i--)
                Recycle(_active[i]);
        }

        // ── API pública ──────────────────────────────────────────────

        public void Register(InventorySystem inventory)
        {
            if (inventory == null) return;
            if (!inventories.Contains(inventory)) inventories.Add(inventory);
            if (isActiveAndEnabled) Subscribe(inventory);
        }

        public void Unregister(InventorySystem inventory)
        {
            if (inventory == null) return;
            inventories.Remove(inventory);

            if (_subscribed.Remove(inventory))
            {
                inventory.OnInventoryChanged -= HandleChanged;
                inventory.OnItemRejected -= HandleRejected;
            }
        }

        private void Subscribe(InventorySystem inv)
        {
            if (inv == null || !_subscribed.Add(inv)) return;
            inv.OnInventoryChanged += HandleChanged;
            inv.OnItemRejected += HandleRejected;
        }

        // ── Handlers ─────────────────────────────────────────────────

        // Ajuste aqui se os nomes das propriedades de InventorySlotChangedEventArgs forem diferentes.
        private void HandleChanged(InventorySlotChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case InventoryChangeType.Inserted when showInserted:
                    Push(e.Item, LootFeedEventKind.Inserted, e.Quantity);
                    break;
                case InventoryChangeType.Removed when showRemoved:
                    Push(e.Item, LootFeedEventKind.Removed, e.Quantity);
                    break;
            }
        }

        private void HandleRejected(InventoryOverflowEventArgs e)
        {
            if (!showRejected || e.Rejected <= 0) return;
            Push(e.Item, LootFeedEventKind.Rejected, e.Rejected);
        }

        // ── Feed ─────────────────────────────────────────────────────

        private void Push(IIITem item, LootFeedEventKind kind, int quantity)
        {
            if (item == null || quantity <= 0 || entryPrefab == null) return;

            if (mergeSameEntries && TryMerge(item, kind, quantity))
                return;

            var entry = Acquire();
            entry.transform.SetParent(Root, false);
            entry.transform.SetSiblingIndex(newestOnTop ? 0 : Root.childCount - 1);

            entry.Setup(
                item,
                kind,
                quantity,
                FormatFor(kind),
                CaptionFor(kind),
                ColorFor(kind),
                NameColorFor(item.Rarity),
                NextStaggerDelay(),
                lifetime,
                fadeInDuration,
                fadeOutDuration,
                collapseDuration,
                OnEntryFinished);

            _active.Add(entry);
        }

        private float NextStaggerDelay()
        {
            float now = Time.unscaledTime;
            float appearAt = Mathf.Max(now, _nextAppearTime);
            _nextAppearTime = appearAt + staggerInterval;
            return appearAt - now;
        }

        private bool TryMerge(IIITem item, LootFeedEventKind kind, int quantity)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var e = _active[i];
                if (e.IsLeaving || e.Kind != kind || !Equals(e.ItemId, item.StorageableId)) continue;

                e.Merge(quantity);
                return true;
            }
            return false;
        }

        private void OnEntryFinished(LootFeedEventKind kind, LootFeedEntryView entry) => Recycle(entry);

        // ── Pool ─────────────────────────────────────────────────────

        private void Prewarm()
        {
            if (entryPrefab == null) return;

            for (int i = 0; i < prewarmCount; i++)
            {
                var entry = Instantiate(entryPrefab, Root);
                entry.gameObject.SetActive(false);
                _pool.Push(entry);
            }
        }

        private LootFeedEntryView Acquire()
        {
            LootFeedEntryView entry = null;

            while (_pool.Count > 0 && entry == null)
                entry = _pool.Pop();

            if (entry == null)
                entry = Instantiate(entryPrefab, Root);

            entry.gameObject.SetActive(true);
            return entry;
        }

        private void Recycle(LootFeedEntryView entry)
        {
            if (entry == null) return;

            _active.Remove(entry);
            entry.gameObject.SetActive(false);
            _pool.Push(entry);
        }

        // ── Apresentação ─────────────────────────────────────────────

        private Color ColorFor(LootFeedEventKind kind) => kind switch
        {
            LootFeedEventKind.Inserted => insertedColor,
            LootFeedEventKind.Removed => removedColor,
            _ => rejectedColor
        };

        private string FormatFor(LootFeedEventKind kind) => kind switch
        {
            LootFeedEventKind.Inserted => insertedFormat,
            LootFeedEventKind.Removed => removedFormat,
            _ => rejectedFormat
        };

        private string CaptionFor(LootFeedEventKind kind) => kind switch
        {
            LootFeedEventKind.Inserted => insertedCaption,
            LootFeedEventKind.Removed => removedCaption,
            _ => rejectedCaption
        };

        private Color NameColorFor(ItemRarity rarity)
        {
            foreach (var rc in rarityColors)
                if (rc.rarity.Equals(rarity)) return rc.color;
            return defaultNameColor;
        }
    }
}