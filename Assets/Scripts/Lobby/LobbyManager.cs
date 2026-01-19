using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    [Header("HowToPlay Popup")]
    [SerializeField] private HowToPopupUI howToPopup; // ★ 변경

    [Header("HowToPlay Button")]
    [SerializeField] private Button htpButton;

    private void Awake()
    {
        if (howToPopup == null || htpButton == null)
        {
            Debug.LogError("HowToPopup 또는 버튼이 할당되지 않았습니다.");
            return;
        }

        htpButton.onClick.AddListener(OnClickHowToPlay);
    }

    private void OnClickHowToPlay()
    {
        howToPopup.Open();
    }
}