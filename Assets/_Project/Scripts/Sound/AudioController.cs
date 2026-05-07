using UnityEngine;

public class AudioController : MonoBehaviour
{
    private void Start()
    {
        AudioManager.instance.PlayBGM("MainMenu");
        AudioManager.instance.PlayBGM("ExplorationMusic");
        AudioManager.instance.PlayAmbientLoop("ExplorationAmbience");
        AudioManager.instance.PlayBGM("CombatTracks");
    }
}