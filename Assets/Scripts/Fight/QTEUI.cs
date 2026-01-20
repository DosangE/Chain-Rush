using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QTEUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Slots (Grid)")]
    [SerializeField] private Transform slotsRoot;
    [SerializeField] private GameObject slotPrefab;

    [Header("Timer Bar (Right -> Left)")]
    [Tooltip("Image Type=Filled, Fill Method=Horizontal, Fill Origin=Right 로 설정된 BarFill Image")]
    [SerializeField] private Image totalTimeBarFill;

    [Tooltip("원하면 숫자도 같이 표시(TMP). 없어도 됨.")]
    [SerializeField] private TMP_Text totalTimeTextOptional;

    [Header("Timer Bar Color FX")]
    [SerializeField] private Color safeColor = Color.white;
    [SerializeField] private Color dangerColor = Color.red;

    [Tooltip("시간 절반(50%)에서 1초 주기(1Hz)")]
    [SerializeField] private float halfTimeFlashHz = 1f;

    [Tooltip("0초 임박에서 0.5초 주기(2Hz)")]
    [SerializeField] private float zeroTimeFlashHz = 2f;

    [Tooltip("0이면 기본 sin 그대로, 1이면 더 완만(부드러움)")]
    [SerializeField, Range(0f, 1f)] private float smoothness = 0.6f;

    [Header("InGameUI")]
    [SerializeField] private PopupTextUI InGamePopupUI;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private class SlotView
    {
        public GameObject go;
        public Graphic[] graphics;   // Image/Text/TMP 등
        public TMP_Text tmp;
        public Text legacyText;

        public void SetLabel(string s)
        {
            if (tmp != null) tmp.text = s;
            if (legacyText != null) legacyText.text = s;
        }

        public void SetVisible(bool on)
        {
            // GameObject는 끄지 않는다(그리드 자리 고정)
            if (graphics != null)
            {
                for (int i = 0; i < graphics.Length; i++)
                {
                    if (graphics[i] != null) graphics[i].enabled = on;
                }
            }
        }
    }

    private readonly List<SlotView> _slots = new List<SlotView>();
    private int _nextHideIndex = 0;

    private void Awake()
    {
        if (root == null) root = gameObject;
    }

    public void Show(BossQTE qte)
    {
        if (root != null) root.SetActive(true);

        BuildSlotsIfNeeded(qte);
        ResetSlotsVisual(qte);
        UpdateTotalTimeBar(qte);

        if (debugLog) Debug.Log("[QTEUI] Show");
    }

    public void Hide(bool judge)
    {
        if (root != null) root.SetActive(false);
        
        if (InGamePopupUI != null && judge)
            InGamePopupUI.Play("Attack Boss!");
        else if (!judge)
            InGamePopupUI.Play("QTE Failed!");

        if (debugLog) Debug.Log("[QTEUI] Hide");
    }

    public void Tick(BossQTE qte)
    {
        UpdateTotalTimeBar(qte);
    }

    // 정답 입력: 앞칸을 "빈칸"으로 (자리 고정)
    public void OnCorrectPopFront(BossQTE qte)
    {
        if (_nextHideIndex < _slots.Count)
        {
            _slots[_nextHideIndex].SetVisible(false);
            _nextHideIndex++;
        }
    }

    // 오답: 전부 다시 보이게 + 라벨 복구
    public void OnWrongReset(BossQTE qte)
    {
        ResetSlotsVisual(qte);
        UpdateTotalTimeBar(qte);
    }

    private void BuildSlotsIfNeeded(BossQTE qte)
    {
        if (slotsRoot == null || slotPrefab == null)
        {
            Debug.LogError("[QTEUI] slotsRoot or slotPrefab is null.");
            return;
        }

        if (_slots.Count == qte.Pattern.Count) return;

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].go != null) Destroy(_slots[i].go);
        }
        _slots.Clear();

        for (int i = 0; i < qte.Pattern.Count; i++)
        {
            GameObject slotGO = Instantiate(slotPrefab, slotsRoot);

            var view = new SlotView();
            view.go = slotGO;

            // 슬롯 내부의 모든 Graphic을 on/off 해서 빈칸화
            view.graphics = slotGO.GetComponentsInChildren<Graphic>(true);

            // 라벨 텍스트(TMP 우선)
            view.tmp = slotGO.GetComponentInChildren<TMP_Text>(true);
            if (view.tmp == null) view.legacyText = slotGO.GetComponentInChildren<Text>(true);

            _slots.Add(view);
        }
    }

    private void ResetSlotsVisual(BossQTE qte)
    {
        _nextHideIndex = 0;

        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot == null || slot.go == null) continue;

            slot.SetVisible(true);
            slot.SetLabel(KeyToShortString(qte.Pattern[i]));
        }
    }

    // ===== Timer Bar 업데이트 (우->좌 감소 + 임박 색 효과) =====
    private void UpdateTotalTimeBar(BossQTE qte)
    {
        if (totalTimeBarFill == null) return;

        float limit = qte.TotalTimeLimit;
        float remain = qte.GetTotalTimeRemaining();

        // fillAmount 계산
        float ratio;
        if (limit <= 0f)
        {
            ratio = 1f;
        }
        else
        {
            ratio = Mathf.Clamp01(remain / limit);
        }

        totalTimeBarFill.fillAmount = ratio;

        // 색 임박 효과
        if (limit <= 0f)
        {
            totalTimeBarFill.color = safeColor;
        }
        else if (ratio > 0.5f)
        {
            // 절반 초과: 안전색 고정
            totalTimeBarFill.color = safeColor;
        }
        else
        {
            // 절반 이하부터: 1Hz -> 2Hz로 점점 빨라짐
            float t = Mathf.InverseLerp(0.5f, 0f, ratio); // 0(50%) ~ 1(0%)
            float hz = Mathf.Lerp(halfTimeFlashHz, zeroTimeFlashHz, t);

            // 부드러운 깜빡임(0~1)
            float s = Mathf.Sin(Time.unscaledTime * hz * Mathf.PI * 2f) * 0.5f + 0.5f;

            // 더 부드럽게(중간을 길게/완만하게)
            float eased = Mathf.SmoothStep(0f, 1f, s);
            float blend = Mathf.Lerp(s, eased, smoothness);

            totalTimeBarFill.color = Color.Lerp(safeColor, dangerColor, blend);
        }

        // (선택) 숫자 표시
        if (totalTimeTextOptional != null)
        {
            if (limit <= 0f) totalTimeTextOptional.text = "∞";
            else totalTimeTextOptional.text = remain.ToString("0.00");
        }
    }

    private string KeyToShortString(KeyCode k)
    {
        return k.ToString().ToUpperInvariant();
    }
}
