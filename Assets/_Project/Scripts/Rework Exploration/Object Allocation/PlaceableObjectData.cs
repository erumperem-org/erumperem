using UnityEngine;

namespace SceneAllocation
{
    /// <summary>
    /// Defines a placeable object type for the Scene Object Allocation System.
    /// Holds the prefab to instantiate, together with the min/max scale and
    /// min/max rotation ranges (per axis) that will be applied whenever this
    /// object type is placed in the scene.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewPlaceableObjectData",
        menuName = "Scene Allocation/Placeable Object Data",
        order = 0)]
    public class PlaceableObjectData : ScriptableObject
    {
        [Header("Prefab")]
        [Tooltip("Prefab that will be instantiated when this object is allocated.")]
        [SerializeField] private GameObject prefab;

        [Header("Scale Range (uniform)")]
        [Tooltip("Minimum uniform scale. The same value is applied to X, Y and Z, " +
                 "so the object is scaled up/down as a whole without distorting its proportions.")]
        [SerializeField] private float minScale = 1f;
        [Tooltip("Maximum uniform scale. The same value is applied to X, Y and Z, " +
                 "so the object is scaled up/down as a whole without distorting its proportions.")]
        [SerializeField] private float maxScale = 1f;

        [Header("Rotation Range (Euler angles, degrees, per axis)")]
        [Tooltip("Minimum rotation allowed on each axis, in degrees.")]
        [SerializeField] private Vector3 minRotation = Vector3.zero;
        [Tooltip("Maximum rotation allowed on each axis, in degrees.")]
        [SerializeField] private Vector3 maxRotation = Vector3.zero;

        public GameObject Prefab => prefab;
        public float MinScale => minScale;
        public float MaxScale => maxScale;
        public Vector3 MinRotation => minRotation;
        public Vector3 MaxRotation => maxRotation;

        /// <summary>
        /// Generates a single random uniform scale factor within the configured
        /// [minScale, maxScale] range and returns it as a Vector3 with the same
        /// value on X, Y and Z, so the object grows/shrinks as a whole without
        /// distorting its original proportions.
        /// </summary>
        public Vector3 GetRandomScale()
        {
            float scale = Random.Range(minScale, maxScale);
            return new Vector3(scale, scale, scale);
        }

        /// <summary>
        /// Generates a random rotation (as a Quaternion built from Euler angles)
        /// within the configured [minRotation, maxRotation] range, evaluated
        /// independently per axis.
        /// </summary>
        public Quaternion GetRandomRotation()
        {
            Vector3 euler = new Vector3(
                Random.Range(minRotation.x, maxRotation.x),
                Random.Range(minRotation.y, maxRotation.y),
                Random.Range(minRotation.z, maxRotation.z));

            return Quaternion.Euler(euler);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Keeps min values from ever exceeding max values when edited in the
        /// Inspector, to avoid Random.Range(min, max) producing inverted ranges.
        /// </summary>
        private void OnValidate()
        {
            maxScale = Mathf.Max(minScale, maxScale);

            maxRotation = new Vector3(
                Mathf.Max(minRotation.x, maxRotation.x),
                Mathf.Max(minRotation.y, maxRotation.y),
                Mathf.Max(minRotation.z, maxRotation.z));
        }
#endif
    }
}