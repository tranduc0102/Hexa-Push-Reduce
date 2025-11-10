using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header("Background Music")]
    public AudioSource bgmSource;
    public AudioClip bgmClip;

    [Header("Sound Effects")]
    public AudioClip soundWin;
    public AudioClip soundLose;
    public AudioClip soundCollectCoin;

    public AudioSource sfxSource;

    private static AudioManager instance;
    public static AudioManager Instance => instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        bgmSource.volume = PlayerPrefs.GetFloat("VolumnSound", 1);
        sfxSource.volume = PlayerPrefs.GetFloat("VolumnSound", 1);
    }

    private void Start()
    {
        PlayBGM();
    }

    public void PlayBGM()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.volume = 0.5f;
        }

        if (bgmClip != null)
        {
            bgmSource.clip = bgmClip;
            bgmSource.Play();
        }
    }

    public void StopBGM()
    {
        if (bgmSource != null && bgmSource.isPlaying)
            bgmSource.Stop();
    }

    public void PlayWin() => PlaySFX(soundWin);
    public void PlayLose() => PlaySFX(soundLose);
    public void PlayCollectCoin() => PlaySFX(soundCollectCoin);
    private void PlaySFX(AudioClip clip)
    {
        if (clip != null)
            sfxSource.PlayOneShot(clip);
    }

    public void SetValue(float value)
    {
        PlayerPrefs.SetFloat("VolumnSound",value);
        bgmSource.volume = value;
        sfxSource.volume = value;
    }
}
