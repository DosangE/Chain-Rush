using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    [Header("HowToPlay Popup")]
    [SerializeField] private HowToPopupUI howToPopup; // ★ 변경
    [Header("Settings Popup")]
    [SerializeField] private VolumeUI settingsPopup; // ★ 변경

    [Header("HowToPlay Button")]
    [SerializeField] private Button htpButton;

    [Header("Settings Button")]
    [SerializeField] private Button settingsButton;

    [Header("Settings Close Button")]
    [SerializeField] private Button settingsCloseButton;
    [Header("Hard Mode")]
    [SerializeField] private Button hardModeButton;
    [SerializeField] private string hardModeSceneName = "InGame_Hard";

    private void Awake()
    {
        if (howToPopup == null || htpButton == null || settingsPopup == null || settingsButton == null || settingsCloseButton == null)
        {
            Debug.LogError("HowToPopup, SettingsPopup 또는 버튼이 할당되지 않았습니다.");
            return;
        }

        if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(Close);
        htpButton.onClick.AddListener(OnClickHowToPlay);
        settingsButton.onClick.AddListener(OnClickSettings);

        if (hardModeButton != null)
            hardModeButton.onClick.AddListener(OnClickHardMode);
    }


    private void OnClickHowToPlay()
    {
        SoundManager.instance.PlayClickSound();
        howToPopup.Open();
    }

    private void OnClickSettings()
    {
        settingsPopup.gameObject.SetActive(true);
        SoundManager.instance.PlayClickSound();
    }

    private void OnEnable()
    {
        RefreshHardModeButton();
    }

    private void RefreshHardModeButton()
    {
        if (hardModeButton == null) return;

        bool unlocked = HardModeSave.IsUnlocked();
        hardModeButton.gameObject.SetActive(unlocked);
    }
    
    private void OnClickHardMode()
    {
        SoundManager.instance.PlayClickSound();
        UnityEngine.SceneManagement.SceneManager.LoadScene(hardModeSceneName);
    }

    public void Close()
    {
        settingsPopup.gameObject.SetActive(false);
        SoundManager.instance.PlayClickSound();
    }
}