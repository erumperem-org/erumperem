using UnityEngine;
using UnityEditor;
using BarSystem.Bars.Health;

namespace BarSystem.Editor
{
    [CustomEditor(typeof(HealthBarInstaller))]
    public class HealthBarInstallerEditor : UnityEditor.Editor
    {
        private const float DamageAmount = 10f;
        private const float HealAmount = 10f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var installer = (HealthBarInstaller)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tests (Play Mode)", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button($"Apply Damage ({DamageAmount})"))
                        installer.ApplyDamage(DamageAmount);

                    if (GUILayout.Button($"Heal ({HealAmount})"))
                        installer.ApplyHeal(HealAmount);
                }
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to test the buttons.",
                    MessageType.Info);
        }
    }
}