using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

[System.Serializable]
public class AmbientSoundProfile
{
    public string profileName;
    public AudioClip[] clips;
    
    [Header("Settings")]
    public Vector2 playInterval = new Vector2(10f, 30f);
    [Range(0f, 1f)] public float volume = 1f;
    public Vector2 pitchRange = new Vector2(0.9f, 1.1f);
    
    [Header("3D Space")]
    public float minDistance = 5f;
    public float maxDistance = 30f;
}

public class RandomAmbientManager : MonoBehaviour
{
    [Header("Mixer Routing")]
    public AudioMixerGroup ambientMixerGroup;

    [Header("Spawn Settings")]
    public Transform target;
    public float spawnRadius = 25f;
    public Vector2 heightOffset = new Vector2(2f, 10f);

    [Header("Sound Profiles")]
    public AmbientSoundProfile[] profiles;

    private void Start()
    {
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }

        foreach (var profile in profiles)
        {
            if (profile.clips == null || profile.clips.Length == 0) continue;
            StartCoroutine(AmbientRoutine(profile));
        }
    }

    private IEnumerator AmbientRoutine(AmbientSoundProfile profile)
    {
        GameObject emitterGO = new GameObject($"AmbientEmitter_{profile.profileName}");
        emitterGO.transform.SetParent(transform);
        
        AudioSource source = emitterGO.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = ambientMixerGroup;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = profile.minDistance;
        source.maxDistance = profile.maxDistance;
        source.playOnAwake = false;
        source.loop = false;

        while (true)
        {
            float waitTime = Random.Range(profile.playInterval.x, profile.playInterval.y);
            yield return new WaitForSeconds(waitTime);

            if (target != null)
            {
                Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
                float randomHeight = Random.Range(heightOffset.x, heightOffset.y);
                source.transform.position = target.position + new Vector3(randomCircle.x, randomHeight, randomCircle.y);
            }

            AudioClip clip = profile.clips[Random.Range(0, profile.clips.Length)];
            source.clip = clip;
            source.volume = profile.volume;
            source.pitch = Random.Range(profile.pitchRange.x, profile.pitchRange.y);
            source.Play();

            yield return new WaitForSeconds(clip.length);
        }
    }
}