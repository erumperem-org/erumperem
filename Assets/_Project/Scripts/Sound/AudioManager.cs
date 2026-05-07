using System;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    public Playlist[] bgmPlaylists; 
    public Sound[] sfxClips;        
    public Sound[] ambientLoops;    

    public AudioSource bgmSource;
    public AudioSource sfxSource;
    public AudioSource ambientSource;

    private Playlist currentPlaylist;
    private int lastBGMIndex = -1;
    private bool isBGMPlayingState = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (isBGMPlayingState && !bgmSource.isPlaying && currentPlaylist != null)
        {
            PlayNextRandomBGM();
        }
    }

    public void PlayBGM(string playlistName)
    {
        Playlist p = Array.Find(bgmPlaylists, x => x.name == playlistName);
        if (p != null && p.clips.Length > 0)
        {
            currentPlaylist = p;
            bgmSource.volume = p.volume;
            bgmSource.pitch = p.pitch;
            bgmSource.loop = false; 
            isBGMPlayingState = true;
            lastBGMIndex = -1; 

            PlayNextRandomBGM();
        }
    }

    private void PlayNextRandomBGM()
    {
        if (currentPlaylist.clips.Length == 0) return;

        int randomIndex = 0;

        if (currentPlaylist.clips.Length > 1)
        {
            do
            {
                randomIndex = UnityEngine.Random.Range(0, currentPlaylist.clips.Length);
            } while (randomIndex == lastBGMIndex);
        }

        lastBGMIndex = randomIndex;
        bgmSource.clip = currentPlaylist.clips[randomIndex];
        bgmSource.Play();
    }

    public void PlaySFX(string soundName)
    {
        Sound s = Array.Find(sfxClips, x => x.name == soundName);
        if (s != null && s.clips.Length > 0)
        {
            sfxSource.pitch = s.pitch;

            int randomIndex = 0;

            // Se houver mais de um áudio, aplica a lógica matemática de não-repetição
            if (s.clips.Length > 1)
            {
                do
                {
                    randomIndex = UnityEngine.Random.Range(0, s.clips.Length);
                } while (randomIndex == s.lastPlayedIndex);
            }

            s.lastPlayedIndex = randomIndex;
            sfxSource.PlayOneShot(s.clips[randomIndex], s.volume);
        }
    }

    public void PlayAmbientLoop(string soundName)
    {
        Sound s = Array.Find(ambientLoops, x => x.name == soundName);
        if (s != null && s.clips.Length > 0)
        {
            ambientSource.clip = s.clips[0]; 
            ambientSource.volume = s.volume;
            ambientSource.pitch = s.pitch;
            ambientSource.loop = true;
            ambientSource.Play();
        }
    }

    public void StopBGM()
    {
        isBGMPlayingState = false;
        bgmSource.Stop();
    }

    public void StopAmbientLoop()
    {
        ambientSource.Stop();
    }
}