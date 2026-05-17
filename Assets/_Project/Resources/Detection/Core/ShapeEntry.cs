using System;
using System.Collections.Generic;
using UnityEngine;
using DetectionSystem.Core.Shapes;

namespace DetectionSystem.Core
{
    public enum ShapeType
    {
        Sphere,
        Box,
        Cylinder,
        Cone,
        Plane,
        Triangle
    }

    [Serializable]
    public class ShapeEntry
    {
        [Tooltip("Label shown in the Inspector foldout and debug overlay.")]
        public string label = "Shape";

        [Tooltip("Which shape geometry to use for detection.")]
        public ShapeType shapeType = ShapeType.Sphere;

        [Tooltip("Enable or disable this entry without removing it.")]
        public bool enabled = true;

        [Tooltip("Local-space offset applied to this shape's center relative to the component's transform.")]
        public Vector3 offset = Vector3.zero;

        // ── Per-shape filter ───────────────────────────────────────────

        [Tooltip("Layers this shape reacts to. Defaults to Everything.")]
        public List<LayerMask> layerMasks = new List<LayerMask> { ~0 };

        [Tooltip("Tags this shape reacts to. Empty = all tags pass.")]
        public List<string> filterTags = new List<string>();

        // ── View ───────────────────────────────────────────────────────

        [Tooltip("Gizmo color for this shape's outline and label.")]
        public Color gizmoColor = new Color(0.2f, 1f, 0.4f, 1f);

        [Tooltip("Alpha for the solid fill gizmo of this shape.")]
        [Range(0f, 1f)]
        public float solidAlpha = 0.15f;

        // ── Per-shape data ─────────────────────────────────────────────
        public SphereShape   sphere   = new SphereShape   { radius     = 1f };
        public BoxShape      box      = new BoxShape      { halfExtents = Vector3.one };
        public CylinderShape cylinder = new CylinderShape { radius = 1f, height = 2f };
        public ConeShape     cone     = new ConeShape     { distance = 5f, angle = 45f };
        public PlaneShape    plane    = new PlaneShape    { size = Vector2.one * 2f };
        public TriangleShape triangle = new TriangleShape
        {
            a = new Vector3(-1f, 0f,  0f),
            b = new Vector3( 1f, 0f,  0f),
            c = new Vector3( 0f, 0f,  2f)
        };

        // ── Cached active shape ────────────────────────────────────────
        [NonSerialized] private IDetectionShape _cachedShape;
        [NonSerialized] private ShapeType _cachedShapeType = (ShapeType)(-1);

        public IDetectionShape GetActiveShape()
        {
            if (_cachedShape == null || _cachedShapeType != shapeType)
            {
                _cachedShapeType = shapeType;
                _cachedShape = shapeType switch
                {
                    ShapeType.Sphere   => sphere,
                    ShapeType.Box      => box,
                    ShapeType.Cylinder => cylinder,
                    ShapeType.Cone     => cone,
                    ShapeType.Plane    => plane,
                    ShapeType.Triangle => triangle,
                    _                  => sphere
                };
            }
            return _cachedShape;
        }

        // ── Filter ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if <paramref name="col"/> passes this shape's layer and tag filters.
        /// </summary>
        public bool PassesFilter(Collider col)
        {
            int  objLayer = 1 << col.gameObject.layer;
            bool layerOk  = false;

            for (int i = 0; i < layerMasks.Count; i++)
                if ((objLayer & (int)layerMasks[i]) != 0) { layerOk = true; break; }

            if (!layerOk) return false;
            if (filterTags.Count == 0) return true;

            for (int i = 0; i < filterTags.Count; i++)
                if (!string.IsNullOrEmpty(filterTags[i]) && col.CompareTag(filterTags[i])) return true;

            return false;
        }

        // ── Geometry ───────────────────────────────────────────────────

        public Vector3 WorldCenter(Vector3 origin, Quaternion rotation)
            => origin + rotation * offset;

        /// <summary>
        /// Returns true if <paramref name="point"/> is inside this shape.
        /// Does NOT apply the filter — call <see cref="PassesFilter"/> separately.
        /// </summary>
        public bool Contains(Vector3 origin, Quaternion rotation, Vector3 point)
        {
            if (!enabled) return false;
            return GetActiveShape().Contains(WorldCenter(origin, rotation), rotation, point);
        }
    }
}
