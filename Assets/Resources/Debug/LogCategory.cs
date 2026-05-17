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

        private LogCategory(string name, Color color)
        {
            Name  = name;
            Color = color;
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

        // ── Mundo ──────────────────────────────────────────────────────
        public static readonly LogCategory World       = new("World",       new Color(0.4f,  0.8f,  1f   )); // Azul céu
        public static readonly LogCategory Environment = new("Environment", new Color(0.3f,  0.5f,  0.8f )); // Azul escuro
        public static readonly LogCategory Physics     = new("Physics",     new Color(0.5f,  0.6f,  1f   )); // Azul médio
        public static readonly LogCategory Navigation  = new("Navigation",  new Color(1f,    0.8f,  0.3f )); // Dourado

        // ── UI ─────────────────────────────────────────────────────────
        public static readonly LogCategory UI = new("UI", new Color(1f,    0.5f,  1f   )); // Rosa
        public static readonly LogCategory UX = new("UX", new Color(0.85f, 0.4f,  1f   )); // Roxo claro

        // ── Dados ──────────────────────────────────────────────────────
        public static readonly LogCategory Data       = new("Data",       new Color(0.5f,  0.9f,  1f   )); // Ciano claro
        public static readonly LogCategory SaveSystem = new("SaveSystem", new Color(0.3f,  0.8f,  1f   )); // Azul ciano
        public static readonly LogCategory Loading    = new("Loading",    new Color(1f,    1f,    0.5f )); // Amarelo

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

        // ── Debug ──────────────────────────────────────────────────────
        public static readonly LogCategory Debug = new("Debug", new Color(0.7f, 0.7f, 0.7f)); // Cinza
    }
}
