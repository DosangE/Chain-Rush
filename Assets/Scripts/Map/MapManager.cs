using System.Collections.Generic;
using System.Transactions;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    [Header("Map Speed")]
    public float currentMapSpeed = 15f;
    public float baseMapSpeed = 15f;

    [Header("Background Settings")]
    public Transform[] bgs = new Transform[3]; // bg1, bg2, bg3를 인스펙터에서 할당
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
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossPos;
    
    [Tooltip("누적 청크 스폰 수(초기 청크 포함)가 이 값에 도달하면 보스 1회 소환")]
    [SerializeField] private int chunksBeforeBoss = 10;

    [Tooltip("플레이어 기준 보스 등장 위치 오프셋(월드). 플레이어 X 고정 기준 화면에 보이게 +X 권장")]
    [SerializeField] private Vector2 bossSpawnOffsetFromPlayer = new Vector2(8f, 0f);

    [Tooltip("보스 출현 1개 전부터(=chunksBeforeBoss-1번째 청크부터) 평지(0번) 강제")]
    [SerializeField] private bool forceFlatOneChunkBeforeBoss = true;

    [Tooltip("보스가 살아있는 동안 평지(0번) 강제")]
    [SerializeField] private bool forceFlatPatternWhileBossAlive = true;

    [Header("Debug")]
    [SerializeField] private bool debugBossAndChunk = false;

    private int spawnedChunkCount = 0;   // 초기 청크 포함 누적 스폰 수
    private bool bossSpawned = false;    // 보스 1회 소환 여부
    private GameObject spawnedBossObj = null; // 보스 오브젝트 참조(컴포넌트 null 문제 대비)
    private Boss spawnedBoss = null;     // 보스 컴포넌트 참조(있으면 PlayerAttack 등과 연계)

    void Start()
    {
        // player / mapMover가 인스펙터에서 비어있으면 자동으로 찾아봄 (안전장치)
        TryResolveRefs();

        // 1) 맵 조각 초기화
        if (mapPatterns != null && mapPatterns.Count > 0)
        {
            SpawnInitialChunk();
        }
        else
        {
            Debug.LogError("[MapManager] mapPatterns가 비어있습니다.");
        }

        // 2) 배경 초기화
        if (bgs == null || bgs.Length == 0 || bgs[0] == null)
        {
            // 배경 없이도 게임 진행 가능하게 early return은 하되, chunk 시스템은 이미 Start에서 진행됨
            return;
        }

        SpriteRenderer sr = bgs[0].GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Debug.LogError("[MapManager] bgs[0]에 SpriteRenderer가 없습니다.");
            return;
        }

        bgWidth = sr.bounds.size.x;

        // 틈새 방지 보정
        bgWidth = Mathf.Ceil(bgWidth * 1000f) / 1000f;
        bgWidth += 0.02f;

        activeBGs.Clear();
        for (int i = 0; i < bgs.Length; i++)
        {
            if (bgs[i] == null) continue;

            // 첫 번째 배경 기준으로 i번째 배경을 순서대로 배치
            bgs[i].position = bgs[0].position + new Vector3((bgWidth - 0.2f) * i, 0, 0);
            activeBGs.Add(bgs[i]);
        }

        if (mapMover != null)
            lastMoverX = mapMover.transform.position.x;
    }

    void Update()
    {
        // ✅ 게임 진행 중이 아니면 MapManager가 player/mapMover를 만지지 않게 막음
        if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            return;

        // ✅ player가 Destroy 되었거나 미할당이면 Update에서 더 이상 접근하지 않음
        if (player == null)
        {
            // 재시작/씬리로드 등으로 player가 새로 생겼을 수도 있으니 한 번 찾아서 복구 시도
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;

            if (player == null) return; // 그래도 없으면 종료
        }

        // ✅ lastEndPoint가 없으면 chunk 시스템 진행 불가 (초기화 실패 보호)
        if (lastEndPoint == null)
            return;

        // 3) 맵 조각 생성 & 제거 로직
        // 플레이어 X 고정 + 맵 이동 구조에서는 endPoint가 왼쪽으로 밀리면서 조건을 만족하게 됨.
        if (player.position.x + spawnDistanceAhead > lastEndPoint.position.x)
        {
            SpawnNextChunk();
            RemoveOldChunk();
        }

        // 4) 패럴랙스 이동 로직
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

        // 5) 무한 배경 순환 로직 (3개 순환)
        if (activeBGs.Count > 0 && bgWidth > 0.001f)
            UpdateBackgroundCycling();
    }

    void UpdateBackgroundCycling()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float camHalfWidth = cam.orthographicSize * cam.aspect;
        float camLeftX = cam.transform.position.x - camHalfWidth;

        // activeBGs[0]은 항상 현재 가장 왼쪽에 있는 배경입니다.
        if (activeBGs[0] != null && activeBGs[0].position.x + bgWidth / 2 < camLeftX)
        {
            Transform firstBG = activeBGs[0];
            activeBGs.RemoveAt(0);

            // 현재 가장 마지막 배경(오른쪽 끝)의 뒤에 배치
            Transform lastBG = activeBGs[activeBGs.Count - 1];
            if (lastBG != null && firstBG != null)
                firstBG.position = lastBG.position + new Vector3(bgWidth - 0.2f, 0, 0);

            activeBGs.Add(firstBG);
        }
    }

    // --- 기존 맵 생성 시스템 (기능 유지 + 방어만 추가) ---
    void SpawnInitialChunk()
    {
        if (mapPatterns == null || mapPatterns.Count == 0) return;

        GameObject prefab = mapPatterns[0];
        if (prefab == null) return;

        GameObject chunk = Instantiate(prefab, Vector3.zero, Quaternion.identity);

        // ✅ 초기 청크도 갭 보정 적용
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

        // ✅ 초기 청크 포함 누적 카운트
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
        {
            // ✅ 프리팹 로컬 좌표 상태에서 먼저 갭 보정
            gapScaler.Apply(currentMapSpeed);
        }

        MapPattern pattern = chunk.GetComponent<MapPattern>();
        if (pattern == null || pattern.startPoint == null || pattern.endPoint == null)
        {
            Destroy(chunk);
            return;
        }

        // ✅ 갭 보정이 끝난 startPoint 기준으로 접합
        Vector3 startLocal = pattern.startPoint.localPosition;
        chunk.transform.position = lastEndPoint.position - startLocal;

        // ✅ 이제 endPoint는 완성된 상태
        lastEndPoint = pattern.endPoint;
        chunks.Enqueue(chunk);

        // ✅ 누적 카운트 + 보스 스폰 체크
        spawnedChunkCount++;
        TrySpawnBossIfReady();

        if (debugBossAndChunk)
        {
            Debug.Log($"[MapManager] SpawnedChunkCount={spawnedChunkCount}, BossSpawned={bossSpawned}, BossAlive={IsBossAlive()}, PatternIndex={index}");
        }
    }

    private int DecideNextPatternIndex()
    {
        // 다음에 스폰될 청크 번호(초기 청크 포함 누적 기준)
        int nextChunkNumber = spawnedChunkCount + 1;

        bool bossAlive = IsBossAlive();

        // 1) 보스가 살아있는 동안: 평지 강제
        if (forceFlatPatternWhileBossAlive && bossAlive)
            return 0;

        // 2) 보스 출현 1개 전부터: 평지 강제 (보스 아직 소환 안 된 상태에서만)
        if (forceFlatOneChunkBeforeBoss && !bossSpawned)
        {
            // chunksBeforeBoss=10이면 nextChunkNumber가 9 이상이면 평지로 바뀜
            if (chunksBeforeBoss >= 2 && nextChunkNumber >= chunksBeforeBoss - 1)
                return 0;
        }

        // 3) 그 외엔 랜덤
        return Random.Range(0, mapPatterns.Count);
    }

    void RemoveOldChunk()
    {
        while (chunks.Count > maxChunks)
        {
            var old = chunks.Dequeue();
            if (old != null) Destroy(old);
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
    }

    // =========================
    // Boss spawn helper
    // =========================
    private void TrySpawnBossIfReady()
    {
        if (bossSpawned) return;
        if (bossPrefab == null) return;
        if (spawnedChunkCount < chunksBeforeBoss) return;

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

        Vector3 spawnPos = bossPos.position;

        // ✅ 부모 null 강제 -> MapMover(맵 루트) 영향에서 완전히 분리
        GameObject bossObj = Instantiate(bossPrefab, spawnPos, Quaternion.identity, null);

        spawnedBossObj = bossObj;

        // ✅ 루트/자식 어디에 Boss가 있든 찾기
        spawnedBoss = bossObj.GetComponent<Boss>();
        if (spawnedBoss == null)
            spawnedBoss = bossObj.GetComponentInChildren<Boss>(true);

        if (spawnedBoss == null)
        {
            Debug.LogWarning("[MapManager] Boss Prefab(또는 자식)에 Boss 컴포넌트가 없습니다. "
                + "PlayerAttack의 보스 공격 분기(GetComponentInParent<Boss>)도 실패할 수 있습니다.");
        }

        if (debugBossAndChunk)
        {
            Debug.Log($"[MapManager] Boss Spawned at {spawnPos}. BossCompFound={(spawnedBoss != null)}");
        }
    }

    private bool IsBossAlive()
    {
        // Unity Destroy 특성상 파괴되면 == null로 판정됨
        if (spawnedBossObj != null) return true;
        if (spawnedBoss != null) return true;
        return false;
    }
}
