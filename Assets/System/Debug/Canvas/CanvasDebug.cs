using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Services.DebugUtilities.Console;
namespace Services.DebugUtilities.Canvas
{
    public static class CanvasLoggerService
    {
        public static DebugCanvasObject UI;

        public static void Initialize(DebugCanvasObject ui)
        {
            UI = ui;
        }

        public static void PrintLogMessage(LogLevel logLevel, HashSet<LogCategory> categories, string message)
        {
            string formattedMessage = $"[{logLevel.ToString().ToUpper()}] + {FormatCategories(categories)}{message}";

            Dispatch(logLevel, formattedMessage);
            UI?.ShowMessage(formattedMessage);
        }

        public static void PrintLogMessage(LogLevel logLevel, HashSet<LogCategory> categories, bool success, string message)
        {
            string categoryPart = FormatCategories(categories);

            Color statusColor = success ? Color.green : Color.red;
            string statusHex = ColorUtility.ToHtmlStringRGB(statusColor);
            string statusLabel = success ? "[SUCCESSFUL]" : "[FAILURE]";

            string formattedMessage =
                $"{categoryPart}<color=#{statusHex}>{statusLabel}</color> {message}";

            Dispatch(logLevel, formattedMessage);
            UI?.ShowMessage(formattedMessage);
        }

        private static string FormatCategories(HashSet<LogCategory> categories)
        {
            System.Text.StringBuilder sb = new();

            foreach (var category in categories.OrderBy(c => c.Name))
            {
                string hex = ColorUtility.ToHtmlStringRGB(category.Color);
                sb.Append($"<color=#{hex}>[{category.Name.ToUpper()}]</color> ");
            }

            return sb.ToString();
        }

        private static void Dispatch(LogLevel logLevel, string message)
        {
            switch (logLevel)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(message);
                    break;
                case LogLevel.Error:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }
    }
}