using UnityEngine;
using Player;

[RequireComponent(typeof(AudioSource))]
public class TorchAudio : MonoBehaviour
{
    [Header("Audio Clips")]
    [Tooltip("Som opcional de acender/sacar a tocha. Toca apenas uma vez ao ativar.")]
    public AudioClip ignitionClip;

    [Header("Pitch Variation Settings")]
    public float minPitch = 0.85f;
    public float maxPitch = 1.15f;

    [Header("3D Audio Bounds")]
    public float minDistance = 1f;
    public float maxDistance = 15f;

    private AudioSource _audioSource;
    private PlayableCharacter _character;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _character = GetComponentInParent<PlayableCharacter>();
        
        _audioSource.spatialBlend = 1f; 
        _audioSource.playOnAwake = false;
        _audioSource.loop = true;

        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.minDistance = minDistance;
        _audioSource.maxDistance = maxDistance;
    }

    private void OnEnable()
    {
        if (_audioSource == null) return;
        
        _audioSource.pitch = Random.Range(minPitch, maxPitch);
        
        if (ignitionClip != null && _character != null && _character.CurrentState == PlayableCharacterState.Main)
        {
            _audioSource.PlayOneShot(ignitionClip);
        }

        _audioSource.Play();
    }

    private void Update()
    {
        if (_character != null && _audioSource != null)
        {
            _audioSource.mute = _character.CurrentState != PlayableCharacterState.Main;
        }
    }

    private void OnDisable()
    {
        if (_audioSource == null) return;
        
        _audioSource.Stop();
    }
}