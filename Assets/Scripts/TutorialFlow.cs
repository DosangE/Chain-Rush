using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class TutorialFlow : MonoBehaviour
{
    public enum GoalType
    {
        GrappleSuccess,
        EnemyHit,
        BossQteSuccess,
        BossHitAfterQte
    }

    [System.Serializable]
    public class Step
    {
        [Header("UI")]
        public string title;
        [TextArea] public string message;

        [Header("Map Patterns (ONLY these)")]
        public List<GameObject> onlyPatterns = new List<GameObject>();

        [Header("Boss Spawn")]
        public bool allowBossSpawn = false;

        [Header("Goal")]
        public GoalType goal = GoalType.GrappleSuccess;
        public int requiredCount = 1;

        [Header("Optional speed lock")]
        public bool forceMapSpeed = true;
        public float forcedMapSpeed = 15f;
    }

    [Header("Steps")]
    [SerializeField] private Step[] steps;

    [Header("Refs")]
    [SerializeField] private MapManager mapManager;

    [Header("Optional Locks")]
    [SerializeField] private MonoBehaviour[] disableDuringStep; // 필요하면 단계 시작 때 끄고, 끝나면 다시 켬
    [SerializeField] private MonoBehaviour[] enableOnlyWhenAllowed; // 필요하면 단계별로 켬/끔(아래 ApplyLocks 참고)

    [Header("UI Events (Optional)")]
    public UnityEvent<string> OnStepTitle;
    public UnityEvent<string> OnStepMessage;
    public UnityEvent<int, int> OnStepProgress; // (current, required)

    [Header("Popup UI")]
    [SerializeField] private TutorialPopupUI popupUI;
    [SerializeField] private bool pauseByTimescale = true;

    [Header("HUD UI")]
    [SerializeField] private TutorialHUDUI hudUI;
    private readonly Dictionary<MonoBehaviour, bool> _originalEnabled = new Dictionary<MonoBehaviour, bool>();

    private bool waitingPopupClose = false;

    private int stepIndex = -1;
    private int progress = 0;
    public static bool IsInputLocked { get; private set; } = false;

    private void Start()
    {
        if (mapManager == null) mapManager = FindObjectOfType<MapManager>();

        if (popupUI == null) popupUI = FindObjectOfType<TutorialPopupUI>(true);
        if (popupUI != null) popupUI.Bind(this);
        if (hudUI == null)
            hudUI = FindObjectOfType<TutorialHUDUI>(true);

        if (hudUI != null) hudUI.Show();

        CacheOriginalEnabledStates();   // ✅ 추가
        GoNextStep();
    }

    private void GoNextStep()
    {
        stepIndex++;
        progress = 0;

        if (steps == null || steps.Length == 0)
        {
            Debug.LogError("[TutorialFlow] steps is empty.");
            return;
        }

        if (stepIndex >= steps.Length)
        {
            FinishTutorial();
            return;
        }

        Step step = steps[stepIndex];

        if (hudUI != null)
        {
            // "키 안내 + 목표"를 한 줄로 구성 (원하는 문구로 바꾸면 됨)
            hudUI.SetHint(step.message); // message를 짧게 써도 되고
            hudUI.SetProgress(0, Mathf.Max(1, step.requiredCount));
        }

        // UI 이벤트는 일단 쏘고
        OnStepTitle?.Invoke(step.title);
        OnStepMessage?.Invoke(step.message);
        OnStepProgress?.Invoke(progress, Mathf.Max(1, step.requiredCount));

        // ✅ 팝업을 띄우고 "닫기 전까지는 단계 적용을 보류"
        waitingPopupClose = true;

        if (pauseByTimescale)
        {
            Time.timeScale = 0f;
            IsInputLocked = true;
        }

        if (popupUI != null)
        {
            popupUI.Show(step.title, step.message);
            hudUI?.Hide();
        }
        else
        {
            // 팝업 UI가 없으면 자동 진행(테스트 편의)
            ResumeFromPopup();
        }
    }

    private void CacheOriginalEnabledStates()
    {
        _originalEnabled.Clear();

        void CacheArray(MonoBehaviour[] arr)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
            {
                var mb = arr[i];
                if (mb == null) continue;
                if (_originalEnabled.ContainsKey(mb)) continue;
                _originalEnabled.Add(mb, mb.enabled);
            }
        }

        CacheArray(disableDuringStep);
        CacheArray(enableOnlyWhenAllowed);
    }

    private void RestoreOriginalEnabledStates()
    {
        foreach (var kv in _originalEnabled)
        {
            if (kv.Key != null)
                kv.Key.enabled = kv.Value;
        }
    }
    private void ApplyLocksForStep(GoalType goal)
    {
        // 여기 부분은 네 프로젝트에 맞춰 "공격 막기/허용" 같은 걸 넣으면 됨
        // 지금은 최소 형태로: disableDuringStep는 항상 끄고, enableOnlyWhenAllowed는 goal에 따라 켬
        if (disableDuringStep != null)
        {
            for (int i = 0; i < disableDuringStep.Length; i++)
                if (disableDuringStep[i] != null) disableDuringStep[i].enabled = false;
        }

        if (enableOnlyWhenAllowed != null)
        {
            // 기본은 전부 끔
            for (int i = 0; i < enableOnlyWhenAllowed.Length; i++)
                if (enableOnlyWhenAllowed[i] != null) enableOnlyWhenAllowed[i].enabled = false;

            // 예시: EnemyHit 단계나 BossHitAfterQte 단계에서만 공격 컴포넌트를 켜고 싶다
            bool allowAttack = (goal == GoalType.EnemyHit || goal == GoalType.BossHitAfterQte);

            if (allowAttack)
            {
                for (int i = 0; i < enableOnlyWhenAllowed.Length; i++)
                    if (enableOnlyWhenAllowed[i] != null) enableOnlyWhenAllowed[i].enabled = true;
            }
        }
    }

    private void AddProgress(int amount)
    {
        if (stepIndex < 0 || stepIndex >= steps.Length) return;

        Step step = steps[stepIndex];
        int req = Mathf.Max(1, step.requiredCount);

        progress += amount;
        if (hudUI != null)
            hudUI.SetProgress(progress, Mathf.Max(1, step.requiredCount));

        OnStepProgress?.Invoke(progress, req);

        if (progress >= req)
        {
            GoNextStep();
        }
    }

    private bool IsCurrentGoal(GoalType g)
    {
        if (steps == null || stepIndex < 0 || stepIndex >= steps.Length) return false;
        return steps[stepIndex].goal == g;
    }

    private void FinishTutorial()
    {
        hudUI?.Hide();

        if (mapManager != null)
            mapManager.Tutorial_ClearOverride();

        RestoreOriginalEnabledStates(); // ✅ 튜토로 꺼둔 컴포넌트 전부 원복
        ResetGlobalLocks();             // ✅ timeScale/IsInputLocked 원복(안전)

        Time.timeScale = 0f;


        if (popupUI != null)
            popupUI.ShowFinish("Tutorial Complete", "이제 어디로 갈까요?");
    }


    // =========================================================
    // 외부에서 호출할 "성공 보고" API
    // =========================================================
    public void ReportGrappleSuccess()
    {
        if (!IsCurrentGoal(GoalType.GrappleSuccess)) return;
        AddProgress(1);
    }

    public void ReportEnemyHit()
    {
        if (!IsCurrentGoal(GoalType.EnemyHit)) return;
        AddProgress(1);
    }

    public void ReportBossQteSuccess()
    {
        if (!IsCurrentGoal(GoalType.BossQteSuccess)) return;
        AddProgress(1);
    }

    public void ReportBossHitAfterQte()
    {
        if (!IsCurrentGoal(GoalType.BossHitAfterQte)) return;
        AddProgress(1);
    }

    public void ResumeFromPopup()
    {
        if (!waitingPopupClose) return;
        waitingPopupClose = false;

        if (pauseByTimescale)
        {
            Time.timeScale = 1f;
            IsInputLocked = false;   // ✅ 잠금 해제
        }

        hudUI?.Show();

        ApplyCurrentStep();
    }

    private void ApplyCurrentStep()
    {
        if (stepIndex < 0 || stepIndex >= steps.Length) return;

        Step step = steps[stepIndex];

        // Map override (리셋 없이 다음 스폰부터 적용)
        if (mapManager != null)
        {
            mapManager.Tutorial_SetOnlyPatterns(step.onlyPatterns, step.allowBossSpawn);

            if (step.forceMapSpeed)
                mapManager.Tutorial_ForceMapSpeed(step.forcedMapSpeed);
        }

        ApplyLocksForStep(step.goal);
    }

    public void OnClickRobby()
    {
        ResetGlobalLocks();

        SceneManager.LoadScene("TitleScene");
    }


    public void OnClickGameStart()
    {
        ResetGlobalLocks();

        SceneManager.LoadScene("InGameScene");
    }


    private void OnDestroy()
    {
        // 씬 전환/종료 시 전역값 원복
        IsInputLocked = false;
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    private void OnDisable()
    {
        // 오브젝트 비활성화로 씬이 넘어가는 케이스도 커버
        IsInputLocked = false;
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }
    private void ResetGlobalLocks()
    {
        IsInputLocked = false;
        Time.timeScale = 1f;
        PlayerActionLock.ForceUnlock();   // ✅ 이 줄 추가

    }
    public void ResumeFromFinish()
    {
        RestoreOriginalEnabledStates(); // ✅
        ResetGlobalLocks();   // Time.timeScale=1f 포함
        hudUI?.Show();
        if (popupUI != null) popupUI.HideImmediate();
    }

}
