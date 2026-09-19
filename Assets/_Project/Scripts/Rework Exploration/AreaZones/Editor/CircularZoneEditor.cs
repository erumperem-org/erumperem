using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor compartilhado para qualquer <see cref="CircularZone"/>
/// (MapLimits, SafeArea, Hub, e qualquer subclasse futura): desenha um
/// handle de raio arrastável no Scene view, em vez de precisar digitar o
/// valor manualmente no Inspector. Funciona automaticamente para novas
/// subclasses por causa do <c>true</c> em <see cref="CustomEditor"/>
/// (aplica-se a tipos derivados).
/// </summary>
[CustomEditor(typeof(CircularZone), true)]
[CanEditMultipleObjects]
public class CircularZoneEditor : Editor
{
    protected SerializedProperty radiusProperty;

    protected virtual void OnEnable()
    {
        radiusProperty = serializedObject.FindProperty("radius");
    }

public override void OnInspectorGUI()
{
    serializedObject.Update();
    SerializedProperty iterator = serializedObject.GetIterator();
    bool enterChildren = true;

    while (iterator.NextVisible(enterChildren))
    {
        enterChildren = false;

        if (iterator.propertyPath == "m_Script")
        {
            continue;
        }

        EditorGUILayout.PropertyField(iterator, true);
    }

    serializedObject.ApplyModifiedProperties();
}

    protected virtual void OnSceneGUI()
    {
        var zone = (CircularZone)target;

        EditorGUI.BeginChangeCheck();
        float newRadius = Handles.RadiusHandle(Quaternion.identity, zone.Center, zone.Radius);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(zone, "Ajustar raio da zona");
            radiusProperty.floatValue = Mathf.Max(0f, newRadius);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
