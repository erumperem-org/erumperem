using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System;

namespace Services.DebugUtilities
{
    /// <summary>
    /// Console logger. Accepts one or more <see cref="LogCategory"/> tags per message.
    /// </summary>
    public static class LoggerService
    {
        // Thread-static: each thread gets its own builder, avoiding cross-thread
        // corruption if logging happens off the main thread (e.g. networking).
        [ThreadStatic]
        private static StringBuilder _sb;

        private static StringBuilder Builder => _sb ??= new StringBuilder(256);

        // ── Runtime filtering ────────────────────────────────────────────

        /// <summary>
        /// Categories currently allowed to log. Null means "all categories enabled".
        /// </summary>
        private static HashSet<LogCategory> _activeCategories;

        /// <summary>Minimum level that will be dispatched, regardless of category.</summary>
        public static LogLevel MinimumLevel = LogLevel.Debug;

        public static void SetActiveCategories(IEnumerable<LogCategory> categories)
        {
            _activeCategories = categories == null ? null : new HashSet<LogCategory>(categories);
        }

        public static void ClearCategoryFilter() => _activeCategories = null;

        private static bool IsEnabled(LogLevel level, LogCategory[] categories)
        {
            if (level < MinimumLevel) return false;
            if (_activeCategories == null) return true;
            if (categories == null || categories.Length == 0) return true;

            foreach (var cat in categories)
                if (_activeCategories.Contains(cat)) return true;

            return false;
        }

        private static bool IsEnabled(LogLevel level, LogCategory category)
        {
            if (level < MinimumLevel) return false;
            if (_activeCategories == null) return true;
            return category == null || _activeCategories.Contains(category);
        }

        // ── Public API - single category (avoids params[] allocation) ───

        public static void PrintLogMessage(LogLevel level, string message, LogCategory category, UnityEngine.Object context = null)
        {
            if (!IsEnabled(level, category)) return;
            Dispatch(level, BuildMessage(message, category), context);
        }

        public static void PrintLogMessage(LogLevel level, bool success, string message, LogCategory category, UnityEngine.Object context = null)
        {
            if (!IsEnabled(level, category)) return;
            Dispatch(level, BuildMessageWithStatus(message, success, category), context);
        }

        // ── Public API - multiple categories (params[], convenience) ────

        public static void PrintLogMessage(LogLevel level, string message, UnityEngine.Object context, params LogCategory[] categories)
        {
            if (!IsEnabled(level, categories)) return;
            Dispatch(level, BuildMessage(message, categories), context);
        }

        public static void PrintLogMessage(LogLevel level, bool success, string message, UnityEngine.Object context, params LogCategory[] categories)
        {
            if (!IsEnabled(level, categories)) return;
            Dispatch(level, BuildMessageWithStatus(message, success, categories), context);
        }

        // ── Stripped-in-release convenience methods ──────────────────────
        // Compiled out entirely in release builds (no formatting, no call overhead).

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogDebug(string message, LogCategory category, UnityEngine.Object context = null)
            => PrintLogMessage(LogLevel.Debug, message, category, context);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogDebug(string message, UnityEngine.Object context, params LogCategory[] categories)
            => PrintLogMessage(LogLevel.Debug, message, context, categories);

        // Warning/Error are intentionally NOT [Conditional] - they should still
        // run in release builds, only Debug-level noise gets stripped.
        public static void LogWarning(string message, LogCategory category, UnityEngine.Object context = null)
            => PrintLogMessage(LogLevel.Warning, message, category, context);

        public static void LogError(string message, LogCategory category, UnityEngine.Object context = null)
            => PrintLogMessage(LogLevel.Error, message, category, context);

        // ── Internal helpers ───────────────────────────────────────────

        private static string BuildMessage(string message, LogCategory category)
            => category == null ? message : category.TagFormatted + message;

        private static string BuildMessage(string message, LogCategory[] categories)
            => BuildCategoryPrefix(categories) + message;

        private static string BuildMessageWithStatus(string message, bool success, LogCategory category)
        {
            string statusPart = success ? SuccessTag : FailureTag;
            string prefix     = category == null ? string.Empty : category.TagFormatted;
            return $"{prefix}{statusPart} {message}";
        }

        private static string BuildMessageWithStatus(string message, bool success, LogCategory[] categories)
        {
            string statusPart = success ? SuccessTag : FailureTag;
            return $"{BuildCategoryPrefix(categories)}{statusPart} {message}";
        }

        // Success/failure tags are constant (Color.green/Color.red never change),
        // so they're precomputed once instead of formatted on every call.
        private static readonly string SuccessTag =
            $"<color=#{ColorUtility.ToHtmlStringRGB(Color.green)}>[SUCCESSFUL]</color>";
        private static readonly string FailureTag =
            $"<color=#{ColorUtility.ToHtmlStringRGB(Color.red)}>[FAILURE]</color>";

        private static string BuildCategoryPrefix(LogCategory[] categories)
        {
            if (categories == null || categories.Length == 0) return string.Empty;
            if (categories.Length == 1) return categories[0].TagFormatted;

            var sb = Builder;
            sb.Clear();
            foreach (var cat in categories)
                sb.Append(cat.TagFormatted);
            return sb.ToString();
        }

        private static void Dispatch(LogLevel level, string message, UnityEngine.Object context)
        {
            switch (level)
            {
                case LogLevel.Warning: Debug.LogWarning(message, context); break;
                case LogLevel.Error:   Debug.LogError(message, context);   break;
                default:               Debug.Log(message, context);        break;
            }
        }


        //Legacy 
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
