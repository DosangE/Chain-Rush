using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapMover : MonoBehaviour
{
    public enum MoveMode { OneWayLeft }

    [Header("이동 모드")]
    public MoveMode mode = MoveMode.OneWayLeft;

    [Header("Rigidbody 사용")]
    public bool useRigidbody2D = true;

    private Rigidbody2D rb;
    private bool isMoving = true;
    private float leftX = -5f;
    private float rightX = 5f;
    private int dir = -1;

    private MapManager manager;

    void Awake()
    {
        manager = FindObjectOfType<MapManager>();

        if (useRigidbody2D)
        {
            rb = GetComponent<Rigidbody2D>();
            if (!rb)
            {
                Debug.LogWarning("[MapMover] Rigidbody2D 없음 → transform 이동으로 전환");
                useRigidbody2D = false;
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
            }
        }
    }

    void Update()
    {
        if (!isMoving || GameManager.Instance.State != GameState.Playing)
            return;

        if (!useRigidbody2D)
            MoveByTransform(Time.deltaTime);
    }

    void FixedUpdate()
    {
        if (useRigidbody2D)
            MoveByRigidbody(Time.fixedDeltaTime);
    }

    public void StopMove()
    {
        isMoving = false;
    }

    void MoveByTransform(float dt)
    {
        if (manager == null) return;
        float speed = manager.currentMapSpeed;

        Vector2 pos = transform.position;

        if (mode == MoveMode.OneWayLeft)
            pos.x += -speed * dt;
        else
        {
            pos.x += dir * speed * dt;
            if (pos.x <= leftX) { pos.x = leftX; dir = +1; }
            if (pos.x >= rightX) { pos.x = rightX; dir = -1; }
        }

        transform.position = pos;
    }

    void MoveByRigidbody(float dt)
    {
        if (manager == null) return;
        float speed = manager.currentMapSpeed;

        Vector2 pos = rb.position;

        if (mode == MoveMode.OneWayLeft)
            pos.x += -speed * dt;
        else
        {
            pos.x += dir * speed * dt;
            if (pos.x <= leftX) { pos.x = leftX; dir = +1; }
            if (pos.x >= rightX) { pos.x = rightX; dir = -1; }
        }

        rb.MovePosition(pos);
    }
}
