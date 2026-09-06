using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BossHPFlashUI : MonoBehaviour
{
    [System.Serializable]
    public class HeartSlot
    {
        public Transform slotTransform; // 각 칸 위치(네가 말한 Transform)
        public Image heartImage;        // 하트 이미지(풀 하트 스프라이트를 기본으로 넣어두는 걸 추천)
    }

    [Header("Slots (총 5칸 등 고정)")]
    [SerializeField] private HeartSlot[] slots;

    [Header("Timing")]
    [SerializeField] private float showSeconds = 1.0f;
    [SerializeField] private float blinkInterval = 0.12f;

    [Header("CanvasGroup (권장)")]
    [SerializeField] private CanvasGroup canvasGroup;

    private Coroutine _co;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // 시작 시 전부 꺼둠
        SetAllSlotsActive(false);
    }

    /// <summary>
    /// 보스 체력을 특정 UI 위치에서 점멸 표시
    /// </summary>
    public void Show(int currentHp, int maxHp)
    {
        if (slots == null || slots.Length == 0) return;
        if (maxHp <= 0) return;

        int totalSlots = slots.Length;
        maxHp = Mathf.Clamp(maxHp, 1, totalSlots);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        // 중앙 정렬: maxHp만큼 가운데에 배치
        // 예: total=5, maxHp=3 -> start=1 => 1,2,3 사용 (0-index)
        // total=5, maxHp=5 -> start=0 => 0..4 사용
        int startIndex = (totalSlots - maxHp) / 2; // 5-3=2 -> 1

        // 슬롯 활성/비활성 및 하트 표시
        for (int i = 0; i < totalSlots; i++)
        {
            bool inUse = (i >= startIndex) && (i < startIndex + maxHp);

            if (slots[i].slotTransform != null)
                slots[i].slotTransform.gameObject.SetActive(inUse);

            if (slots[i].heartImage != null)
            {
                // 빈 하트 스프라이트 안 쓰고 "빈칸" 처리: 남은 체력만 enabled=true
                if (!inUse)
                {
                    slots[i].heartImage.enabled = false;
                }
                else
                {
                    int localIndex = i - startIndex; // 0..maxHp-1
                    bool filled = localIndex < currentHp;
                    slots[i].heartImage.enabled = filled;
                }
            }
        }

        // 연타 대비 코루틴 리셋
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Co_Blink());
    }

    /// <summary>
    /// 보스 등장 시 한번 보여주고(점멸하거나), 그냥 잠깐 표시만 하고 싶을 때 사용
    /// </summary>
    public void ShowOnSpawn(int currentHp, int maxHp)
    {
        // 등장 연출도 동일하게 1초 점멸로 보여주려면 그냥 Show 호출이면 끝
        Show(currentHp, maxHp);
    }

    private IEnumerator Co_Blink()
    {
        float t = 0f;
        float blinkT = 0f;
        bool on = true;

        canvasGroup.alpha = 1f;

        while (t < showSeconds)
        {
            float dt = Time.deltaTime;
            t += dt;
            blinkT += dt;

            if (blinkT >= blinkInterval)
            {
                blinkT = 0f;
                on = !on;
                canvasGroup.alpha = on ? 1f : 0f;
            }

            yield return null;
        }

        canvasGroup.alpha = 0f;
        _co = null;

        // 표시가 끝나면 슬롯도 꺼두고 싶으면 아래 줄 활성화
        // SetAllSlotsActive(false);
    }

    private void SetAllSlotsActive(bool active)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].slotTransform != null)
                slots[i].slotTransform.gameObject.SetActive(active);

            if (slots[i].heartImage != null)
                slots[i].heartImage.enabled = false;
        }
    }
}
