using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    [Header("Map Speed")]
    public float currentMapSpeed = 15f;
    public float baseMapSpeed = 15f;

    [Header("Speed Stages (3개로 사용 권장)")]
    [SerializeField] private float[] speedStages = { 15f, 19f, 23f };
    private int currentSpeedStage = 0;

    [Header("Background (Optional)")]
    public Transform[] bgs = new Transform[3];
    public float parallax = 0.3f;

    private float bgWidth;
    private float lastPlayerX;
    private List<Transform> activeBGs = new List<Transform>();

    [Header("Map Patterns (Chunk System)")]
    public List<GameObject> mapPatterns;
    public int maxChunks = 5;
    public Transform player;
    public float spawnDistanceAhead = 30f;

    private Queue<GameObject> chunks = new Queue<GameObject>();
    private Transform lastEndPoint;

    // ✅ chunks와 함께 "패턴 인덱스"도 동일한 순서로 추적
    private readonly Queue<int> chunkPatternIndices = new Queue<int>();
    private readonly HashSet<int> activePatternIndices = new HashSet<int>();

    // =========================
    // Boss Spawn
    // =========================
    [Header("Boss Spawn (3개로 사용 권장)")]
    [SerializeField] private GameObject[] bossPrefabs = new GameObject[3];
    [SerializeField] private Transform bossPos;

    [Tooltip("누적 청크 스폰 수(초기 청크 포함)가 이 값에 도달하면 보스 1회 소환")]
    [SerializeField] private int chunksBeforeBoss = 10;

    [Header("Boss QTE Trigger (by removed chunks)")]
    [Tooltip("보스 진입(=보스 소환) 이후, '삭제된 청크'가 이 개수만큼 누적되면 QTE 발생")]
    [SerializeField] private int removedChunksBeforeQTE = 3;

    [Tooltip("true면 QTE가 한 번만, false면 removedChunksBeforeQTE마다 반복 발생")]
    [SerializeField] private bool qteOnlyOnceAfterBossSpawn = false;

    [Header("Debug")]
    [SerializeField] private bool debugBossAndChunk = false;

    [Header("InGameUI")]
    [SerializeField] private PopupTextUI InGamePopupUI;

    // 보스 진행은 "개수"로만 관리
    [Header("Boss Progression")]
    [Tooltip("보스를 몇 번 처치해야 게임 클리어인지. (기본 3)")]
    [SerializeField] private int totalBossesToClear = 3;

    [SerializeField] private int bossesDefeated = 0;

    private int spawnedChunkCount = 0;             // 초기 청크 포함 누적 스폰 수
    private int spawnedChunkCountAtStageStart = 0; // 스테이지 시작 시점 누적 스폰 수

    private bool bossSpawned = false;
    private GameObject spawnedBossObj = null;
    private Boss spawnedBoss = null;

    // --- 삭제(지나감) 카운터 ---
    private int removedChunkCountTotal = 0;
    private int removedChunkCountAtBossSpawn = 0;
    private int removedChunkCountAtLastQTE = 0;
    private bool qteTriggeredOnce = false;

    // QTE 성공 후, 보스 타격권 소모 전까지는 QTE 재트리거 금지
    private bool waitUntilBossHitConsumed = false;
    private PlayerAttack cachedPlayerAttack = null;

    // ✅ NEW: 공격 중엔 배경 패럴럭스 멈추기
    private bool parallaxPausedByAttack = false;

    // =========================================================
    // Game Clear (연출용 추가)
    // =========================================================
    [Header("Game Clear")]
    [SerializeField] private GameObject gameClearUI;
    [SerializeField] private bool stopTimeScaleOnClear = false;
    private bool isGameCleared = false;

    [Header("Game Clear Sequence - UI Hide")]
    [Tooltip("클리어 연출 시작 시 숨길 UI 루트들(인게임 HUD, QTE, 체력바 등)")]
    [SerializeField] private GameObject[] uiRootsToHideOnClear;

    [Header("Game Clear Sequence - Disable Controls")]
    [Tooltip("클리어 연출 시작 시 비활성화할 컴포넌트들(플레이어 조작/공격/그래플 등)")]
    [SerializeField] private MonoBehaviour[] componentsToDisableOnClear;

    [Header("Game Clear Sequence - Camera")]
    [SerializeField] private Camera clearCamera;
    [Tooltip("줌/이동 연출 시간")]
    [SerializeField] private float clearZoomDuration = 0.9f;
    [Tooltip("클리어 시 목표 ortho size(작을수록 확대)")]
    [SerializeField] private float clearTargetOrthoSize = 3.5f;
    [Tooltip("보스 포커스 오프셋(보스 중심에서 약간 위로 등)")]
    [SerializeField] private Vector3 clearFocusOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Game Clear Sequence - Fade")]
    [Tooltip("검은 페이드용 CanvasGroup(알파 0->1)")]
    [SerializeField] private ScreenFader screenFader;
    [SerializeField] private float fadeOutDuration = 0.6f; // 이건 유지해도 됨(화면 페이드 시간)


    [Header("Game Clear Sequence - Timing")]
    [SerializeField] private float holdAfterFade = 0.15f;

    private Coroutine clearRoutine = null;

    void Start()
    {
        TryResolveRefs();
        HookAttackEvents();

        bossesDefeated = 0;
        ApplyStageFromBossCount(playFx: false);

        spawnedChunkCountAtStageStart = 0;

        if (mapPatterns != null && mapPatterns.Count > 0)
        {
            SpawnInitialChunk();
        }
        else
        {
            Debug.LogError("[MapManager] mapPatterns가 비어있습니다.");
        }

        SetupBackgrounds();

        if (clearCamera == null) clearCamera = Camera.main;
        if (screenFader != null)
            screenFader.SetAlpha(0f, blockInput: false); // 평소 클릭 통과
    }

    private void OnDestroy()
    {
        UnhookAttackEvents();
    }

    void Update()
    {
        if (isGameCleared) return;

        if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            return;

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            if (player == null) return;
        }

        // 청크 스폰/삭제
        if (lastEndPoint != null && player.position.x + spawnDistanceAhead > lastEndPoint.position.x)
        {
            SpawnNextChunk();
            RemoveOldChunk(); // ✅ 여기서 삭제 카운트 + QTE 트리거
        }

        // 배경 패럴럭스(선택): 플레이어 X 이동량 기반
        UpdateBackgroundParallaxByPlayer();

        // 배경 무한루프(선택)
        if (activeBGs.Count > 0 && bgWidth > 0.001f)
            UpdateBackgroundCycling();
    }

    // =========================================================
    // ✅ PlayerAttack 이벤트 구독/해제
    // =========================================================
    private void HookAttackEvents()
    {
        if (cachedPlayerAttack == null)
            cachedPlayerAttack = FindObjectOfType<PlayerAttack>();

        if (cachedPlayerAttack == null) return;

        cachedPlayerAttack.OnAttackOutStart += HandleAttackStart;
        cachedPlayerAttack.OnAttackReturnEnd += HandleAttackEnd;
    }

    private void UnhookAttackEvents()
    {
        if (cachedPlayerAttack == null) return;

        cachedPlayerAttack.OnAttackOutStart -= HandleAttackStart;
        cachedPlayerAttack.OnAttackReturnEnd -= HandleAttackEnd;
    }

    private void HandleAttackStart()
    {
        parallaxPausedByAttack = true;

        if (player != null) lastPlayerX = player.position.x;
    }

    private void HandleAttackEnd()
    {
        parallaxPausedByAttack = false;

        if (player != null) lastPlayerX = player.position.x;
    }

    // =========================================================
    // Background (Optional)
    // =========================================================
    private void SetupBackgrounds()
    {
        if (bgs == null || bgs.Length == 0 || bgs[0] == null)
            return;

        SpriteRenderer sr = bgs[0].GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Debug.LogError("[MapManager] bgs[0]에 SpriteRenderer가 없습니다.");
            return;
        }

        bgWidth = sr.bounds.size.x;
        bgWidth = Mathf.Ceil(bgWidth * 1000f) / 1000f;
        bgWidth += 0.02f;

        activeBGs.Clear();
        for (int i = 0; i < bgs.Length; i++)
        {
            if (bgs[i] == null) continue;
            bgs[i].position = bgs[0].position + new Vector3((bgWidth - 0.2f) * i, 0, 0);
            activeBGs.Add(bgs[i]);
        }

        lastPlayerX = (player != null) ? player.position.x : 0f;
    }

    private void UpdateBackgroundParallaxByPlayer()
    {
        if (activeBGs.Count == 0) return;
        if (player == null) return;

        if (parallaxPausedByAttack)
        {
            lastPlayerX = player.position.x;
            return;
        }

        float currentX = player.position.x;
        float deltaX = currentX - lastPlayerX;

        if (Mathf.Abs(deltaX) > 0.0001f)
        {
            for (int i = 0; i < activeBGs.Count; i++)
            {
                var bg = activeBGs[i];
                if (bg == null) continue;
                bg.position += new Vector3(deltaX * parallax, 0, 0);
            }
        }

        lastPlayerX = currentX;
    }

    private void UpdateBackgroundCycling()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float camHalfWidth = cam.orthographicSize * cam.aspect;
        float camLeftX = cam.transform.position.x - camHalfWidth;

        if (activeBGs[0] != null && activeBGs[0].position.x + bgWidth / 2 < camLeftX)
        {
            Transform firstBG = activeBGs[0];
            activeBGs.RemoveAt(0);

            Transform lastBG = activeBGs[activeBGs.Count - 1];
            if (lastBG != null && firstBG != null)
                firstBG.position = lastBG.position + new Vector3(bgWidth - 0.2f, 0, 0);

            activeBGs.Add(firstBG);
        }
    }

    // =========================================================
    // Chunk Spawn
    // =========================================================
    void SpawnInitialChunk()
    {
        if (mapPatterns == null || mapPatterns.Count == 0) return;

        int index = 0;
        GameObject prefab = mapPatterns[index];
        if (prefab == null) return;

        GameObject chunk = Instantiate(prefab, Vector3.zero, Quaternion.identity);

        var gapScaler = chunk.GetComponent<MapChunkGapScaler>();
        if (gapScaler != null)
            gapScaler.Apply(currentMapSpeed);

        MapPattern pattern = chunk.GetComponent<MapPattern>();
        if (pattern == null || pattern.endPoint == null)
        {
            Debug.LogError("[MapManager] MapPattern 또는 endPoint가 없습니다. 프리팹 확인 필요.");
            Destroy(chunk);
            return;
        }

        lastEndPoint = pattern.endPoint;
        chunks.Enqueue(chunk);

        chunkPatternIndices.Enqueue(index);
        activePatternIndices.Add(index);

        spawnedChunkCount++;
        TrySpawnBossIfReady();
    }

    void SpawnNextChunk()
    {
        if (mapPatterns == null || mapPatterns.Count == 0) return;
        if (lastEndPoint == null) return;

        int index = DecideNextPatternIndex();
        GameObject prefab = mapPatterns[index];
        if (prefab == null) return;

        GameObject chunk = Instantiate(prefab);

        var gapScaler = chunk.GetComponent<MapChunkGapScaler>();
        if (gapScaler != null)
            gapScaler.Apply(currentMapSpeed);

        MapPattern pattern = chunk.GetComponent<MapPattern>();
        if (pattern == null || pattern.startPoint == null || pattern.endPoint == null)
        {
            Destroy(chunk);
            return;
        }

        Vector3 startLocal = pattern.startPoint.localPosition;
        chunk.transform.position = lastEndPoint.position - startLocal;

        lastEndPoint = pattern.endPoint;
        chunks.Enqueue(chunk);

        chunkPatternIndices.Enqueue(index);
        activePatternIndices.Add(index);

        spawnedChunkCount++;
        TrySpawnBossIfReady();

        if (debugBossAndChunk)
        {
            Debug.Log($"[MapManager] SpawnedChunkCount={spawnedChunkCount}, BossSpawned={bossSpawned}, BossAlive={IsBossAlive()}, PatternIndex={index}, RemovedTotal={removedChunkCountTotal}");
        }
    }

    private int DecideNextPatternIndex()
    {
        int count = mapPatterns != null ? mapPatterns.Count : 0;
        if (count <= 0) return 0;

        List<int> candidates = null;

        for (int i = 0; i < count; i++)
        {
            if (mapPatterns[i] == null) continue;
            if (activePatternIndices.Contains(i)) continue;

            candidates ??= new List<int>(count);
            candidates.Add(i);
        }

        if (candidates != null && candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];

        return Random.Range(0, count);
    }

    void RemoveOldChunk()
    {
        while (chunks.Count > maxChunks)
        {
            var old = chunks.Dequeue();

            if (chunkPatternIndices.Count > 0)
            {
                int removedIndex = chunkPatternIndices.Dequeue();
                activePatternIndices.Remove(removedIndex);
            }

            if (old != null)
            {
                Destroy(old);

                removedChunkCountTotal++;
                TryTriggerBossQTEByRemovedChunks();
            }
        }
    }

    // =========================================================
    // Boss Flow
    // =========================================================
    private void TrySpawnBossIfReady()
    {
        if (bossSpawned) return;

        if ((spawnedChunkCount - spawnedChunkCountAtStageStart) < chunksBeforeBoss)
            return;

        SpawnBoss();
    }

    private void SpawnBoss()
    {
        if (bossSpawned) return;
        bossSpawned = true;

        if (player == null)
        {
            Debug.LogError("[MapManager] player가 없어 보스를 소환할 수 없습니다.");
            return;
        }

        int stageIndex = Mathf.Clamp(bossesDefeated, 0, 2);

        GameObject prefab = null;
        if (bossPrefabs != null && bossPrefabs.Length > 0)
        {
            if (stageIndex < bossPrefabs.Length)
                prefab = bossPrefabs[stageIndex];
            else
                prefab = bossPrefabs[bossPrefabs.Length - 1];
        }

        if (prefab == null)
        {
            Debug.LogError("[MapManager] bossPrefabs가 비어있거나 stageIndex에 해당하는 프리팹이 없습니다.");
            return;
        }

        Vector3 spawnPos = (bossPos != null) ? bossPos.position : player.position;

        spawnedBossObj = Instantiate(prefab, spawnPos, Quaternion.identity);

        spawnedBoss = spawnedBossObj.GetComponent<Boss>();
        if (spawnedBoss == null)
            spawnedBoss = spawnedBossObj.GetComponentInChildren<Boss>(true);

        removedChunkCountAtBossSpawn = removedChunkCountTotal;
        removedChunkCountAtLastQTE = removedChunkCountTotal;
        qteTriggeredOnce = false;

        if (debugBossAndChunk)
        {
            Debug.Log($"[MapManager] Boss Spawned (stage={stageIndex}) at {spawnPos}. RemovedAtBossSpawn={removedChunkCountAtBossSpawn}, BossCompFound={(spawnedBoss != null)}");
        }
    }

    private void TryTriggerBossQTEByRemovedChunks()
    {
        if (!bossSpawned) return;
        if (!IsBossAlive()) return;
        if (qteOnlyOnceAfterBossSpawn && qteTriggeredOnce) return;

        if (waitUntilBossHitConsumed)
        {
            if (cachedPlayerAttack == null)
                cachedPlayerAttack = FindObjectOfType<PlayerAttack>();

            if (cachedPlayerAttack != null && cachedPlayerAttack.HasBossHitCredit())
                return;

            waitUntilBossHitConsumed = false;
            removedChunkCountAtLastQTE = removedChunkCountTotal;
        }

        if (spawnedBoss == null && spawnedBossObj != null)
        {
            spawnedBoss = spawnedBossObj.GetComponent<Boss>();
            if (spawnedBoss == null)
                spawnedBoss = spawnedBossObj.GetComponentInChildren<Boss>(true);
        }
        if (spawnedBoss == null) return;

        if (spawnedBoss.IsInQTE) return;

        int baseRemoved = qteOnlyOnceAfterBossSpawn ? removedChunkCountAtBossSpawn : removedChunkCountAtLastQTE;
        int passed = removedChunkCountTotal - baseRemoved;

        if (passed < removedChunksBeforeQTE) return;

        bool started = spawnedBoss.ForceStartAttackAttempt();
        if (started)
        {
            if (qteOnlyOnceAfterBossSpawn) qteTriggeredOnce = true;

            if (debugBossAndChunk)
                Debug.Log($"[MapManager] QTE TRIGGERED. passedRemoved={passed}, removedTotal={removedChunkCountTotal}");
        }
    }

    private bool IsBossAlive()
    {
        if (spawnedBossObj != null) return true;
        if (spawnedBoss != null) return true;
        return false;
    }

    // =========================================================
    // Speed Stage (Boss Count 기반)
    // =========================================================
    private void ApplyStageFromBossCount(bool playFx)
    {
        if (speedStages == null || speedStages.Length == 0)
            return;

        int targetStage = Mathf.Clamp(bossesDefeated, 0, speedStages.Length - 1);

        if (targetStage == currentSpeedStage && currentMapSpeed > 0f)
            return;

        currentSpeedStage = targetStage;
        currentMapSpeed = speedStages[currentSpeedStage];
        baseMapSpeed = currentMapSpeed;

        if (playFx)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PlaySpeedupSFX();

            if (InGamePopupUI != null)
                InGamePopupUI.Play("SPEED UP!!");

            Debug.Log($"[MapManager] Speed Stage {currentSpeedStage + 1} → {currentMapSpeed}");
        }
    }

    public void OnBossDefeated()
    {
        // ✅ 마지막 보스 클리어 연출용: 보스 Transform을 먼저 확보
        Transform lastBossFocus = (spawnedBossObj != null) ? spawnedBossObj.transform : null;

        bossesDefeated++;

        bossSpawned = false;
        spawnedBossObj = null;
        spawnedBoss = null;

        if (GameManager.Instance != null)
            GameManager.Instance.PlayBoomSFX();

        if (totalBossesToClear <= 0) totalBossesToClear = 1;

        if (bossesDefeated >= totalBossesToClear)
        {
            TriggerGameClear(lastBossFocus);
            return;
        }

        ApplyStageFromBossCount(playFx: true);

        spawnedChunkCountAtStageStart = spawnedChunkCount;

        removedChunkCountAtBossSpawn = removedChunkCountTotal;
        removedChunkCountAtLastQTE = removedChunkCountTotal;
        qteTriggeredOnce = false;
    }

    public void OnBossQTEResult(bool success)
    {
        removedChunkCountAtLastQTE = removedChunkCountTotal;

        if (success)
        {
            waitUntilBossHitConsumed = true;
        }
    }

    // =========================================================
    // Game Clear
    // =========================================================
    private void StopMapMovement()
    {
        currentMapSpeed = 0f;
    }

    private void TriggerGameClear(Transform bossFocus)
    {
        if (isGameCleared) return;
        isGameCleared = true;

        // 맵 정지(즉시)
        StopMapMovement();

        // 연출 코루틴 시작
        if (clearRoutine != null) StopCoroutine(clearRoutine);
        clearRoutine = StartCoroutine(GameClearSequence(bossFocus));
    }

    private IEnumerator GameClearSequence(Transform bossFocus)
    {
        // 1) 기존 UI 숨김
        if (uiRootsToHideOnClear != null)
        {
            for (int i = 0; i < uiRootsToHideOnClear.Length; i++)
            {
                if (uiRootsToHideOnClear[i] != null)
                    uiRootsToHideOnClear[i].SetActive(false);
            }
        }

        // 2) 플레이어 조작/공격 등 비활성화(선택)
        if (componentsToDisableOnClear != null)
        {
            for (int i = 0; i < componentsToDisableOnClear.Length; i++)
            {
                if (componentsToDisableOnClear[i] != null)
                    componentsToDisableOnClear[i].enabled = false;
            }
        }

        // 3) 카메라 줌/이동(보스가 없으면 플레이어로)
        Camera cam = clearCamera != null ? clearCamera : Camera.main;
        if (cam != null)
        {
            Vector3 startPos = cam.transform.position;
            float startSize = cam.orthographicSize;

            Transform focus = bossFocus != null ? bossFocus : player;
            Vector3 targetPos = startPos;
            if (focus != null)
            {
                Vector3 fp = focus.position + clearFocusOffset;
                targetPos = new Vector3(fp.x, fp.y, startPos.z);
            }

            float t = 0f;
            float dur = Mathf.Max(0.01f, clearZoomDuration);

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / dur);

                cam.transform.position = Vector3.Lerp(startPos, targetPos, a);
                cam.orthographicSize = Mathf.Lerp(startSize, clearTargetOrthoSize, a);

                yield return null;
            }

            cam.transform.position = targetPos;
            cam.orthographicSize = clearTargetOrthoSize;
        }

        // 4) 검은 페이드아웃 (ScreenFader 사용)
        if (screenFader != null)
        {
            // 페이드 중에는 입력 막기, 페이드 끝나면 기본 정책대로(통과)로 돌아가게 됨
            screenFader.FadeOut(fadeOutDuration);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, fadeOutDuration));
        }

        // 5) 잠깐 홀드 후 Clear UI 띄우기
        if (holdAfterFade > 0f)
        {
            float t = 0f;
            while (t < holdAfterFade)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (gameClearUI != null)
            gameClearUI.SetActive(true);

        if (stopTimeScaleOnClear)
            Time.timeScale = 0f;
    }

    // =========================================================
    // Refs
    // =========================================================
    private void TryResolveRefs()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (cachedPlayerAttack == null)
            cachedPlayerAttack = FindObjectOfType<PlayerAttack>();
    }
}
