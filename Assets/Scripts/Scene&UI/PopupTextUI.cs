using UnityEngine;
using TMPro;

public class PopupTextUI : MonoBehaviour
{
    [Header("Required")]
    [SerializeField] private TMP_Text textUI;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Animator animator;

    [Header("Animator")]
    [SerializeField] private string showTriggerName = "Show";
    [SerializeField] private string showStateName = "Show"; // Animator State 이름과 동일하게

    private int showTriggerHash;
    private bool initialized;

    private void Awake()
    {
        InitIfNeeded();
        HideImmediate(); // 시작 시 안 보이게
    }

    private void OnEnable()
    {
        // 씬 로드/재활성화 때 깜빡임 방지
        InitIfNeeded();
        HideImmediate();
    }

    private void InitIfNeeded()
    {
        if (initialized) return;
        initialized = true;

        if (textUI == null) textUI = GetComponentInChildren<TMP_Text>(true);
        if (canvasGroup == null) canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        if (animator == null) animator = GetComponentInChildren<Animator>(true);

        showTriggerHash = Animator.StringToHash(showTriggerName);
    }
    public void Play(string message = null)
    {
        InitIfNeeded();

        if (textUI == null || canvasGroup == null || animator == null)
        {
            Debug.LogError("[PopupTextUI] Missing references. Assign TMP_Text / CanvasGroup / Animator.");
            return;
        }

        if (!string.IsNullOrEmpty(message))
            textUI.text = message;

        // 보이도록 확실히 열어두고(깜빡임/상태 꼬임 방지)
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        // 연타 시에도 항상 "처음부터" 재생되게 강제
        animator.ResetTrigger(showTriggerHash);
        animator.SetTrigger(showTriggerHash);

        // 상태가 Show로 넘어가며 첫 프레임 반영(가끔 트리거 씹힘 방지)
        animator.Play(showStateName, 0, 0f);
        animator.Update(0f);
    }


    public void HideImmediate()
    {
        InitIfNeeded();
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }
}
