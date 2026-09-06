using UnityEngine;

/// <summary>
/// Visual 오브젝트를 상하로만 부드럽게 흔들어 "비행" 느낌을 주는 스크립트.
/// BossRoot(로직/콜라이더) 아래 Visual(스프라이트)에 붙이는 걸 권장.
/// </summary>
public class FloatingBob : MonoBehaviour
{
    [Header("Bob Settings")]
    [Tooltip("상하 이동 거리(유닛). 0.05~0.25 정도 추천")]
    [SerializeField] private float amplitude = 0.12f;

    [Tooltip("상하 왕복 속도(Hz). 0.6~2.0 정도 추천")]
    [SerializeField] private float frequency = 0.6f;

    [Tooltip("시작 위상 랜덤(여러 보스/부품이 동시에 움직이는 느낌 방지)")]
    [SerializeField] private bool randomizePhase = true;

    [Header("Pause/Disable Handling")]
    [Tooltip("이 오브젝트가 비활성/재활성화될 때 기준 위치를 다시 잡을지")]
    [SerializeField] private bool recacheBaseOnEnable = true;

    private Vector3 baseLocalPos;
    private float phase;

    private void Awake()
    {
        baseLocalPos = transform.localPosition;
        phase = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
    }

    private void OnEnable()
    {
        if (recacheBaseOnEnable)
            baseLocalPos = transform.localPosition;
    }

    private void Update()
    {
        // Time.timeScale=0(일시정지)면 움직임도 멈추는 게 자연스러우면 Time.time 그대로 사용.
        // 일시정지 중에도 떠있게 하려면 Time.unscaledTime로 바꾸면 됨.
        float t = Time.time;
        float yOffset = Mathf.Sin((t * frequency * Mathf.PI * 2f) + phase) * amplitude;

        transform.localPosition = baseLocalPos + new Vector3(0f, yOffset, 0f);
    }

    /// <summary>외부에서 강제로 기준 위치를 재설정하고 싶을 때</summary>
    public void RecacheBasePosition()
    {
        baseLocalPos = transform.localPosition;
    }

    /// <summary>게임 중 세팅 변경용</summary>
    public void SetBob(float newAmplitude, float newFrequency)
    {
        amplitude = Mathf.Max(0f, newAmplitude);
        frequency = Mathf.Max(0f, newFrequency);
    }
}
