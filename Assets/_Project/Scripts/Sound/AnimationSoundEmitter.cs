using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationSoundEmitter : MonoBehaviour
{
    public void PlayAnimationSound(string soundName)
    {
        if (AudioManager.instance != null && !string.IsNullOrEmpty(soundName))
        {
            AudioManager.instance.PlaySFX(soundName);
        }
    }
}