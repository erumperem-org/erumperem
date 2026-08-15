using UnityEngine;

public class AudioController : MonoBehaviour
{
    private void Start()
    {
        // Only one PlayBGM can be audible at boot; scene-specific tracks are triggered elsewhere.
        AudioManager.instance.PlayBGM("MainMenu");
        AudioManager.instance.PlayAmbientLoop("ExplorationAmbience");
    }
}