using UnityEngine;
using Player;

[RequireComponent(typeof(Animator))]
public class AnimationSoundEmitter : MonoBehaviour
{
    [Header("Volume Settings")]
    [Range(0f, 1f)] public float mainVolume = 1f;
    [Range(0f, 1f)] public float companionVolume = 0.35f;

    private PlayableCharacter _character;

    private void Awake()
    {
        _character = GetComponentInParent<PlayableCharacter>();
    }

    public void PlayAnimationSound(string soundName)
    {
        if (AudioManager.instance == null || string.IsNullOrEmpty(soundName)) return;

        float targetVolume = 1f;
        
        if (_character != null)
        {
            targetVolume = _character.CurrentState == PlayableCharacterState.Main ? mainVolume : companionVolume;
        }

        AudioManager.instance.PlaySFX(soundName, targetVolume);
    }
}