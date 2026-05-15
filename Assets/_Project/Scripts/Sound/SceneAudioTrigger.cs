using System.Collections;
using UnityEngine;

public class SceneAudioTrigger : MonoBehaviour
{
    [Header("Audio Tracks")]
    public string bgmPlaylistName;
    public string ambientLoopName;

    // Transformamos o Start em um IEnumerator para gerar um micro-atraso calculado
    private IEnumerator Start()
    {
        // Aguarda estritamente o carregamento total do primeiro frame para evitar Race Conditions
        yield return new WaitForEndOfFrame();

        if (AudioManager.instance == null) yield break;

        if (!string.IsNullOrEmpty(bgmPlaylistName))
        {
            AudioManager.instance.PlayBGM(bgmPlaylistName);
        }
        else
        {
            AudioManager.instance.StopBGM();
        }

        if (!string.IsNullOrEmpty(ambientLoopName))
        {
            AudioManager.instance.PlayAmbientLoop(ambientLoopName);
        }
        else
        {
            AudioManager.instance.StopAmbientLoop();
        }
    }
}