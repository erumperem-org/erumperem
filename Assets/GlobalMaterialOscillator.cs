using UnityEngine;

public class GlobalMaterialOscillator : MonoBehaviour
{
    [SerializeField] private Material targetMaterial;
    [SerializeField] [ColorUsage(true, true)] private Color baseEmissionColor = Color.green;
    [SerializeField] private float vibrationSpeed = 8f;
    [SerializeField] private float minIntensity = 0.1f;
    [SerializeField] private float maxIntensity = 3f;

    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

    private void Update()
    {
        if (targetMaterial == null) return;

        float sineTime = (Mathf.Sin(Time.time * vibrationSpeed) + 1f) * 0.5f;
        float currentIntensity = Mathf.Lerp(minIntensity, maxIntensity, sineTime);

        Color finalColor = baseEmissionColor * currentIntensity;
        
        targetMaterial.SetColor(EmissionColorProperty, finalColor);
    }
}