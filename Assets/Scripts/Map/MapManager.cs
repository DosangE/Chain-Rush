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

    // =========================================================
    // ✅ Map Patterns (Chunk System) - Stage Pools
    // =========================================================
    [System.Serializable]
    public class StagePatternSet
    {
        [Tooltip("스테이지 이름(디버그용)")]
        public string stageName = "Stage";

        [Tooltip("이 스테이지에서 등장 가능한 맵 프리팹들")]
        public List<GameObject> patterns = new List<GameObject>();
    }

    [Header("Map Patterns (Stage Pools)")]
    [Tooltip("스테이지(=bossesDefeated 기반)별로 등장 가능한 청크 풀을 설정합니다.")]
    [SerializeField] private StagePatternSet[] stagePatternSets;

    [Tooltip("stagePatternSets가 비어있거나, 해당 스테이지 풀에 프리팹이 없을 때 사용할 기본 풀(옵션)")]
    public List<GameObject> mapPatterns; // 폴백용

    public int maxChunks = 5;
    public Transform player;
    public float spawnDistanceAhead = 30f;

    private Queue<GameObject> chunks = new Queue<GameObject>();
    private Transform lastEndPoint;

    // ✅ 변경 포인트(B안)
    // 인덱스 기반이 아니라 "프리팹 레퍼런스" 기반으로 현재 활성(화면에 떠있는) 패턴을 추적한다.
    private readonly Queue<GameObject> chunkPatternPrefabs = new Queue<GameObject>();
    private readonly HashSet<GameObject> activePatternPrefabs = new HashSet<GameObject>();

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
    [SerializeField] private GameObject[] uiRootsToHideOnClear;

    [Header("Game Clear Sequence - Disable Controls")]
    [SerializeField] private MonoBehaviour[] componentsToDisableOnClear;

    [Header("Game Clear Sequence - Camera")]
    [SerializeField] private Camera clearCamera;
    [SerializeField] private float clearZoomDuration = 0.9f;
    [SerializeField] private float clearTargetOrthoSize = 3.5f;
    [SerializeField] private Vector3 clearFocusOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Game Clear Sequence - Fade")]
    [SerializeField] private ScreenFader screenFader;
    [SerializeField] private float fadeOutDuration = 0.6f;

    [Header("Game Clear Sequence - Timing")]
    [SerializeField] private float holdAfterFade = 0.15f;

    private Coroutine clearRoutine = null;
    // =========================================================
    // Boss Entrance (Drop from top)
    // =========================================================
    [Header("Boss Entrance - Drop")]
    [SerializeField] private bool playBossEntrance = true;

    [Tooltip("최종 스폰 위치(기존 spawnPos) 기준으로 위쪽으로 얼마나 띄워서 시작할지")]
    [SerializeField] private float bossEntranceStartYOffset = 10f;

    [Tooltip("내려오는 시간")]
    [SerializeField] private float bossEntranceDuration = 0.7f;

    [Tooltip("끝에서 살짝 튕기는 느낌(0이면 Lerp로 딱 멈춤)")]
    [SerializeField] private float bossEntranceOvershoot = 0.25f;

    [Tooltip("연출 중에 플레이어/맵 진행을 멈출지 (맵은 currentMapSpeed=0로 멈춤)")]
    [SerializeField] private bool stopMapDuringBossEntrance = true;

    private Coroutine bossEntranceRoutine = null;

    void Start()
    {
        TryResolveRefs();
        HookAttackEvents();

        bossesDefeated = 0;
        ApplyStageFromBossCount(playFx: false);

        spawnedChunkCountAtStageStart = 0;

        if (GetCurrentStagePatternsCount() > 0 || (mapPatterns != null && mapPatterns.Count > 0))
        {
            SpawnInitialChunk();
        }
        else
        {
            Debug.LogError("[MapManager] stagePatternSets/mapPatterns가 비어있습니다.");
        }

        SetupBackgrounds();

        if (clearCamera == null) clearCamera = Camera.main;
        if (screenFader != null)
            screenFader.SetAlpha(0f, blockInput: false);
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

        if (lastEndPoint != null && player.position.x + spawnDistanceAhead > lastEndPoint.position.x)
        {
            SpawnNextChunk();
            RemoveOldChunk();
        }

        UpdateBackgroundParallaxByPlayer();

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
    // ✅ Stage Pattern Helpers
    // =========================================================
    private List<GameObject> GetCurrentStagePatterns()
    {
        int stageIndex = Mathf.Clamp(bossesDefeated, 0, 9999);

        if (stagePatternSets != null && stagePatternSets.Length > 0)
        {
            int idx = Mathf.Clamp(stageIndex, 0, stagePatternSets.Length - 1);
            var set = stagePatternSets[idx];
            if (set != null && set.patterns != null && set.patterns.Count > 0)
                return set.patterns;
        }

        return mapPatterns;
    }

    private int GetCurrentStagePatternsCount()
    {
        var list = GetCurrentStagePatterns();
        return list != null ? list.Count : 0;
    }

    // =========================================================
    // Chunk Spawn
    // =========================================================
    void SpawnInitialChunk()
    {
        var patterns = GetCurrentStagePatterns();
        if (patterns == null || patterns.Count == 0) return;

        int index = 0;
        GameObject prefab = patterns[index];
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

        // ✅ 프리팹 레퍼런스로 추적
        chunkPatternPrefabs.Enqueue(prefab);
        activePatternPrefabs.Add(prefab);

        spawnedChunkCount++;
        TrySpawnBossIfReady();
    }

    void SpawnNextChunk()
    {
        var patterns = GetCurrentStagePatterns();
        if (patterns == null || patterns.Count == 0) return;
        if (lastEndPoint == null) return;

        int index = DecideNextPatternIndex(patterns);
        GameObject prefab = patterns[index];
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

        // ✅ 프리팹 레퍼런스로 추적
        chunkPatternPrefabs.Enqueue(prefab);
        activePatternPrefabs.Add(prefab);

        spawnedChunkCount++;
        TrySpawnBossIfReady();

        if (debugBossAndChunk)
        {
            Debug.Log($"[MapManager] SpawnedChunkCount={spawnedChunkCount}, BossSpawned={bossSpawned}, BossAlive={IsBossAlive()}, PatternIndex={index}, RemovedTotal={removedChunkCountTotal}");
        }
    }

    // ✅ 변경: "현재 화면에 떠있는 프리팹(activePatternPrefabs)"은 후보에서 제외
    private int DecideNextPatternIndex(List<GameObject> patterns)
    {
        int count = patterns != null ? patterns.Count : 0;
        if (count <= 0) return 0;

        List<int> candidates = null;

        for (int i = 0; i < count; i++)
        {
            var prefab = patterns[i];
            if (prefab == null) continue;

            // 현재 화면에 떠있는(=active) 프리팹은 제외
            if (activePatternPrefabs.Contains(prefab)) continue;

            candidates ??= new List<int>(count);
            candidates.Add(i);
        }

        if (candidates != null && candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];

        // 폴백: 스테이지 풀이 너무 작으면(예: 1~2개) 결국 활성 중복 허용 랜덤
        return Random.Range(0, count);
    }

    void RemoveOldChunk()
    {
        while (chunks.Count > maxChunks)
        {
            var old = chunks.Dequeue();

            // ✅ 프리팹 추적 해제
            if (chunkPatternPrefabs.Count > 0)
            {
                GameObject removedPrefab = chunkPatternPrefabs.Dequeue();
                if (removedPrefab != null)
                    activePatternPrefabs.Remove(removedPrefab);
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

        // ✅ 일단 생성
        spawnedBossObj = Instantiate(prefab, spawnPos, Quaternion.identity);

        // ✅ 등장 연출: 위에서 내려오기
        if (playBossEntrance)
        {
            if (bossEntranceRoutine != null) StopCoroutine(bossEntranceRoutine);
            bossEntranceRoutine = StartCoroutine(BossEntranceDrop(spawnedBossObj, spawnPos));
        }


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

        // ✅ B 방식: 여기서 active/queue를 절대 초기화하지 않는다.
        // 화면에 남아있는 청크들은 그대로 유지되고,
        // 다음 Spawn부터만 GetCurrentStagePatterns()가 새 풀을 사용한다.
    }

    public void OnBossDefeated()
    {
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

        StopMapMovement();

        if (clearRoutine != null) StopCoroutine(clearRoutine);
        clearRoutine = StartCoroutine(GameClearSequence(bossFocus));
    }

    private IEnumerator GameClearSequence(Transform bossFocus)
    {
        if (uiRootsToHideOnClear != null)
        {
            for (int i = 0; i < uiRootsToHideOnClear.Length; i++)
            {
                if (uiRootsToHideOnClear[i] != null)
                    uiRootsToHideOnClear[i].SetActive(false);
            }
        }

        if (componentsToDisableOnClear != null)
        {
            for (int i = 0; i < componentsToDisableOnClear.Length; i++)
            {
                if (componentsToDisableOnClear[i] != null)
                    componentsToDisableOnClear[i].enabled = false;
            }
        }

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

        if (screenFader != null)
        {
            screenFader.FadeOut(fadeOutDuration);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, fadeOutDuration));
        }

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

        GameManager.Instance.PlayClearSFX();

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

    private IEnumerator BossEntranceDrop(GameObject bossObj, Vector3 finalPos)
    {
        if (bossObj == null) yield break;

        float prevSpeed = currentMapSpeed;
        if (stopMapDuringBossEntrance)
            currentMapSpeed = 0f;

        // 시작 위치: 최종 위치보다 위
        Vector3 startPos = finalPos + new Vector3(0f, bossEntranceStartYOffset, 0f);
        bossObj.transform.position = startPos;

        float dur = Mathf.Max(0.01f, bossEntranceDuration);
        float t = 0f;

        // Overshoot 목표(살짝 더 내려갔다가 다시 올라오게)
        Vector3 overshootPos = finalPos - new Vector3(0f, Mathf.Max(0f, bossEntranceOvershoot), 0f);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);

            // 부드러운 감속 (ease out)
            float eased = 1f - Mathf.Pow(1f - a, 3f);

            // 0~0.85 구간: start -> overshoot
            // 0.85~1 구간: overshoot -> final
            if (eased < 0.85f)
            {
                float k = eased / 0.85f;
                bossObj.transform.position = Vector3.Lerp(startPos, overshootPos, k);
            }
            else
            {
                float k = (eased - 0.85f) / 0.15f;
                bossObj.transform.position = Vector3.Lerp(overshootPos, finalPos, k);
            }

            yield return null;
        }

        bossObj.transform.position = finalPos;

        if (stopMapDuringBossEntrance)
            currentMapSpeed = prevSpeed;

        bossEntranceRoutine = null;
    }

}
