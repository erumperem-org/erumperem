using UnityEngine;

namespace Services.DebugUtilities
{
    /// <summary>
    /// Associates a debug category with a display color.
    /// Green and Red are reserved for success/failure status indicators.
    /// </summary>
    public sealed class LogCategory
    {
        public readonly string Name;
        public readonly Color  Color;

        /// <summary>Precomputed hex string for <see cref="Color"/> (e.g. "FF00AA").</summary>
        public readonly string ColorHex;

        /// <summary>Precomputed "&lt;color=#...&gt;[NAME]&lt;/color&gt; " tag, ready to append.</summary>
        public readonly string TagFormatted;

        private LogCategory(string name, Color color)
        {
            Name         = name;
            Color        = color;
            ColorHex     = ColorUtility.ToHtmlStringRGB(color);
            TagFormatted = $"<color=#{ColorHex}>[{name.ToUpperInvariant()}]</color> ";
        }

        public override string ToString() => Name;

        // ── Sistema base ───────────────────────────────────────────────
        public static readonly LogCategory Core           = new("Core",           new Color(0.6f,  0.7f,  1f   )); // Azul claro
        public static readonly LogCategory Initialization = new("Initialization", new Color(0.6f,  0.6f,  1f   )); // Azul suave
        public static readonly LogCategory Lifecycle      = new("Lifecycle",      new Color(0.9f,  0.9f,  0.4f )); // Amarelo suave

        // ── Gameplay ───────────────────────────────────────────────────
        public static readonly LogCategory Gameplay    = new("Gameplay",    new Color(0.95f, 0.95f, 0.95f)); // Branco suave
        public static readonly LogCategory Combat      = new("Combat",      new Color(1f,    0.6f,  0.2f )); // Laranja
        public static readonly LogCategory Ability     = new("Ability",     new Color(0.6f,  0.3f,  1f   )); // Roxo
        public static readonly LogCategory AI          = new("AI",          new Color(1f,    0.7f,  0.3f )); // Laranja claro
        public static readonly LogCategory Player      = new("Player",      new Color(0.3f,  0.7f,  1f   )); // Azul
        public static readonly LogCategory NPC         = new("NPC",         new Color(0.4f,  0.9f,  0.5f )); // Verde menta
        public static readonly LogCategory Input       = new("Input",       new Color(0.75f, 0.75f, 0.75f)); // Cinza claro
        public static readonly LogCategory Interaction = new("Interaction", new Color(1f,    0.85f, 0.3f )); // Amarelo dourado
        public static readonly LogCategory Inventory   = new("Inventory",   new Color(0.8f,  0.55f, 0.2f )); // Marrom dourado
        public static readonly LogCategory Quest       = new("Quest",       new Color(1f,    0.9f,  0.2f )); // Amarelo brilhante
        public static readonly LogCategory Detection   = new("Detection",   new Color(0.2f,  1f,    0.6f )); // Verde ciano
        public static readonly LogCategory Animation   = new("Animation",   new Color(0.9f,  0.5f,  0.8f )); // Rosa suave
        public static readonly LogCategory Audio       = new("Audio",       new Color(0.5f,  0.85f, 0.7f )); // Verde água
        public static readonly LogCategory Equipment    = new("Equipment",    new Color(0.75f, 0.6f,  0.4f )); // Bronze
        public static readonly LogCategory Crafting     = new("Crafting",     new Color(0.9f,  0.65f, 0.3f )); // Âmbar
        public static readonly LogCategory Skill        = new("Skill",        new Color(0.55f, 0.35f, 0.9f )); // Violeta
        public static readonly LogCategory StatusEffect = new("StatusEffect", new Color(0.4f,  1f,    0.4f )); // Verde efeito
        public static readonly LogCategory Loot         = new("Loot",         new Color(1f,    0.84f, 0f   )); // Dourado
        public static readonly LogCategory Camera       = new("Camera",       new Color(0.6f,  0.9f,  1f   )); // Azul piscina
        public static readonly LogCategory Cutscene     = new("Cutscene",     new Color(0.8f,  0.7f,  1f   )); // Lilás
        public static readonly LogCategory Dialogue     = new("Dialogue",     new Color(1f,    0.75f, 0.85f)); // Rosa pastel
        public static readonly LogCategory Faction      = new("Faction",      new Color(0.7f,  0.4f,  0.4f )); // Vermelho terroso
        public static readonly LogCategory Economy      = new("Economy",      new Color(0.4f,  0.8f,  0.4f )); // Verde nota

        // ── Mundo ──────────────────────────────────────────────────────
        public static readonly LogCategory World       = new("World",       new Color(0.4f,  0.8f,  1f   )); // Azul céu
        public static readonly LogCategory Environment = new("Environment", new Color(0.3f,  0.5f,  0.8f )); // Azul escuro
        public static readonly LogCategory Physics     = new("Physics",     new Color(0.5f,  0.6f,  1f   )); // Azul médio
        public static readonly LogCategory Navigation  = new("Navigation",  new Color(1f,    0.8f,  0.3f )); // Dourado
        public static readonly LogCategory Weather     = new("Weather",     new Color(0.6f,  0.75f, 0.9f )); // Azul acinzentado
        public static readonly LogCategory TimeOfDay   = new("TimeOfDay",   new Color(1f,    0.85f, 0.5f )); // Âmbar claro
        public static readonly LogCategory VFX         = new("VFX",         new Color(0.9f,  0.4f,  0.9f )); // Magenta
        public static readonly LogCategory Pooling     = new("Pooling",     new Color(0.55f, 0.75f, 0.55f)); // Verde oliva claro

        // ── UI ─────────────────────────────────────────────────────────
        public static readonly LogCategory UI            = new("UI",            new Color(1f,    0.5f,  1f   )); // Rosa
        public static readonly LogCategory UX            = new("UX",            new Color(0.85f, 0.4f,  1f   )); // Roxo claro
        public static readonly LogCategory Tutorial      = new("Tutorial",      new Color(0.6f,  1f,    0.9f )); // Ciano suave
        public static readonly LogCategory Notification  = new("Notification",  new Color(1f,    0.95f, 0.6f )); // Amarelo pálido
        public static readonly LogCategory Localization  = new("Localization",  new Color(0.5f,  0.7f,  0.9f )); // Azul acinzentado
        public static readonly LogCategory Accessibility = new("Accessibility", new Color(0.7f,  1f,    0.7f )); // Verde claro
        public static readonly LogCategory Achievement   = new("Achievement",   new Color(1f,    0.8f,  0f   )); // Ouro

        // ── Dados ──────────────────────────────────────────────────────
        public static readonly LogCategory Data         = new("Data",         new Color(0.5f,  0.9f,  1f   )); // Ciano claro
        public static readonly LogCategory SaveSystem   = new("SaveSystem",   new Color(0.3f,  0.8f,  1f   )); // Azul ciano
        public static readonly LogCategory Loading      = new("Loading",      new Color(1f,    1f,    0.5f )); // Amarelo
        public static readonly LogCategory Settings     = new("Settings",     new Color(0.65f, 0.8f,  0.95f)); // Azul pastel
        public static readonly LogCategory Progression  = new("Progression",  new Color(1f,    0.6f,  0.6f )); // Salmão
        public static readonly LogCategory Serialization= new("Serialization",new Color(0.6f,  0.6f,  0.85f)); // Roxo acinzentado
        public static readonly LogCategory Versioning   = new("Versioning",   new Color(0.8f,  0.8f,  0.6f )); // Bege
        public static readonly LogCategory Corruption   = new("Corruption",   new Color(0.55f, 0.1f,  0.6f )); // Roxo escuro

        // ── Arquitetura ────────────────────────────────────────────────
        public static readonly LogCategory Command      = new("CommandBus",   new Color(1f,    0.75f, 0.3f )); // Laranja claro
        public static readonly LogCategory EventBus     = new("EventBus",     new Color(0.4f,  0.9f,  1f   )); // Ciano
        public static readonly LogCategory StateMachine = new("StateMachine", new Color(0.7f,  0.3f,  1f   )); // Roxo forte

        // ── Multiplayer ────────────────────────────────────────────────
        public static readonly LogCategory Network     = new("Network",     new Color(0.3f,  0.6f,  1f   )); // Azul rede
        public static readonly LogCategory Prediction  = new("Prediction",  new Color(0.7f,  0.7f,  1f   )); // Azul claro
        public static readonly LogCategory Replication = new("Replication", new Color(1f,    0.7f,  0.4f )); // Laranja médio

        // ── Performance ────────────────────────────────────────────────
        public static readonly LogCategory Performance = new("Performance", new Color(1f,    1f,    0.3f )); // Amarelo forte
        public static readonly LogCategory Memory      = new("Memory",      new Color(0.6f,  0.5f,  0.4f )); // Marrom claro
        public static readonly LogCategory Rendering   = new("Rendering",   new Color(1f,    0.5f,  0.5f )); // Vermelho suave
        public static readonly LogCategory Streaming   = new("Streaming",   new Color(0.5f,  0.8f,  0.8f )); // Ciano acinzentado

        // ── Plataforma & Serviços ────────────────────────────────────────
        public static readonly LogCategory Platform    = new("Platform",    new Color(0.6f,  0.6f,  0.6f )); // Cinza médio
        public static readonly LogCategory CloudSave   = new("CloudSave",   new Color(0.4f,  0.7f,  1f   )); // Azul nuvem
        public static readonly LogCategory Analytics   = new("Analytics",   new Color(0.85f, 0.85f, 0.3f )); // Amarelo oliva
        public static readonly LogCategory PlatformAchievements = new("PlatformAchievements", new Color(1f, 0.75f, 0.2f)); // Laranja dourado (conquistas de plataforma, ex: Steam/PSN)
        public static readonly LogCategory Security    = new("Security",    new Color(0.8f,  0.2f,  0.2f )); // Vermelho forte
        public static readonly LogCategory Modding     = new("Modding",     new Color(0.4f,  0.9f,  0.9f )); // Ciano vivo

        // ── Ferramentas & Qualidade ───────────────────────────────────────
        public static readonly LogCategory EditorTool  = new("EditorTool",  new Color(0.75f, 0.75f, 1f   )); // Lavanda
        public static readonly LogCategory Testing     = new("Testing",     new Color(0.6f,  1f,    0.6f )); // Verde teste
        public static readonly LogCategory Validation  = new("Validation",  new Color(1f,    0.65f, 0.65f)); // Rosa avermelhado
        public static readonly LogCategory Build       = new("Build",       new Color(0.7f,  0.7f,  0.9f )); // Azul suave

        // ── Debug ──────────────────────────────────────────────────────
        public static readonly LogCategory Cheats = new("Cheats", new Color(1f, 0.3f, 0.3f)); // Vermelho vivo (destaca uso de cheat)
        public static readonly LogCategory Debug  = new("Debug",  new Color(0.7f, 0.7f, 0.7f)); // Cinza
    }
}