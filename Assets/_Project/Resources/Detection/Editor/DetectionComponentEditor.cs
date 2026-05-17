#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using DetectionSystem.Core;

namespace DetectionSystem.Editor
{
    [CustomEditor(typeof(DetectionComponent))]
    public class DetectionComponentEditor : UnityEditor.Editor
    {
        // ── serialized props ───────────────────────────────────────────
        SerializedProperty _shapes;
        SerializedProperty _drawGizmos;
        SerializedProperty _showLabels;

        // ── fold state per entry ────────────────────────────────────────
        private bool[] _foldouts = new bool[0];

        // ── style cache ─────────────────────────────────────────────────
        private GUIStyle _headerStyle;
        private GUIStyle _foldoutStyle;

        private static readonly Color HeaderBg    = new Color(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Color EnabledBg   = new Color(0.22f, 0.38f, 0.22f, 0.35f);
        private static readonly Color DisabledBg  = new Color(0.38f, 0.22f, 0.22f, 0.25f);
        private static readonly Color SeparatorC  = new Color(1f, 1f, 1f, 0.08f);

        void OnEnable()
        {
            _shapes     = serializedObject.FindProperty("shapes");
            _drawGizmos = serializedObject.FindProperty("drawGizmos");
            _showLabels = serializedObject.FindProperty("showLabels");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            BuildStyles();
            SyncFoldouts();

            // ── Gizmos section ─────────────────────────────────────────
            DrawSectionHeader("Gizmos");
            EditorGUILayout.PropertyField(_drawGizmos, new GUIContent("Draw Gizmos"));
            EditorGUILayout.PropertyField(_showLabels, new GUIContent("Show Labels"));

            EditorGUILayout.Space(6);

            // ── Shapes section ─────────────────────────────────────────
            DrawSectionHeader($"Detection Shapes  ({_shapes.arraySize})");
            EditorGUILayout.Space(2);

            for (int i = 0; i < _shapes.arraySize; i++)
                DrawShapeEntry(i);

            EditorGUILayout.Space(4);

            // ── Add / Clear buttons ────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("＋  Add Shape", GUILayout.Height(24)))
                {
                    _shapes.InsertArrayElementAtIndex(_shapes.arraySize);
                    // Give the new entry a default label
                    var newProp = _shapes.GetArrayElementAtIndex(_shapes.arraySize - 1);
                    newProp.FindPropertyRelative("label").stringValue = $"Shape {_shapes.arraySize}";
                    newProp.FindPropertyRelative("enabled").boolValue = true;
                    SyncFoldouts();
                    _foldouts[_shapes.arraySize - 1] = true;
                }

                GUI.enabled = _shapes.arraySize > 0;
                if (GUILayout.Button("Clear All", GUILayout.Height(24), GUILayout.Width(90)))
                    if (EditorUtility.DisplayDialog("Clear Shapes",
                            "Remove all detection shapes?", "Yes", "Cancel"))
                        _shapes.ClearArray();
                GUI.enabled = true;
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ── Entry drawer ───────────────────────────────────────────────

        private void DrawShapeEntry(int index)
        {
            var entryProp = _shapes.GetArrayElementAtIndex(index);

            var labelProp     = entryProp.FindPropertyRelative("label");
            var enabledProp   = entryProp.FindPropertyRelative("enabled");
            var shapeTypeProp = entryProp.FindPropertyRelative("shapeType");

            bool isEnabled = enabledProp.boolValue;
            var  type      = (ShapeType)shapeTypeProp.enumValueIndex;

            // ── Row background ─────────────────────────────────────────
            Color bgColor = isEnabled ? EnabledBg : DisabledBg;
            Rect  bgRect  = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(bgRect, bgColor);

            // ── Header row ─────────────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                // Enabled toggle
                bool newEnabled = EditorGUILayout.Toggle(isEnabled, GUILayout.Width(18));
                if (newEnabled != isEnabled) enabledProp.boolValue = newEnabled;

                // Foldout
                string headerLabel = $"[{index}]  {labelProp.stringValue}  —  {type}";
                _foldouts[index] = EditorGUILayout.Foldout(_foldouts[index], headerLabel, true, _foldoutStyle);

                GUILayout.FlexibleSpace();

                // Move up
                GUI.enabled = index > 0;
                if (GUILayout.Button("▲", GUILayout.Width(22), GUILayout.Height(18)))
                    _shapes.MoveArrayElement(index, index - 1);
                GUI.enabled = true;

                // Move down
                GUI.enabled = index < _shapes.arraySize - 1;
                if (GUILayout.Button("▼", GUILayout.Width(22), GUILayout.Height(18)))
                    _shapes.MoveArrayElement(index, index + 1);
                GUI.enabled = true;

                // Remove
                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                {
                    _shapes.DeleteArrayElementAtIndex(index);
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

            // ── Expanded body ──────────────────────────────────────────
            if (_foldouts[index])
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(labelProp,     new GUIContent("Label"));
                EditorGUILayout.PropertyField(enabledProp,   new GUIContent("Enabled"));
                EditorGUILayout.PropertyField(shapeTypeProp, new GUIContent("Shape Type"));

                EditorGUILayout.Space(4);

                // ── Transform ─────────────────────────────────────────
                EditorGUILayout.LabelField("Transform", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    entryProp.FindPropertyRelative("offset"),
                    new GUIContent("Center Offset", "Local-space displacement of this shape's center."));

                EditorGUILayout.Space(4);

                // ── Filter ────────────────────────────────────────────
                EditorGUILayout.LabelField("Filter", EditorStyles.boldLabel);
                DrawLayerMaskList(entryProp.FindPropertyRelative("layerMasks"));
                DrawTagList(entryProp.FindPropertyRelative("filterTags"));

                EditorGUILayout.Space(4);

                // ── View ──────────────────────────────────────────────
                EditorGUILayout.LabelField("View", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    entryProp.FindPropertyRelative("gizmoColor"),
                    new GUIContent("Color"));
                EditorGUILayout.PropertyField(
                    entryProp.FindPropertyRelative("solidAlpha"),
                    new GUIContent("Fill Alpha"));

                EditorGUILayout.Space(4);

                DrawShapeData(entryProp, (ShapeType)shapeTypeProp.enumValueIndex);

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndVertical();

            // ── Separator ──────────────────────────────────────────────
            Rect sep = GUILayoutUtility.GetRect(1, 1);
            EditorGUI.DrawRect(sep, SeparatorC);
            EditorGUILayout.Space(2);
        }

        // ── Shape-specific fields ──────────────────────────────────────

        private void DrawShapeData(SerializedProperty entry, ShapeType type)
        {
            EditorGUILayout.LabelField("Shape Properties", EditorStyles.boldLabel);

            switch (type)
            {
                case ShapeType.Sphere:
                {
                    var sp = entry.FindPropertyRelative("sphere");
                    EditorGUILayout.PropertyField(sp.FindPropertyRelative("radius"),
                        new GUIContent("Radius"));
                    break;
                }

                case ShapeType.Box:
                {
                    var bp = entry.FindPropertyRelative("box");
                    EditorGUILayout.PropertyField(bp.FindPropertyRelative("halfExtents"),
                        new GUIContent("Half Extents"));
                    break;
                }

                case ShapeType.Cylinder:
                {
                    var cp = entry.FindPropertyRelative("cylinder");
                    EditorGUILayout.PropertyField(cp.FindPropertyRelative("radius"),
                        new GUIContent("Radius"));
                    EditorGUILayout.PropertyField(cp.FindPropertyRelative("height"),
                        new GUIContent("Height"));
                    break;
                }

                case ShapeType.Cone:
                {
                    var cp = entry.FindPropertyRelative("cone");
                    EditorGUILayout.PropertyField(cp.FindPropertyRelative("distance"),
                        new GUIContent("Distance"));
                    EditorGUILayout.PropertyField(cp.FindPropertyRelative("angle"),
                        new GUIContent("Angle (degrees)"));
                    break;
                }

                case ShapeType.Plane:
                {
                    var pp = entry.FindPropertyRelative("plane");
                    EditorGUILayout.PropertyField(pp.FindPropertyRelative("size"),
                        new GUIContent("Size (X × Z)"));
                    break;
                }

                case ShapeType.Triangle:
                {
                    var tp = entry.FindPropertyRelative("triangle");
                    EditorGUILayout.PropertyField(tp.FindPropertyRelative("a"),
                        new GUIContent("Vertex A"));
                    EditorGUILayout.PropertyField(tp.FindPropertyRelative("b"),
                        new GUIContent("Vertex B"));
                    EditorGUILayout.PropertyField(tp.FindPropertyRelative("c"),
                        new GUIContent("Vertex C"));
                    EditorGUILayout.HelpBox(
                        "Triangle detection operates on the XZ plane (Y is ignored).",
                        MessageType.Info);
                    break;
                }
            }
        }

        // ── Filter drawers ─────────────────────────────────────────────

        /// <summary>
        /// Draws a List&lt;LayerMask&gt; as individual EditorGUI.LayerField rows
        /// with ＋ / ✕ buttons, since Unity does not render LayerMask lists
        /// correctly out of the box.
        /// </summary>
        private void DrawLayerMaskList(SerializedProperty listProp)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Layer Masks", "Layers this shape reacts to. OR-combined."),
                    GUILayout.Width(EditorGUIUtility.labelWidth));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("＋", GUILayout.Width(22), GUILayout.Height(16)))
                {
                    listProp.InsertArrayElementAtIndex(listProp.arraySize);
                    // Default to Everything (~0)
                    listProp.GetArrayElementAtIndex(listProp.arraySize - 1).intValue = ~0;
                }
            }

            EditorGUI.indentLevel++;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var elem = listProp.GetArrayElementAtIndex(i);

                using (new EditorGUILayout.HorizontalScope())
                {
                    // LayerField expects a layer index, but LayerMask stores a bitmask.
                    // We store the raw bitmask and convert for display / editing.
                    int currentMask  = elem.intValue;
                    int currentLayer = LayerMaskToLayer(currentMask);

                    EditorGUI.BeginChangeCheck();
                    int newLayer = EditorGUILayout.LayerField(
                        new GUIContent($"Mask {i}"),
                        currentLayer < 0 ? 0 : currentLayer);
                    if (EditorGUI.EndChangeCheck())
                        elem.intValue = 1 << newLayer;

                    if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(16)))
                    {
                        listProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }

            if (listProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "No layer masks — nothing will be detected by this shape.",
                    MessageType.Warning);
            }
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// Returns the index of the single layer represented by <paramref name="mask"/>,
        /// or -1 when the mask is 0 / multi-layer / Everything.
        /// </summary>
        private static int LayerMaskToLayer(int mask)
        {
            if (mask == 0 || mask == ~0) return -1;
            for (int i = 0; i < 32; i++)
                if ((mask & (1 << i)) != 0) return i;
            return -1;
        }

        /// <summary>
        /// Draws a List&lt;string&gt; of tag filters with ＋ / ✕ buttons
        /// and a TagField per row.
        /// </summary>
        private void DrawTagList(SerializedProperty listProp)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Filter Tags", "Tags this shape reacts to. Empty = all tags pass."),
                    GUILayout.Width(EditorGUIUtility.labelWidth));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("＋", GUILayout.Width(22), GUILayout.Height(16)))
                {
                    listProp.InsertArrayElementAtIndex(listProp.arraySize);
                    listProp.GetArrayElementAtIndex(listProp.arraySize - 1).stringValue = "Untagged";
                }
            }

            EditorGUI.indentLevel++;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var elem = listProp.GetArrayElementAtIndex(i);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    string newTag = EditorGUILayout.TagField(
                        new GUIContent($"Tag {i}"),
                        string.IsNullOrEmpty(elem.stringValue) ? "Untagged" : elem.stringValue);
                    if (EditorGUI.EndChangeCheck())
                        elem.stringValue = newTag;

                    if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(16)))
                    {
                        listProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }

            if (listProp.arraySize == 0)
            {
                using (new EditorGUI.IndentLevelScope())
                    EditorGUILayout.LabelField(
                        "(empty — all tags pass)",
                        EditorStyles.miniLabel);
            }
            EditorGUI.indentLevel--;
        }

        // ── Helpers ────────────────────────────────────────────────────

        private void DrawSectionHeader(string title)
        {
            Rect r = GUILayoutUtility.GetRect(1, 22, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, HeaderBg);
            GUI.Label(r, title, _headerStyle);
        }

        private void SyncFoldouts()
        {
            if (_foldouts.Length != _shapes.arraySize)
            {
                var old = _foldouts;
                _foldouts = new bool[_shapes.arraySize];
                for (int i = 0; i < Mathf.Min(old.Length, _foldouts.Length); i++)
                    _foldouts[i] = old[i];
            }
        }

        private void BuildStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize  = 12,
                    alignment = TextAnchor.MiddleLeft,
                    padding   = new RectOffset(8, 0, 0, 0)
                };
                _headerStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            }

            if (_foldoutStyle == null)
            {
                _foldoutStyle = new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold
                };
            }
        }
    }
}
#endif
