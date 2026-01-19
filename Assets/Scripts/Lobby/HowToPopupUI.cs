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
        // 실수 방지용: popupRoot를 자동으로 자기 자신으로
        popupRoot = gameObject;
    }

    private void Awake()
    {
        // 버튼 이벤트 연결
        if (btnPrev != null) btnPrev.onClick.AddListener(PrevPage);
        if (btnNext != null) btnNext.onClick.AddListener(NextPage);
        if (btnClose != null) btnClose.onClick.AddListener(Close);

        // 시작 상태: 닫힌 상태 + 1페이지로 초기화
        pageIndex = 0;
        ApplyPage();
        if (popupRoot != null) popupRoot.SetActive(false);
    }

    /// <summary>
    /// 타이틀씬의 "게임 방법" 버튼에서 호출
    /// </summary>
    public void Open()
    {
        pageIndex = 0;
        ApplyPage();
        if (popupRoot != null) popupRoot.SetActive(true);
    }

    public void Close()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
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

        // 안전 처리: 범위 클램프
        if (pageIndex < 0) pageIndex = 0;
        if (pageIndex > pages.Length - 1) pageIndex = pages.Length - 1;

        // 페이지 on/off
        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null)
                pages[i].SetActive(i == pageIndex);
        }

        // 버튼 활성/비활성 규칙
        int last = pages.Length - 1;

        if (btnPrev != null) btnPrev.interactable = pageIndex > 0;      // 첫 페이지면 비활성
        if (btnNext != null) btnNext.interactable = pageIndex < last;   // 마지막 페이지면 비활성
    }
}
