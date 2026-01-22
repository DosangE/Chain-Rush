using System;
using UnityEngine;
using UnityEngine.UI;

public class VolumeUI : MonoBehaviour
{
    [Header("BGM 볼륨 슬라이더")]
    [SerializeField]
    private Slider bgmVolumeSlider;
    
    [Header("효과음 볼륨 슬라이더")]
    [SerializeField]
    private Slider sfxVolumeSlider;

    private void Start()
    {
        if (bgmVolumeSlider == null || sfxVolumeSlider == null)
        {
            Debug.LogError("볼륨 슬라이더가 할당되지 않았습니다. 볼륨 UI를 설정해주세요.");
            return;
        }
        
        bgmVolumeSlider.onValueChanged.AddListener(SetBGMVolume);
        //sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
        
        // 초기 볼륨 설정
        bgmVolumeSlider.value = SoundManager.instance.GetBGMVolume();
        //sfxVolumeSlider.value = SoundManager.instance.GetSFXVolume();
    }

    private void SetBGMVolume(float volume)
    {
        SoundManager.instance.SetBGMVolume(volume);
    }
    
    private void SetSFXVolume(float volume)
    {
        SoundManager.instance.SetSFXVolume(volume);
    }
}