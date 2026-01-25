using System.Collections.Generic;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    [Header("Map Speed")]
    public float currentMapSpeed = 15f;
    public float baseMapSpeed = 15f;

    [Header("Background Settings")]
    public Transform[] bgs = new Transform[3];
    public float parallax = 0.3f;
    public MapMover mapMover;

    private float bgWidth;
    private float lastMoverX;
    private List<Transform> activeBGs = new List<Transform>();

    [Header("Map Patterns (Chunk System)")]
    public List<GameObject> mapPatterns;
    public int maxChunks = 5;
    public Transform player;
    public float spawnDistanceAhead = 30f;

    private Queue<GameObject> chunks = new Queue<GameObject>();
    private Transform lastEndPoint;

    // =========================
    // Boss Spawn
    // =========================
    [Header("Boss Spawn")]
    [SerializeField] private GameObject bossPrefabStage1;
    [SerializeField] private GameObject bossPrefabStage2;
    [SerializeField] private GameObject bossPrefabFinal;
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

    private int spawnedChunkCount = 0;       // 초기 청크 포함 누적 스폰 수
    private bool bossSpawned = false;        // 보스 1회 소환 여부
    private GameObject spawnedBossObj = null;
    private Boss spawnedBoss = null;

    // --- 삭제(지나감) 카운터 ---
    private int removedChunkCountTotal = 0;  // 게임 시작 이후 삭제된 청크 총합
    private int removedChunkCountAtBossSpawn = 0; // 보스 소환 시점의 removedChunkCountTotal
    private int removedChunkCountAtLastQTE = 0;   // 마지막 QTE 시작 시점의 removedChunkCountTotal
    private bool qteTriggeredOnce = false;

    [Header("Speed Stages")]
    [SerializeField] private float[] speedStages = { 15f, 19f, 23f };

    private int currentSpeedStage = 0;

    private int spawnedChunkCountAtStageStart = 0; // 스테이지 시작 시점 누적 스폰 수

    private bool waitUntilBossHitConsumed = false;
    private PlayerAttack cachedPlayerAttack = null;

    // =========================
    // ✅ NEW: "현재 살아있는 청크 패턴" 추적
    // =========================
    private readonly Queue<int> chunkPatternIndices = new Queue<int>(); // chunks와 동일 순서
    private readonly HashSet<int> activePatternIndices = new HashSet<int>(); // 현재 스폰되어 살아있는 패턴 인덱스들


    void Start()
    {
        currentSpeedStage = 0;
        currentMapSpeed = speedStages[currentSpeedStage];
        spawnedChunkCountAtStageStart = 0;

        TryResolveRefs();

        if (mapPatterns != null && mapPatterns.Count > 0)
        {
            SpawnInitialChunk();
        }
        else
        {
            Debug.LogError("[MapManager] mapPatterns가 비어있습니다.");
        }

        if (bgs == null || bgs.Length == 0 || bgs[0] == null)
        {
            return;
        }

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

        if (mapMover != null)
            lastMoverX = mapMover.transform.position.x;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            return;

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            if (player == null) return;
        }

        if (lastEndPoint == null)
            return;

        if (player.position.x + spawnDistanceAhead > lastEndPoint.position.x)
        {
            SpawnNextChunk();
            RemoveOldChunk(); // ✅ 여기서 삭제 카운트 + QTE 트리거
        }

        if (mapMover != null)
        {
            float currentX = mapMover.transform.position.x;
            float deltaX = currentX - lastMoverX;

            if (Mathf.Abs(deltaX) > 0.0001f)
            {
                for (int i = 0; i < activeBGs.Count; i++)
                {
                    var bg = activeBGs[i];
                    if (bg == null) continue;
                    bg.position += new Vector3(deltaX * parallax, 0, 0);
                }
            }
            lastMoverX = currentX;
        }

        if (activeBGs.Count > 0 && bgWidth > 0.001f)
            UpdateBackgroundCycling();
    }

    void UpdateBackgroundCycling()
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

    void SpawnInitialChunk()
    {
        if (mapPatterns == null || mapPatterns.Count == 0) return;

        int index = 0;
        GameObject prefab = mapPatterns[index];
        if (prefab == null) return;

        GameObject chunk = Instantiate(prefab, Vector3.zero, Quaternion.identity);

        var gapScaler = chunk.GetComponent<MapChunkGapScaler>();
        if (gapScaler != null)
        {
            gapScaler.Apply(currentMapSpeed);
        }

        MapPattern pattern = chunk.GetComponent<MapPattern>();
        if (pattern == null || pattern.endPoint == null)
        {
            Debug.LogError("[MapManager] MapPattern 또는 endPoint가 없습니다. 프리팹 확인 필요.");
            Destroy(chunk);
            return;
        }

        lastEndPoint = pattern.endPoint;
        chunks.Enqueue(chunk);

        // ✅ NEW: 패턴 인덱스 추적 등록
        chunkPatternIndices.Enqueue(index);
        activePatternIndices.Add(index);

        spawnedChunkCount++;
        TrySpawnBossIfReady();
    }

    void SpawnNextChunk()
    {
        if (mapPatterns == null || mapPatterns.Count == 0) return;
        if (lastEndPoint == null) return;

        int index = DecideNextPatternIndex(); // ✅ NEW: "현재 살아있는 패턴" 제외 로직 적용
        GameObject prefab = mapPatterns[index];
        if (prefab == null) return;

        GameObject chunk = Instantiate(prefab);

        var gapScaler = chunk.GetComponent<MapChunkGapScaler>();
        if (gapScaler != null)
        {
            gapScaler.Apply(currentMapSpeed);
        }

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

        // ✅ NEW: 패턴 인덱스 추적 등록
        chunkPatternIndices.Enqueue(index);
        activePatternIndices.Add(index);

        spawnedChunkCount++;
        TrySpawnBossIfReady();

        if (debugBossAndChunk)
        {
            Debug.Log($"[MapManager] SpawnedChunkCount={spawnedChunkCount}, BossSpawned={bossSpawned}, BossAlive={IsBossAlive()}, PatternIndex={index}, RemovedTotal={removedChunkCountTotal}");
        }
    }

    // =========================
    // ✅ NEW: "활성 청크에서 사용 중인 패턴은 제외" 룰
    // =========================
    private int DecideNextPatternIndex()
    {
        int count = mapPatterns != null ? mapPatterns.Count : 0;
        if (count <= 0) return 0;

        // 후보(=현재 active에 없는 인덱스) 만들기
        List<int> candidates = null;

        // activePatternIndices가 너무 커질 일은 없지만, 안전하게 count 기준으로 필터
        for (int i = 0; i < count; i++)
        {
            if (mapPatterns[i] == null) continue; // null 프리팹은 제외
            if (activePatternIndices.Contains(i)) continue;

            candidates ??= new List<int>(count);
            candidates.Add(i);
        }

        // 후보가 있으면 그 중 랜덤
        if (candidates != null && candidates.Count > 0)
        {
            return candidates[Random.Range(0, candidates.Count)];
        }

        // ✅ 예외 처리:
        // 모든 패턴이 현재 active(=maxChunks가 패턴 수보다 크거나 같을 때)라면
        // "겹치지 않게"가 불가능하므로 그냥 랜덤 fallback
        // (원하면 여기서 '가장 오래된 active를 제외하고 뽑기' 같은 정책도 가능)
        return Random.Range(0, count);
    }

    void RemoveOldChunk()
    {
        while (chunks.Count > maxChunks)
        {
            var old = chunks.Dequeue();

            // ✅ NEW: old와 동일 순서로 패턴 인덱스도 제거
            if (chunkPatternIndices.Count > 0)
            {
                int removedIndex = chunkPatternIndices.Dequeue();
                activePatternIndices.Remove(removedIndex);
            }

            if (old != null)
            {
                Destroy(old);

                // ✅ "삭제된 청크" = 실제로 지나간 청크로 카운트
                removedChunkCountTotal++;

                // ✅ 보스전 QTE 트리거는 삭제 기준으로 체크
                TryTriggerBossQTEByRemovedChunks();
            }
        }
    }

    private void TryResolveRefs()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (mapMover == null)
            mapMover = FindObjectOfType<MapMover>();

        if (cachedPlayerAttack == null)
            cachedPlayerAttack = FindObjectOfType<PlayerAttack>();
    }

    // =========================
    // Boss spawn helper
    // =========================
    private void TrySpawnBossIfReady()
    {
        if (bossSpawned) return;

        if ((spawnedChunkCount - spawnedChunkCountAtStageStart) < chunksBeforeBoss) return;

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

        Vector3 spawnPos = (bossPos != null) ? bossPos.position : player.position;

        GameObject prefab =
            currentSpeedStage == 0 ? bossPrefabStage1 :
            currentSpeedStage == 1 ? bossPrefabStage2 :
            bossPrefabFinal;

        GameObject bossObj = Instantiate(prefab, spawnPos, Quaternion.identity);

        spawnedBoss = bossObj.GetComponent<Boss>();
        if (spawnedBoss == null)
            spawnedBoss = bossObj.GetComponentInChildren<Boss>(true);

        // ✅ 보스 진입 시점의 "삭제 카운트"를 기준점으로 저장
        removedChunkCountAtBossSpawn = removedChunkCountTotal;
        removedChunkCountAtLastQTE = removedChunkCountTotal;
        qteTriggeredOnce = false;

        if (debugBossAndChunk)
        {
            Debug.Log($"[MapManager] Boss Spawned at {spawnPos}. RemovedAtBossSpawn={removedChunkCountAtBossSpawn}, BossCompFound={(spawnedBoss != null)}");
        }
    }

    private void TryTriggerBossQTEByRemovedChunks()
    {
        if (!bossSpawned) return;
        if (!IsBossAlive()) return;
        if (qteOnlyOnceAfterBossSpawn && qteTriggeredOnce) return;

        // ✅ QTE 성공으로 공격권(보스 1회 타격)이 생긴 상태면,
        // 그 공격권을 "소모할 때까지" QTE를 다시 띄우지 않는다.
        if (waitUntilBossHitConsumed)
        {
            if (cachedPlayerAttack == null)
                cachedPlayerAttack = FindObjectOfType<PlayerAttack>();

            // PlayerAttack에 아래에서 추가할 메서드 사용
            if (cachedPlayerAttack != null && cachedPlayerAttack.HasBossHitCredit())
                return;

            // 공격권이 없어진 순간부터 다시 N청크 카운트 시작
            waitUntilBossHitConsumed = false;
            removedChunkCountAtLastQTE = removedChunkCountTotal;
        }

        // Boss 참조 복구 시도(혹시 누락됐을 때)
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

        bool started = spawnedBoss.ForceStartAttackAttempt(); // ✅ 쿨타임 무시 강제 시작
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

    public void AdvanceSpeedStage()
    {
        if (currentSpeedStage >= speedStages.Length - 1)
            return;

        currentSpeedStage++;
        currentMapSpeed = speedStages[currentSpeedStage];

        if (InGamePopupUI != null)
            InGamePopupUI.Play("SPEED UP!!");

        Debug.Log($"[MapManager] Speed Stage {currentSpeedStage + 1} → {currentMapSpeed}");
    }

    public void OnBossDefeated()
    {
        // ✅ 보스 참조 정리
        bossSpawned = false;
        spawnedBossObj = null;
        spawnedBoss = null;

        // ✅ 다음 스테이지 시작 기준점 갱신 (지금부터 다시 chunksBeforeBoss 카운트)
        spawnedChunkCountAtStageStart = spawnedChunkCount;

        // ✅ QTE 기준점 리셋(다음 보스 기준으로 다시 계산)
        removedChunkCountAtBossSpawn = removedChunkCountTotal;
        removedChunkCountAtLastQTE = removedChunkCountTotal;
        qteTriggeredOnce = false;
    }

    public void OnBossQTEResult(bool success)
    {
        // ✅ 성공/실패와 무관하게 QTE가 "끝난 시점"부터
        // removedChunksBeforeQTE 만큼 청크가 더 지나야 다음 QTE
        removedChunkCountAtLastQTE = removedChunkCountTotal;
    }
}
