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

    [Header("Timer Bar")]
    [Tooltip("항상 보이는 바(베이스). 검은색/프레임 이미지 등. Type=Simple(or Sliced) 권장")]
    [SerializeField] private Image totalTimeBarBackground;

    [Tooltip("시간이 지날수록 '오른쪽부터 차오르는' 오버레이. Image Type=Filled, Fill Method=Horizontal, Fill Origin=Right")]
    [SerializeField] private Image totalTimeBarElapsedOverlay;

    [Tooltip("원하면 숫자도 같이 표시(TMP). 없어도 됨.")]
    [SerializeField] private TMP_Text totalTimeTextOptional;

    [Header("Timer Bar Color FX (Overlay에 적용)")]
    [SerializeField] private Color safeColor = Color.black;
    [SerializeField] private Color dangerColor = Color.red;

    [Tooltip("시간 절반(50%)에서 1초 주기(1Hz)")]
    [SerializeField] private float halfTimeFlashHz = 1f;

    [Tooltip("0초 임박에서 0.5초 주기(2Hz)")]
    [SerializeField] private float zeroTimeFlashHz = 2f;

    [Tooltip("0이면 기본 sin 그대로, 1이면 더 완만(부드러움)")]
    [SerializeField, Range(0f, 1f)] private float smoothness = 0.6f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private class SlotView
    {
        public GameObject go;
        public Graphic[] graphics;
        public TMP_Text tmp;
        public Text legacyText;

        public void SetLabel(string s)
        {
            if (tmp != null) tmp.text = s;
            if (legacyText != null) legacyText.text = s;
        }

        public void SetVisible(bool on)
        {
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
        if (debugLog) Debug.Log("[QTEUI] Hide");
    }

    public void Tick(BossQTE qte)
    {
        UpdateTotalTimeBar(qte);
    }

    public void OnCorrectPopFront(BossQTE qte)
    {
        if (_nextHideIndex < _slots.Count)
        {
            _slots[_nextHideIndex].SetVisible(false);
            _nextHideIndex++;
        }
    }

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
            view.graphics = slotGO.GetComponentsInChildren<Graphic>(true);

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

    // ===== Timer Bar 업데이트 =====
    // - 베이스(totalTimeBarBackground)는 고정
    // - 오버레이(totalTimeBarElapsedOverlay)가 "오른쪽부터 차오르게" (elapsed 증가)
    private void UpdateTotalTimeBar(BossQTE qte)
    {
        if (totalTimeBarElapsedOverlay == null) return;

        float limit = qte.TotalTimeLimit;
        float remain = qte.GetTotalTimeRemaining();

        float remainRatio;
        if (limit <= 0f) remainRatio = 1f;
        else remainRatio = Mathf.Clamp01(remain / limit);

        // 경과(Elapsed) = 1 - 남은비율
        float elapsedRatio = 1f - remainRatio;
        totalTimeBarElapsedOverlay.fillAmount = elapsedRatio;

        // 오버레이 색 효과(원하면 그대로, 싫으면 safeColor만 쓰면 됨)
        if (limit <= 0f)
        {
            totalTimeBarElapsedOverlay.color = safeColor;
        }
        // else if (remainRatio > 0.5f)
        // {
        //     totalTimeBarElapsedOverlay.color = safeColor;
        // }
        // else
        // {
        //     float t = Mathf.InverseLerp(0.5f, 0f, remainRatio); // 0(50%) ~ 1(0%)
        //     float hz = Mathf.Lerp(halfTimeFlashHz, zeroTimeFlashHz, t);

        //     float s = Mathf.Sin(Time.unscaledTime * hz * Mathf.PI * 2f) * 0.5f + 0.5f;
        //     float eased = Mathf.SmoothStep(0f, 1f, s);
        //     float blend = Mathf.Lerp(s, eased, smoothness);

        //     totalTimeBarElapsedOverlay.color = Color.Lerp(safeColor, dangerColor, blend);
        // }

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
