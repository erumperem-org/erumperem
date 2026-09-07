#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Core.Storage.Editor
{
    /// <summary>
    /// Dropdown para selecionar a implementação concreta de qualquer campo
    /// [SerializeReference] do tipo IStorageStrategy. Sem isso, o campo
    /// apareceria vazio no Inspector, sem forma de escolher o tipo.
    /// </summary>
    [CustomPropertyDrawer(typeof(IStorageStrategy), true)]
    public sealed class StorageStrategyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var buttonRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            string currentName = string.IsNullOrEmpty(property.managedReferenceFullTypename)
                ? "<Nenhuma>"
                : GetShortTypeName(property.managedReferenceFullTypename);

            if (EditorGUI.DropdownButton(buttonRect, new GUIContent($"{label.text}: {currentName}"), FocusType.Keyboard))
                ShowTypeMenu(property);

            EditorGUI.indentLevel++;
            var contentRect = new Rect(
                position.x,
                position.y + EditorGUIUtility.singleLineHeight + 2,
                position.width,
                EditorGUI.GetPropertyHeight(property, true) - EditorGUIUtility.singleLineHeight - 2);

            EditorGUI.PropertyField(contentRect, property, GUIContent.none, true);
            EditorGUI.indentLevel--;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
                return EditorGUI.GetPropertyHeight(property, label, true);

            return EditorGUIUtility.singleLineHeight + 2 + EditorGUI.GetPropertyHeight(property, true);
        }

        private void ShowTypeMenu(SerializedProperty property)
        {
            var menu = new GenericMenu();

            var derivedTypes = TypeCache.GetTypesDerivedFrom<IStorageStrategy>()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .OrderBy(t => t.Name);

            foreach (var type in derivedTypes)
            {
                menu.AddItem(new GUIContent(type.Name), false, () =>
                {
                    property.managedReferenceValue = Activator.CreateInstance(type);
                    property.serializedObject.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }

        private static string GetShortTypeName(string fullTypeName)
        {
            int spaceIndex = fullTypeName.LastIndexOf(' ');
            string typeName = spaceIndex >= 0 ? fullTypeName[(spaceIndex + 1)..] : fullTypeName;
            int lastDot = typeName.LastIndexOf('.');
            return lastDot >= 0 ? typeName[(lastDot + 1)..] : typeName;
        }
    }
}
#endif
