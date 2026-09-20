#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace InteractionSystem.Editor
{
    [CustomPropertyDrawer(typeof(RequireInterfaceAttribute))]
    public class RequireInterfaceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (RequireInterfaceAttribute)attribute;

            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUI.HelpBox(position, "[RequireInterface] só funciona em campos de referência.", MessageType.Error);
                return;
            }

            var fieldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginChangeCheck();
            var newValue = EditorGUI.ObjectField(fieldRect, label, property.objectReferenceValue, typeof(UnityEngine.Object), true);
            if (EditorGUI.EndChangeCheck())
            {
                property.objectReferenceValue = ResolveInterfaceComponent(newValue, attr.InterfaceType);
            }

            bool hasError = property.objectReferenceValue != null
                && !attr.InterfaceType.IsInstanceOfType(property.objectReferenceValue);

            if (hasError)
            {
                var errorRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2,
                    position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.HelpBox(errorRect, $"Objeto não implementa {attr.InterfaceType.Name}.", MessageType.Error);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var attr = (RequireInterfaceAttribute)attribute;
            bool hasError = property.objectReferenceValue != null
                && !attr.InterfaceType.IsInstanceOfType(property.objectReferenceValue);

            return hasError
                ? EditorGUIUtility.singleLineHeight * 2 + 2
                : EditorGUIUtility.singleLineHeight;
        }

        private static UnityEngine.Object ResolveInterfaceComponent(UnityEngine.Object dragged, System.Type interfaceType)
        {
            if (dragged == null) return null;
            if (interfaceType.IsInstanceOfType(dragged)) return dragged;

            GameObject go = dragged as GameObject ?? (dragged as Component)?.gameObject;
            if (go != null)
            {
                var component = go.GetComponent(interfaceType);
                if (component != null) return component;
            }

            Debug.LogWarning($"'{dragged.name}' não implementa {interfaceType.Name}.");
            return null;
        }
    }
}
#endif
