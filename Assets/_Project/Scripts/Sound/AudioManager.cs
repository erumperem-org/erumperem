using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;

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
    private Tween _bgmFade;
    private readonly List<AudioSource> _sfxVoices = new();

    [Header("Music Transitions")]
    [SerializeField, Min(0f)] private float _musicFadeInSeconds = 0.45f;
    [SerializeField, Min(0f)] private float _battleEndFadeSeconds = 0.8f;

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
        if (bgmSource == null || bgmPlaylists == null) return;
        Playlist p = Array.Find(bgmPlaylists, x => x != null && x.name == playlistName);
        if (p != null && p.clips != null && p.clips.Length > 0)
        {
            _bgmFade?.Kill();
            currentPlaylist = p;
            bgmSource.volume = p.volume;
            bgmSource.pitch = p.pitch;
            bgmSource.loop = false; 
            isBGMPlayingState = true;
            lastBGMIndex = -1; 

            PlayNextRandomBGM();
        }
    }

    public void FadeInBGM(string playlistName)
    {
        PlayBGM(playlistName);
        if (bgmSource == null || currentPlaylist == null || currentPlaylist.name != playlistName) return;
        bgmSource.volume = 0f;
        _bgmFade = bgmSource.DOFade(currentPlaylist.volume, _musicFadeInSeconds).SetUpdate(true);
    }

    public void FadeOutBGM(float duration)
    {
        _bgmFade?.Kill();
        isBGMPlayingState = false;
        if (bgmSource == null) return;
        _bgmFade = bgmSource.DOFade(0f, Mathf.Max(0f, duration))
            .SetUpdate(true).OnComplete(() => bgmSource.Stop());
    }

    public void PlayCombatOutcome(bool victory)
    {
        FadeOutBGM(_battleEndFadeSeconds);
        if (victory) PlaySFX("CombatVictory");
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        _bgmFade?.Kill();
        instance = null;
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

    public void PlaySFX(string soundName, float volumeMultiplier = 1f)
    {
        if (sfxClips == null || sfxSource == null || string.IsNullOrEmpty(soundName)) return;

        Sound s = Array.Find(sfxClips, x => x != null && x.name == soundName);
        if (s != null && s.clips != null && s.clips.Length > 0)
        {
            int randomIndex = 0;

            if (s.clips.Length > 1)
            {
                do
                {
                    randomIndex = UnityEngine.Random.Range(0, s.clips.Length);
                } while (randomIndex == s.lastPlayedIndex);
            }

            s.lastPlayedIndex = randomIndex;
            if (s.clips[randomIndex] != null)
            {
                var voice = GetAvailableSfxVoice();
                voice.pitch = s.pitch;
                voice.PlayOneShot(s.clips[randomIndex], s.volume * volumeMultiplier);
            }
        }
    }

    private AudioSource GetAvailableSfxVoice()
    {
        AudioSource voice = null;
        if (!sfxSource.isPlaying)
            voice = sfxSource;
        else
        {
            foreach (var candidate in _sfxVoices)
            {
                if (candidate != null && !candidate.isPlaying)
                {
                    voice = candidate;
                    break;
                }
            }
        }

        if (voice == null)
        {
            var emitter = new GameObject("SfxVoice");
            emitter.transform.SetParent(sfxSource.transform, false);
            voice = emitter.AddComponent<AudioSource>();
            _sfxVoices.Add(voice);
        }

        voice.playOnAwake = false;
        voice.loop = false;
        voice.dopplerLevel = 0f;
        if (voice != sfxSource)
        {
            voice.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
            voice.volume = sfxSource.volume;
            voice.mute = sfxSource.mute;
            voice.priority = sfxSource.priority;
            voice.panStereo = sfxSource.panStereo;
            voice.spatialBlend = sfxSource.spatialBlend;
            voice.spatialize = sfxSource.spatialize;
            voice.spread = sfxSource.spread;
            voice.minDistance = sfxSource.minDistance;
            voice.maxDistance = sfxSource.maxDistance;
            voice.rolloffMode = sfxSource.rolloffMode;
            voice.reverbZoneMix = sfxSource.reverbZoneMix;
            voice.ignoreListenerPause = sfxSource.ignoreListenerPause;
            voice.ignoreListenerVolume = sfxSource.ignoreListenerVolume;
            if (sfxSource.rolloffMode == AudioRolloffMode.Custom)
                voice.SetCustomCurve(AudioSourceCurveType.CustomRolloff,
                    sfxSource.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
        }
        return voice;
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
        _bgmFade?.Kill();
        isBGMPlayingState = false;
        if (bgmSource != null) bgmSource.Stop();
    }

    public void StopAmbientLoop()
    {
        ambientSource.Stop();
    }
    [Header("Audio Mixer")]
    public AudioMixer mainMixer;

    public void SetMasterVolume(float sliderValue)
    {
        ApplyMixerVolume("MasterVolume", sliderValue);
        PlayerPrefs.SetFloat("PrefMasterVolume", sliderValue);
    }

    public void SetBGMVolume(float sliderValue)
    {
        ApplyMixerVolume("BGMVolume", sliderValue);
        PlayerPrefs.SetFloat("PrefBGMVolume", sliderValue);
    }

    public void SetSFXVolume(float sliderValue)
    {
        ApplyMixerVolume("SFXVolume", sliderValue);
        PlayerPrefs.SetFloat("PrefSFXVolume", sliderValue);
    }

    public void SetAmbientVolume(float sliderValue)
    {
        ApplyMixerVolume("AmbientVolume", sliderValue);
        PlayerPrefs.SetFloat("PrefAmbientVolume", sliderValue);
    }

    private void ApplyMixerVolume(string exposedParameter, float sliderValue)
    {
        if (mainMixer == null)
        {
            Debug.LogWarning($"[{nameof(AudioManager)}] {nameof(mainMixer)} is not assigned; cannot set '{exposedParameter}'.", this);
            return;
        }

        float dbVolume = sliderValue <= 0.001f ? -80f : Mathf.Log10(sliderValue) * 20f;
        mainMixer.SetFloat(exposedParameter, dbVolume);
    }
}
