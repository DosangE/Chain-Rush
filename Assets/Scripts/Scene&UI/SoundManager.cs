using System;
using UnityEngine;
using UnityEngine.Audio;

public class SoundManager : MonoBehaviour
{
    public static SoundManager instance;

    [Header("Audio Sources")] public AudioSource bgmSource;
    public AudioSource sfxSource;

    [Header("Audio Clips")] 
    public AudioClip lobbyBGM;
    public AudioClip mainBGM;
    public AudioClip clickSFX;
    public AudioClip hitSFX;
    public AudioClip jumpSFX;
    public AudioClip grappleAttachSFX;

    [Header("Audio Mixer")] [SerializeField]
    private AudioMixer mixer;

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

    private void Start()
    {
        // 초기 볼륨 설정
        if (mixer != null)
        {
            float bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 0f);
            float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0f);
            mixer.SetFloat("BGM", bgmVolume);
            mixer.SetFloat("SFX", sfxVolume);
        } 
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || bgmSource == null) return;

        if (bgmSource.clip == clip && bgmSource.isPlaying)
            return; // 같은 BGM이면 다시 재생하지 않음

        bgmSource.Stop();
        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void PlayClickSound() => PlaySFX(clickSFX);
    public void PlayHitSound() => PlaySFX(hitSFX);

    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }

    public void SetBGMVolume(float volume)
    {
        if (volume <= -40.0f)
            volume = -80.0f; // -40dB 이하로 설정하면 음소거로 간주

        if (mixer != null)
        {
            mixer.SetFloat("BGM", volume);
            PlayerPrefs.SetFloat("BGMVolume", volume);
        }
    }

    public void SetSFXVolume(float volume)
    {
        if (volume <= -40.0f)
            volume = -80.0f; // -40dB 이하로 설정하면 음소거로 간주

        if (mixer != null)
        {
            mixer.SetFloat("SFX", volume);
            PlayerPrefs.SetFloat("SFXVolume", volume);
        }
    }

    public float GetBGMVolume()
    {
        if (mixer != null)
        {
            mixer.GetFloat("BGM", out float volume);
            return volume;
        }

        return 0f; // 기본값
    }

    public float GetSFXVolume()
    {
        if (mixer != null)
        {
            mixer.GetFloat("SFX", out float volume);
            return volume;
        }

        return 0f; // 기본값
    }
}