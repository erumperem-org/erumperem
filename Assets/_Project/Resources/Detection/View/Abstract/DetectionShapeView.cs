using UnityEngine;

namespace DetectionSystem.View
{
    public abstract class DetectionShapeView : MonoBehaviour
    {
        [Header("Circle Settings")]
        [SerializeField] protected string shapeName = "Detection Area";
        [SerializeField] protected Color shapeColor = Color.green;

        [Header("Display")]
        [SerializeField] protected bool drawSolid = true;
        [Range(0f, 1f)]
        [SerializeField] protected float solidAlpha = 0.15f;

        [Header("Label")]
        [SerializeField] protected bool showLabel = true;
        [SerializeField] protected float labelOffset = 0.25f;

#if UNITY_EDITOR
        protected GUIStyle _cachedLabelStyle;

        protected void EnsureStyle()
        {
            if (_cachedLabelStyle == null)
            {
                _cachedLabelStyle = new GUIStyle
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            _cachedLabelStyle.normal.textColor = new Color(shapeColor.r, shapeColor.g, shapeColor.b, 1f);
        }

        protected Color GetSolidColor() => new Color(shapeColor.r, shapeColor.g, shapeColor.b, solidAlpha);
        protected Color GetBorderColor() => new Color(shapeColor.r, shapeColor.g, shapeColor.b, 1f);
#endif
    }
}