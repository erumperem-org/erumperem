#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Core.CharacterStats.Authoring
{
    /// <summary>
    /// Table inspector for IntensityLibrary: one block per stat, rows from
    /// "++++" down to "----", columns Absolute | Relative.
    /// </summary>
    [CustomEditor(typeof(IntensityLibrary))]
    public sealed class IntensityLibraryEditor : Editor
    {
        private const float SymbolWidth = 56f;

        private static readonly StatIntensity[] RowOrder =
        {
            StatIntensity.Plus4, StatIntensity.Plus3, StatIntensity.Plus2, StatIntensity.Plus1,
            StatIntensity.Minus1, StatIntensity.Minus2, StatIntensity.Minus3, StatIntensity.Minus4
        };

        public override void OnInspectorGUI()
        {
            var library = (IntensityLibrary)target;
            library.EnsureLayout();

            serializedObject.Update();
            var values = serializedObject.FindProperty("_values");

            EditorGUILayout.HelpBox(
                "Absolute: signed flat change in the stat's unit (HP points; fractions like 0.05 for Defense/Critical).\n" +
                "Relative: signed percentage (10 = +10%, -25 = -25%).",
                MessageType.Info);

            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField(stat.ToString(), EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(GUIContent.none, GUILayout.Width(SymbolWidth));
                    GUILayout.Label("Absolute", EditorStyles.centeredGreyMiniLabel);
                    GUILayout.Label("Relative (%)", EditorStyles.centeredGreyMiniLabel);
                }

                foreach (var intensity in RowOrder)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(intensity.ToSymbol(), EditorStyles.boldLabel, GUILayout.Width(SymbolWidth));

                        foreach (StatModifierType type in Enum.GetValues(typeof(StatModifierType)))
                        {
                            int index = IntensityLibrary.IndexOf(stat, type, intensity);
                            EditorGUILayout.PropertyField(values.GetArrayElementAtIndex(index), GUIContent.none);
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif