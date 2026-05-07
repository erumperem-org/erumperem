using UnityEngine;

[System.Serializable]
public class Sound
{
    public string name;
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;

    [HideInInspector] public int lastPlayedIndex = -1;
}

[System.Serializable]
public class Playlist
{
    public string name;
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;
}