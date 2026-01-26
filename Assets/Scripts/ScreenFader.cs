using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("검은색 전체 화면 Image(Stretch). Raycast Target은 꺼두는 것을 권장.")]
    [SerializeField] private Image fadeImage;

    [Tooltip("FadeCanvas에 붙은 CanvasGroup(권장). 없으면 자동으로 추가 시도.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Defaults")]
    [SerializeField] private float defaultFadeOutDuration = 0.6f;
    [SerializeField] private float defaultFadeInDuration = 0.4f;

    [Tooltip("true면 페이드 중에만 입력을 막고, 평소엔 항상 클릭 통과")]
    [SerializeField] private bool blockInputOnlyDuringFade = true;

    private Coroutine running;

    private void Awake()
    {
        // CanvasGroup 자동 확보/추가
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // fadeImage 자동 확보(자식 포함)
        if (fadeImage == null)
            fadeImage = GetComponentInChildren<Image>(true);

        // 기본값: 화면은 투명, 입력은 통과
        canvasGroup.alpha = 0f;
        SetInputBlocking(false);

        // 가장 흔한 실수 방지: 이미지가 레이캐스트 먹지 않게
        if (fadeImage != null)
            fadeImage.raycastTarget = false;
    }

    // =========================
    // Public API
    // =========================

    /// <summary>즉시 알파 설정(입력 차단은 유지/해제 여부 선택)</summary>
    public void SetAlpha(float alpha, bool blockInput)
    {
        alpha = Mathf.Clamp01(alpha);
        canvasGroup.alpha = alpha;
        SetInputBlocking(blockInput);
    }

    /// <summary>외부에서 강제로 입력 차단/해제</summary>
    public void SetInputBlocking(bool block)
    {
        // CanvasGroup이 Raycast를 막는 주체가 되도록 통일
        canvasGroup.blocksRaycasts = block;
        canvasGroup.interactable = block;
    }

    /// <summary>기본 페이드아웃(검정으로)</summary>
    public void FadeOut(float duration = -1f, bool? blockInputOverride = null)
    {
        if (duration < 0f) duration = defaultFadeOutDuration;
        StartFade(targetAlpha: 1f, duration: duration, blockInputOverride: blockInputOverride);
    }

    /// <summary>기본 페이드인(투명으로)</summary>
    public void FadeIn(float duration = -1f, bool? blockInputOverride = null)
    {
        if (duration < 0f) duration = defaultFadeInDuration;
        StartFade(targetAlpha: 0f, duration: duration, blockInputOverride: blockInputOverride);
    }

    /// <summary>
    /// 검정으로 페이드아웃 후 콜백 실행(예: UI 켜기/씬 전환/카메라 줌 완료 후 등)
    /// </summary>
    public void FadeToBlackThen(System.Action onBlack, float fadeOutDuration = -1f, bool keepBlockingAfter = false)
    {
        if (fadeOutDuration < 0f) fadeOutDuration = defaultFadeOutDuration;
        StartRoutine(FadeToBlackThenRoutine(onBlack, fadeOutDuration, keepBlockingAfter));
    }

    /// <summary>
    /// 검정 → 콜백 → 투명(대표: 게임오버 UI 켠 뒤 다시 클릭 가능하게)
    /// </summary>
    public void FadeToBlackThenFadeIn(System.Action onBlack, float fadeOutDuration = -1f, float fadeInDuration = -1f)
    {
        if (fadeOutDuration < 0f) fadeOutDuration = defaultFadeOutDuration;
        if (fadeInDuration < 0f) fadeInDuration = defaultFadeInDuration;
        StartRoutine(FadeToBlackThenFadeInRoutine(onBlack, fadeOutDuration, fadeInDuration));
    }

    /// <summary>진행 중 페이드 중단</summary>
    public void StopFade(bool keepCurrentAlpha = true, bool allowInput = true)
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        if (allowInput)
            SetInputBlocking(false);

        if (!keepCurrentAlpha)
            canvasGroup.alpha = 0f;
    }

    // =========================
    // Internals
    // =========================

    private void StartFade(float targetAlpha, float duration, bool? blockInputOverride)
    {
        StartRoutine(FadeRoutine(targetAlpha, duration, blockInputOverride));
    }

    private void StartRoutine(IEnumerator routine)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(routine);
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration, bool? blockInputOverride)
    {
        duration = Mathf.Max(0.01f, duration);

        bool shouldBlock;
        if (blockInputOverride.HasValue)
            shouldBlock = blockInputOverride.Value;
        else
            shouldBlock = blockInputOnlyDuringFade;

        // 페이드 중 입력 차단(원하면)
        if (shouldBlock)
            SetInputBlocking(true);

        float startAlpha = canvasGroup.alpha;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, a);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        // 페이드 종료 후 입력 정책
        if (blockInputOverride.HasValue)
        {
            // override가 있으면 그대로 유지
            SetInputBlocking(blockInputOverride.Value);
        }
        else
        {
            // 기본은: 페이드 중만 막고 끝나면 통과
            if (blockInputOnlyDuringFade)
                SetInputBlocking(false);
        }

        running = null;
    }

    private IEnumerator FadeToBlackThenRoutine(System.Action onBlack, float fadeOutDuration, bool keepBlockingAfter)
    {
        // 검정으로
        yield return FadeRoutine(targetAlpha: 1f, duration: fadeOutDuration, blockInputOverride: true);

        // 검정 상태에서 콜백
        onBlack?.Invoke();

        // 검정 상태 유지 중 입력 막을지
        SetInputBlocking(keepBlockingAfter);

        running = null;
    }

    private IEnumerator FadeToBlackThenFadeInRoutine(System.Action onBlack, float fadeOutDuration, float fadeInDuration)
    {
        // 검정으로 (입력 차단)
        yield return FadeRoutine(targetAlpha: 1f, duration: fadeOutDuration, blockInputOverride: true);

        onBlack?.Invoke();

        // 다시 투명 (투명 끝나면 입력 통과)
        yield return FadeRoutine(targetAlpha: 0f, duration: fadeInDuration, blockInputOverride: null);

        running = null;
    }
}
