using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    [Header("로비 UI")]
    [SerializeField]
    private GameObject lobbyUI;

    [Header("HowToPlay UI")]
    [SerializeField]
    private GameObject htpUI;

    [Header("HowToPlay Button")]
    [SerializeField]
    private Button htpButton;

    [Header("Return Button")]
    [SerializeField]
    private Button returnButton;

    private void Awake()
    {
        if (lobbyUI == null || htpUI == null || htpButton == null)
        {
            Debug.LogError("로비 UI 또는 설정 UI가 할당되지 않았습니다. UI를 설정해주세요.");
            return;
        }

        // 버튼 클릭 이벤트 등록
        htpButton.onClick.AddListener(OnClickSettingButton);
        // Return 버튼 클릭 이벤트 등록
        returnButton.onClick.AddListener(OnClickReturnButton);

        // 초기 로비 UI 활성화
        lobbyUI.SetActive(true);
        htpUI.SetActive(false);
    }

    private void OnClickSettingButton()
    {
        // if (SoundManager.instance != null)
        //     SoundManager.instance.PlayClickSound();
        // 로비 UI 비활성화, 설정 UI 활성화
        lobbyUI.SetActive(false);
        htpUI.SetActive(true);

        // Return 버튼 활성화
        returnButton.gameObject.SetActive(true);
        htpButton.gameObject.SetActive(false);
    }

    private void OnClickReturnButton()
    {
        // if (SoundManager.instance != null)
        //     SoundManager.instance.PlayClickSound();
        // 설정 UI 비활성화, 로비 UI 활성화
        htpUI.SetActive(false);
        lobbyUI.SetActive(true);

        // Return 버튼 비활성화, Setting 버튼 활성화
        returnButton.gameObject.SetActive(false);
        htpButton.gameObject.SetActive(true);
    }
}