using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapMover : MonoBehaviour
{
    public enum MoveMode { OneWayLeft, PingPongX }

    [Header("이동 모드")]
    public MoveMode mode = MoveMode.PingPongX;

    [Header("속도/구간")]
    public float speed = 2f;            // 이동 속도 (유닛/초)
    public float leftX = -5f;           // PingPong 왼쪽 경계
    public float rightX = 5f;           // PingPong 오른쪽 경계

    [Header("Rigidbody 사용")]
    public bool useRigidbody2D = true;  // true면 rb.MovePosition 사용, false면 transform으로 이동

    Rigidbody2D rb;
    Vector2 startPos;
    int dir = -1; // 왼쪽(-1)부터 시작

    void Awake()
    {
        if (useRigidbody2D)
        {
            rb = GetComponent<Rigidbody2D>();
            if (!rb)
            {
                Debug.LogWarning("[GrappleTargetMover] Rigidbody2D가 없습니다. 자동으로 transform 이동으로 전환합니다.");
                useRigidbody2D = false;
            }
            else
            {
                // 권장: Kinematic
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
            }
        }
        startPos = transform.position;
    }

    void Update()
    {
        if (!useRigidbody2D) MoveByTransform(Time.deltaTime);
    }

    void FixedUpdate()
    {
        if (useRigidbody2D) MoveByRigidbody(Time.fixedDeltaTime);
    }

    void MoveByTransform(float dt)
    {
        Vector2 pos = transform.position;

        if (mode == MoveMode.OneWayLeft)
        {
            pos.x += -speed * dt; // 왼쪽으로만 진행
        }
        else // PingPongX
        {
            pos.x += dir * speed * dt;
            if (pos.x <= leftX) { pos.x = leftX; dir = +1; }
            if (pos.x >= rightX) { pos.x = rightX; dir = -1; }
        }
        transform.position = pos;
    }

    void MoveByRigidbody(float dt)
    {
        Vector2 pos = rb.position;

        if (mode == MoveMode.OneWayLeft)
        {
            pos.x += -speed * dt;
        }
        else // PingPongX
        {
            pos.x += dir * speed * dt;
            if (pos.x <= leftX) { pos.x = leftX; dir = +1; }
            if (pos.x >= rightX) { pos.x = rightX; dir = -1; }
        }
        rb.MovePosition(pos);
    }

    void OnDrawGizmosSelected()
    {
        // 경계 시각화
        Gizmos.color = Color.cyan;
        Vector3 a = new Vector3(leftX, transform.position.y, 0);
        Vector3 b = new Vector3(rightX, transform.position.y, 0);
        Gizmos.DrawLine(a + Vector3.up * 0.5f, b + Vector3.up * 0.5f);
        Gizmos.DrawSphere(a + Vector3.up * 0.5f, 0.08f);
        Gizmos.DrawSphere(b + Vector3.up * 0.5f, 0.08f);
    }
}
