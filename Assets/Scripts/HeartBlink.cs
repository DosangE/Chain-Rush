using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class HeartBlink : MonoBehaviour
{
    [SerializeField] private Sprite frameA;
    [SerializeField] private Sprite frameB;

    [Tooltip("프레임 전환 간격(초)")]
    [SerializeField] private float interval = 0.5f;

    // 모든 HeartBlink가 공유하는 기준 시각(동기화용)
    private static float s_startTime = -1f;

    private Image img;
    private int lastPhase = -1;

    private void Awake()
    {
        img = GetComponent<Image>();

        // 첫 HeartBlink가 기준 시각을 잡음 (씬 시작 시 동기화)
        if (s_startTime < 0f)
            s_startTime = Time.unscaledTime;

        ApplyByGlobalPhase(force: true);
    }

    private void OnEnable()
    {
        // 재활성화되어도 "지금 전역 위상"에 맞는 프레임으로 즉시 표시
        ApplyByGlobalPhase(force: true);
    }

    private void Update()
    {
        ApplyByGlobalPhase(force: false);
    }

    private void ApplyByGlobalPhase(bool force)
    {
        if (img == null) return;
        if (interval <= 0f) interval = 0.5f;

        float t = Time.unscaledTime - s_startTime;
        int phase = (int)Mathf.Floor(t / interval) & 1; // 0 또는 1

        if (!force && phase == lastPhase) return;

        lastPhase = phase;
        img.sprite = (phase == 0) ? frameA : frameB;
    }

    public static void RestartGlobalBlinkPhase()
    {
        s_startTime = Time.unscaledTime;
    }

    public void ResetBlink()
    {
        ApplyByGlobalPhase(force: true);
    }

    public void SetFrames(Sprite a, Sprite b)
    {
        frameA = a;
        frameB = b;
        ApplyByGlobalPhase(force: true);
    }
}
