using System.Collections.Generic;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    public List<GameObject> mapPatterns;
    public int maxChunks = 5;

    public Transform player;
    public float spawnDistanceAhead = 30f;

    private Queue<GameObject> chunks = new Queue<GameObject>();
    private Transform lastEndPoint; 

    void Start()
    {
        // 첫 패턴 하나 생성 (기본 위치에 맞게)
        SpawnInitialChunk();
    }

    void Update()
    {
        if (player.position.x + spawnDistanceAhead > lastEndPoint.position.x)
        {
            SpawnNextChunk();
            RemoveOldChunk();
        }
    }

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

        // 새 패턴의 StartPoint를 lastEndPoint 위치에 맞춰 스폰
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
