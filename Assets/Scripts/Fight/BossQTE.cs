using System.Collections.Generic;
using UnityEngine;

public class BossQTE : MonoBehaviour
{
    [Header("Keys Pool (qwerasdf)")]
    [SerializeField]
    private KeyCode[] keyPool = new KeyCode[]
    {
        KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R,
        KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F
    };

    [Header("Pattern")]
    [SerializeField] private int patternLength = 10;

    [Header("Time Limit (Unscaled)")]
    [Tooltip("전체 제한시간(초). 0 이하이면 시간제한 없음")]
    [SerializeField] private float totalTimeLimit = 6.0f;

    [Tooltip("각 입력 사이 제한시간(초). 0 이하이면 사용 안함")]
    [SerializeField] private float perKeyTimeLimit = 1.2f;

    [Header("UI")]
    [SerializeField] private QTEUI ui;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    [Header("SFX")]
    [SerializeField] private AudioClip HitSFX;

    public void PlayHitSFX() => PlaySFX(HitSFX);

    public bool IsRunning { get; private set; }
    public bool WasSuccess { get; private set; }

    public IReadOnlyList<KeyCode> Pattern => _pattern;
    public int SolvedCount => _index;
    public int RemainingCount => Mathf.Max(0, _pattern.Count - _index);

    // ✅ UI/로직이 참조하는 "실제 제한시간"
    public float TotalTimeLimit => totalTimeLimit;
    public float PerKeyTimeLimit => perKeyTimeLimit;

    private readonly List<KeyCode> _pattern = new List<KeyCode>();
    private int _index;

    private float _startTimeUnscaled;
    private float _lastInputTimeUnscaled;

    private void Awake()
    {
        if (ui == null) ui = FindObjectOfType<QTEUI>(true);
    }

    public void Begin()
    {
        WasSuccess = false;
        IsRunning = true;

        if (ui == null)
            ui = FindObjectOfType<QTEUI>(true);

        GeneratePattern();
        ResetProgressOnly();

        _startTimeUnscaled = Time.unscaledTime;
        _lastInputTimeUnscaled = Time.unscaledTime;

        if (debugLog)
            Debug.Log($"[BossQTE] Begin totalTimeLimit={totalTimeLimit}, perKeyTimeLimit={perKeyTimeLimit}, patternLen={patternLength}");

        if (ui != null) ui.Show(this);
    }

    private void Update()
    {
        if (!IsRunning) return;

        float now = Time.unscaledTime;

        // 전체 시간 제한 체크
        if (totalTimeLimit > 0f && now - _startTimeUnscaled > totalTimeLimit)
        {
            PlayHitSFX();
            if (debugLog) Debug.Log("[BossQTE] FAIL (total time limit)");
            Fail();
            return;
        }

        // 풀 키 중 하나가 눌리면 처리
        for (int i = 0; i < keyPool.Length; i++)
        {
            var k = keyPool[i];
            if (Input.GetKeyDown(k))
            {
                HandleKeyDown(k, now);
                break;
            }
        }

        if (ui != null) ui.Tick(this);
    }

    private void HandleKeyDown(KeyCode pressed, float nowUnscaled)
    {
        _lastInputTimeUnscaled = nowUnscaled;

        if (_index >= _pattern.Count) return;

        KeyCode expected = _pattern[_index];

        if (pressed == expected)
        {
            _index++;
            if (ui != null) ui.OnCorrectPopFront(this);

            if (debugLog) Debug.Log($"[BossQTE] Correct {pressed} ({_index}/{_pattern.Count})");

            if (_index >= _pattern.Count)
                Success();
        }
        else
        {
            if (debugLog) Debug.Log($"[BossQTE] Wrong {pressed} expected {expected} -> reset");
            ResetProgressOnly();
            GameManager.Instance.PlayWrongQTESFX();
            if (ui != null) ui.OnWrongReset(this);
        }
    }

    private void ResetProgressOnly()
    {
        _index = 0;
    }

    private void GeneratePattern()
    {
        _pattern.Clear();

        if (keyPool == null || keyPool.Length == 0)
        {
            Debug.LogError("[BossQTE] keyPool is empty.");
            return;
        }

        for (int i = 0; i < patternLength; i++)
        {
            int r = Random.Range(0, keyPool.Length);
            _pattern.Add(keyPool[r]);
        }
    }

    private void Success()
    {
        WasSuccess = true;
        IsRunning = false;

        if (debugLog) Debug.Log("[BossQTE] SUCCESS");

        /*
        보스 배리어 해제
        */

        if (ui != null) ui.Hide(true);
    }

    private void Fail()
    {
        WasSuccess = false;
        IsRunning = false;

        /*
        보스 레이저 공격
        */

        if (ui != null) ui.Hide(false);
    }

    public void Cancel()
    {
        if (!IsRunning) return;
        IsRunning = false;
        WasSuccess = false;
        if (ui != null) ui.Hide(false);
    }

    public float GetTotalTimeRemaining()
    {
        if (totalTimeLimit <= 0f) return float.PositiveInfinity;
        float remain = totalTimeLimit - (Time.unscaledTime - _startTimeUnscaled);
        return Mathf.Max(0f, remain);
    }

    public float GetPerKeyTimeRemaining()
    {
        if (perKeyTimeLimit <= 0f) return float.PositiveInfinity;
        float remain = perKeyTimeLimit - (Time.unscaledTime - _lastInputTimeUnscaled);
        return Mathf.Max(0f, remain);
    }
    private void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        if (SoundManager.instance == null) return;   // TitleScene에서 생성 안 됐으면 null 가능
        SoundManager.instance.PlaySFX(clip);         // 네 SoundManager 함수 그대로 사용
    }
}
