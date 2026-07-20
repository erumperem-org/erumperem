using UnityEngine;
using DetectionSystem.Core;
public class ScavengerDetectionSystem : Detector
{
    public GameObject panel;

    public void Update()
    {
        Scan();
    }
    protected override void OnDetectionEnter(Collider detected, string shapeLabel, int shapeIndex)
    {
        if (detected.CompareTag("Player"))
        {
            base.OnDetectionEnter(detected, shapeLabel, shapeIndex);
            panel.SetActive(true);
        }
    }

    protected override void OnDetectionExit(Collider detected, string shapeLabel, int shapeIndex)
    {
        if (detected.CompareTag("Player"))
        {
            base.OnDetectionExit(detected, shapeLabel, shapeIndex);
            panel.SetActive(false);
        }
    }
}
