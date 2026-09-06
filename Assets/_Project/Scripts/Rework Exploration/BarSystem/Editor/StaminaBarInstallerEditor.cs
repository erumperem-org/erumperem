using UnityEngine;
using UnityEditor;
using BarSystem.Bars.Stamina;

namespace BarSystem.Editor
{
    [CustomEditor(typeof(StaminaBarInstaller))]
    public class StaminaBarInstallerEditor : UnityEditor.Editor
    {
        private const float ConsumeOnClick = 10f;
        private const float ConsumePerSecondWhileHeld = 20f;

        private double _lastHoldTime;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var installer = (StaminaBarInstaller)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tests (Play Mode)", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button($"Consume {ConsumeOnClick} (Single Click)"))
                    installer.Consume(ConsumeOnClick);

                DrawHoldButton(installer);
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to test the buttons.",
                    MessageType.Info);
        }

        /// <summary>
        /// Consumes stamina progressively while the button is being held
        /// — simulates "hold to run". Uses GUILayout.RepeatButton, which already
        /// returns true on every repaint while the mouse is held over the button,
        /// without needing to manually handle Event/MouseDown/MouseUp.
        /// </summary>
        private void DrawHoldButton(StaminaBarInstaller installer)
        {
            bool isHeld = GUILayout.RepeatButton(
                $"Hold to Consume (-{ConsumePerSecondWhileHeld}/s)");

            if (isHeld && Application.isPlaying)
            {
                double now = EditorApplication.timeSinceStartup;
                float deltaTime = (float)(now - _lastHoldTime);
                installer.Consume(ConsumePerSecondWhileHeld * deltaTime);

                // Keeps the repaint loop running while the button is held.
                Repaint();
            }

            _lastHoldTime = EditorApplication.timeSinceStartup;
        }
    }
}