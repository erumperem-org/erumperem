using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PostProcessController : MonoBehaviour
{
    [SerializeField]private Volume globalVolume;

    public void SetChromaticAberration(bool enabled)
    {
        if (globalVolume == null)
        {
            return;
        }

        if (globalVolume.profile.TryGet<ChromaticAberration>(out ChromaticAberration chromatic))
        {
            chromatic.active = true;
            chromatic.intensity.overrideState = true;
            chromatic.intensity.value = enabled ? 0.1f : 0f;
            Debug.Log("Chromatic Aberration: " + enabled);
        }
    }

    public void SetFilmGrain(bool enabled)
    {
        if (globalVolume == null)
        {
            return;
        }

        if (globalVolume.profile.TryGet<FilmGrain>(out FilmGrain grain))
        {
            grain.active = true;
            grain.intensity.overrideState = true;
            grain.intensity.value = enabled ? 1f : 0f;
        }
    }
}