using UnityEngine;
using UnityEngine.UI;

public class HowToPopupUI : MonoBehaviour
{
    [Header("Popup Root (this)")]
    [SerializeField] private GameObject popupRoot;   // HowToPopup 패널(보통 자기 자신)

    [Header("Pages (size must be 3)")]
    [SerializeField] private GameObject[] pages;     // Page1, Page2, Page3 순서대로

    [Header("Buttons")]
    [SerializeField] private Button btnPrev;         // "<"
    [SerializeField] private Button btnNext;         // ">"
    [SerializeField] private Button btnClose;        // "X"

    private int pageIndex = 0;

    private void Reset()
    {
        popupRoot = gameObject;
    }

    private void Awake()
    {
        // 버튼 이벤트 연결
        if (btnPrev != null) btnPrev.onClick.AddListener(PrevPage);
        if (btnNext != null) btnNext.onClick.AddListener(NextPage);
        if (btnClose != null) btnClose.onClick.AddListener(Close);

        // 페이지 상태만 초기화 (여기서 popupRoot를 끄지 않음!)
        pageIndex = 0;
        ApplyPage();
    }

    /// <summary>
    /// 타이틀씬의 "게임 방법" 버튼에서 호출
    /// </summary>
    public void Open()
    {
        pageIndex = 0;
        ApplyPage();

        if (popupRoot != null && !popupRoot.activeSelf)
            popupRoot.SetActive(true);
    }

    public void Close()
    {
        if (popupRoot != null && popupRoot.activeSelf)
            popupRoot.SetActive(false);
    }

    private void NextPage()
    {
        if (pages == null || pages.Length == 0) return;

        int last = pages.Length - 1;
        if (pageIndex >= last) return;

        pageIndex++;
        ApplyPage();
    }

    private void PrevPage()
    {
        if (pages == null || pages.Length == 0) return;
        if (pageIndex <= 0) return;

        pageIndex--;
        ApplyPage();
    }

    private void ApplyPage()
    {
        if (pages == null || pages.Length == 0) return;

        if (pageIndex < 0) pageIndex = 0;
        if (pageIndex > pages.Length - 1) pageIndex = pages.Length - 1;

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null)
                pages[i].SetActive(i == pageIndex);
        }

        int last = pages.Length - 1;
        if (btnPrev != null) btnPrev.interactable = pageIndex > 0;
        if (btnNext != null) btnNext.interactable = pageIndex < last;
    }
}
