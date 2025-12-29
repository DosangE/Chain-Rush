using System.Collections.Generic;
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

    void Start()
    {
        // 1) 맵 조각 초기화
        SpawnInitialChunk();

        // 2) 배경 초기화
        if (bgs[0] == null) return;

        SpriteRenderer sr = bgs[0].GetComponent<SpriteRenderer>();
        bgWidth = sr.bounds.size.x;
        
        // 틈새 방지 보정
        bgWidth = Mathf.Ceil(bgWidth * 1000f) / 1000f;
        bgWidth += 0.02f; 

        // 배경들 초기 위치 정렬 및 리스트 추가
        for (int i = 0; i < bgs.Length; i++)
        {
            // 첫 번째 배경 기준으로 i번째 배경을 순서대로 배치
            bgs[i].position = bgs[0].position + new Vector3((bgWidth - 0.2f) * i, 0, 0);
            activeBGs.Add(bgs[i]);
        }

        lastMoverX = mapMover.transform.position.x;
    }

    void Update()
    {
        // 3) 맵 조각 생성 & 제거 로직
        if (player.position.x + spawnDistanceAhead > lastEndPoint.position.x)
        {
            SpawnNextChunk();
            RemoveOldChunk();
        }

        // 4) 패럴랙스 이동 로직
        float currentX = mapMover.transform.position.x;
        float deltaX = currentX - lastMoverX;

        if (Mathf.Abs(deltaX) > 0.0001f)
        {
            foreach (Transform bg in activeBGs)
            {
                bg.position += new Vector3(deltaX * parallax, 0, 0);
            }
        }
        lastMoverX = currentX;

        // 5) 무한 배경 순환 로직 (3개 순환)
        UpdateBackgroundCycling();
    }

    void UpdateBackgroundCycling()
    {
        Camera cam = Camera.main;
        float camHalfWidth = cam.orthographicSize * cam.aspect;
        float camLeftX = cam.transform.position.x - camHalfWidth;

        // activeBGs[0]은 항상 현재 가장 왼쪽에 있는 배경입니다.
        if (activeBGs[0].position.x + bgWidth / 2 < camLeftX)
        {
            Transform firstBG = activeBGs[0];
            activeBGs.RemoveAt(0); // 리스트에서 제거

            // 현재 가장 마지막 배경(오른쪽 끝)의 뒤에 배치
            Transform lastBG = activeBGs[activeBGs.Count - 1];
            firstBG.position = lastBG.position + new Vector3(bgWidth - 0.2f, 0, 0);

            activeBGs.Add(firstBG); // 리스트의 맨 뒤로 추가
        }
    }

    // --- 기존 맵 생성 시스템 (수정 없음) ---
    void SpawnInitialChunk()
    {
        GameObject prefab = mapPatterns[0];
        GameObject chunk = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        MapPattern pattern = chunk.GetComponent<MapPattern>();
        lastEndPoint = pattern.endPoint;
        chunks.Enqueue(chunk);
    }

    void SpawnNextChunk()
    {
        int index = Random.Range(0, mapPatterns.Count);
        GameObject prefab = mapPatterns[index];
        GameObject chunk = Instantiate(prefab);
        MapPattern pattern = chunk.GetComponent<MapPattern>();
        Vector3 offset = chunk.transform.position - pattern.startPoint.position;
        chunk.transform.position = lastEndPoint.position + offset;
        lastEndPoint = pattern.endPoint;
        chunks.Enqueue(chunk);
    }

    void RemoveOldChunk()
    {
        while (chunks.Count > maxChunks)
        {
            var old = chunks.Dequeue();
            Destroy(old);
        }
    }
}