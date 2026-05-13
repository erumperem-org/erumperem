using System;
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

        // ── view ───────────────────────────────────────────────────────
        [Tooltip("Gizmo color for this shape's outline and label.")]
        public Color gizmoColor = new Color(0.2f, 1f, 0.4f, 1f);

        [Tooltip("Alpha for the solid fill gizmo of this shape.")]
        [Range(0f, 1f)]
        public float solidAlpha = 0.15f;

        // ── per-shape data ─────────────────────────────────────────────
        // Only the data for the active shapeType is used at runtime,
        // but all are serialized so Unity keeps the values when you switch.

        public SphereShape   sphere   = new SphereShape   { radius      = 1f };
        public BoxShape      box      = new BoxShape      { halfExtents  = Vector3.one };
        public CylinderShape cylinder = new CylinderShape { radius = 1f, height = 2f };
        public ConeShape     cone     = new ConeShape     { distance = 5f, angle = 45f };
        public PlaneShape    plane    = new PlaneShape    { size = Vector2.one * 2f };
        public TriangleShape triangle = new TriangleShape
        {
            a = new Vector3(-1f, 0f,  0f),
            b = new Vector3( 1f, 0f,  0f),
            c = new Vector3( 0f, 0f,  2f)
        };

        // ── runtime helpers ────────────────────────────────────────────

        /// <summary>Returns the IDetectionShape instance for the active shapeType.</summary>
        public IDetectionShape GetActiveShape()
        {
            return shapeType switch
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

        /// <summary>
        /// World-space center of this entry, applying the local <see cref="offset"/>
        /// rotated by <paramref name="rotation"/>.
        /// </summary>
        public Vector3 WorldCenter(Vector3 origin, Quaternion rotation)
            => origin + rotation * offset;

        /// <summary>Returns true if <paramref name="point"/> is inside this entry's shape.</summary>
        public bool Contains(Vector3 origin, Quaternion rotation, Vector3 point)
        {
            if (!enabled) return false;
            return GetActiveShape().Contains(WorldCenter(origin, rotation), rotation, point);
        }
    }
}
