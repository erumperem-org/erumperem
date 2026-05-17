using System.Text;
using UnityEngine;

namespace Services.DebugUtilities
{
    /// <summary>
    /// Console logger. Accepts one or more <see cref="LogCategory"/> tags per message.
    /// </summary>
    public static class LoggerService
    {
        // Reusable builder — avoids per-call allocation for the formatted string.
        private static readonly StringBuilder _sb = new(256);

        /// <summary>Logs a message with one or more category tags.</summary>
        public static void PrintLogMessage(LogLevel level, string message, params LogCategory[] categories)
        {
            string formatted = BuildMessage(message, categories);
            Dispatch(level, formatted);
        }

        /// <summary>Logs a message with one or more category tags and a success indicator.</summary>
        public static void PrintLogMessage(LogLevel level, bool success, string message, params LogCategory[] categories)
        {
            Color  statusColor = success ? Color.green : Color.red;
            string statusHex   = ColorUtility.ToHtmlStringRGB(statusColor);
            string statusLabel = success ? "[SUCCESSFUL]" : "[FAILURE]";

            string categoryPart = BuildCategoryPrefix(categories);
            string formatted    = $"{categoryPart}<color=#{statusHex}>{statusLabel}</color> {message}";
            Dispatch(level, formatted);
        }

        // ── Internal helpers ───────────────────────────────────────────

        private static string BuildMessage(string message, LogCategory[] categories)
            => $"{BuildCategoryPrefix(categories)}{message}";

        private static string BuildCategoryPrefix(LogCategory[] categories)
        {
            if (categories == null || categories.Length == 0) return string.Empty;

            _sb.Clear();
            foreach (var cat in categories)
            {
                string hex = ColorUtility.ToHtmlStringRGB(cat.Color);
                _sb.Append($"<color=#{hex}>[{cat.Name.ToUpper()}]</color> ");
            }
            return _sb.ToString();
        }

        private static void Dispatch(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Warning: UnityEngine.Debug.LogWarning(message); break;
                case LogLevel.Error:   UnityEngine.Debug.LogError(message);   break;
                default:               UnityEngine.Debug.Log(message);        break;
            }
        }
    }
}
