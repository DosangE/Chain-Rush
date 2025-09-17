using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grappling : MonoBehaviour
{
    [Header("컴포넌트 설정")]
    [SerializeField] private LineRenderer line;    // 연결 줄의 시각적 표현
    [SerializeField] private Transform hook;    // 줄이 발사되고 도달할 위치
    [SerializeField] private DistanceJoint2D joint2D;    //  물리적으로 연결을 담당하는 Joint
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] float jumpForce = 15f;
    private bool isGrounded;

    [Header("감지 대상 레이어")]
    [SerializeField] private LayerMask grappleLayer;    //  Raycast로 감지할 수 있는 레이어
    private Vector2 launchDir = new Vector2(1, 1).normalized;   // 줄이 발사되는 방향
    private bool isHookActive;      // 줄이 발사 중인지 여부
    private bool isLineMax;   // 줄이 최대 길이에 도달했는지 여부
    private bool isAttach;      // 줄이 연결된 상태인지 여부
    private float hookDist = 0f;    // 줄 길이를 시간에 따라 누적시키기 위한 스칼라(플레이어 기준)

    [Header("줄 설정")]
    [SerializeField] private float maxGrappleDistance = 7f;
    [SerializeField] private float minGrappleDistance = 1f;
    [SerializeField] private float hookSpeed = 25; // 줄이 발사되는 속도
    [SerializeField] private float visualOffset = 0.05f;  // 줄 길이 보정 값


    // 그래플 발사 입력 버퍼
    bool queuedGrapple = false;
    float queuedGrappleUntil = 0f;
    const float jumpBufferTime = 0.15f; // 점프 후 이 시간 안에 공중이면 자동 발사
    void Start()
    {
        line.positionCount = 2;
        line.startWidth = 0.15f;
        line.endWidth = 0.05f;
        line.useWorldSpace = true;

        isHookActive = false;
        isAttach = false;
        isLineMax = false;

        line.enabled = false;
        hook.gameObject.SetActive(false);
        joint2D.enabled = false;
    }

    void Update()
    {
        CheckLineBlocked();   // ✅ 항상 실행

        line.SetPosition(0, transform.position);

        if (isAttach && joint2D.enabled && joint2D.connectedBody != null)
        {
            HandleAttachedState();  // 연결된 상태에서의 처리
        }
        else
        {
            HandleDetachedState();  // 연결되지 않은 상태에서의 처리
        }
    }
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }
    public bool IsGrounded()
    {
        return isGrounded;
    }
    void Jump()
    {
        if (IsGrounded())
        {
            // 수직속도 리셋 후 점프 (보다 일관된 점프감)
            rb.velocity = new Vector2(rb.velocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
    }
    private void CheckLineBlocked()
    {
        Vector2 targetPos;

        if (isAttach && joint2D.enabled && joint2D.connectedBody != null)
        {
            // 연결된 상태면 Anchor로 검사
            targetPos = joint2D.connectedBody.transform.TransformPoint(joint2D.connectedAnchor);
        }
        else if (isHookActive)
        {
            // 발사 또는 되돌림 중이면 Hook 위치로 검사
            targetPos = hook.position;
        }
        else
        {
            // 아무것도 없으면 검사 안함
            return;
        }

        Vector2 dir = targetPos - (Vector2)transform.position;
        float distance = dir.magnitude;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir.normalized, distance, grappleLayer);

        if (hit.collider != null &&
            hit.collider.attachedRigidbody != joint2D.connectedBody &&  // 연결 대상은 무시
            hit.collider.gameObject != gameObject)                      // Player 자신은 무시
        {
            Debug.Log($"[LineBlocked]  {hit.collider.name} 끼어듦 → 즉시 해제");
            ReleaseGrapple();
        }
    }

    private void HandleAttachedState()
    {

        Vector2 anchorPos = joint2D.connectedBody.transform.TransformPoint(joint2D.connectedAnchor);
        Vector2 dirToAnchor = anchorPos - (Vector2)transform.position;
        // 줄 위치
        line.SetPosition(1, GetVisualAnchor());

        // 좌클릭 해제 시 끊기
        if (!Input.GetKey(KeyCode.Mouse0))
        {
            ReleaseGrapple();
            return;
        }
    }
    private void HandleDetachedState()
    {
        // 1) 입력 처리
        if (Input.GetKeyDown(KeyCode.Mouse0) && !isHookActive)
        {
            if (IsGrounded()) Jump();
            else StartHookShot();
        }

        // 2) 훅 이동/상태 갱신
        if (isHookActive && !isAttach)
        {
            if (isLineMax || IsGrounded()) ReturnHook();
            else ShootHook();
        }
        line.SetPosition(1, hook.position);
    }

    private void StartHookShot()
    {
        hook.SetParent(null);
        hook.position = transform.position;

        isHookActive = true;
        isLineMax = false;
        isAttach = false;

        hookDist = 0f;    // 줄 길이 초기화

        hook.gameObject.SetActive(true);
        line.enabled = true;
    }

    private void ShootHook()
    {
        // 항상 45° 유지
        Vector2 origin = transform.position;
        Vector2 dir = launchDir.normalized; // (1,1).normalized

        // 이번 프레임에 늘어날 목표 거리
        float nextDist = Mathf.Min(hookDist + hookSpeed * Time.deltaTime, maxGrappleDistance);

        // 플레이어 현재 위치를 기준으로, 0 -> nextDist 구간에서 레이캐스트
        RaycastHit2D hit = Physics2D.Raycast(origin, dir, nextDist, grappleLayer);

        if (hit.collider != null && hit.collider.attachedRigidbody != null)
        {
            float hitDistance = Vector2.Distance(origin, hit.point);

            if (hitDistance < minGrappleDistance)
            {
                isLineMax = true;     // 너무 가까우면 연결하지 않음 → 회수 루틴으로
                return;
            }

            // ✅ 연결
            joint2D.connectedBody = hit.collider.attachedRigidbody;
            joint2D.autoConfigureConnectedAnchor = false;
            joint2D.connectedAnchor = hit.collider.attachedRigidbody.transform.InverseTransformPoint(hit.point);
            joint2D.autoConfigureDistance = false;
            joint2D.maxDistanceOnly = false;
            joint2D.enableCollision = true;
            joint2D.distance = hitDistance;
            joint2D.enabled = true;

            isAttach = true;

            // 훅(비주얼) 위치 고정
            hook.position = hit.point;

            return;
        }

        // 충돌 안 됐으면: 거리 스칼라만 늘리고, 훅 위치는 "플레이어 현재 위치 + 45° × 거리"로 강제
        hookDist = nextDist;
        hook.position = origin + dir * hookDist;   // ✅ 플레이어가 움직여도 항상 45° 유지

        if (hookDist >= maxGrappleDistance)
        {
            isLineMax = true;
        }
    }

    private void ReturnHook()
    {
        isHookActive = false;
        isLineMax = false;
        hookDist = 0f;
        hook.gameObject.SetActive(false);
        line.enabled = false;
        hook.position = transform.position;
    }

    // 연결 해제 처리
    private void ReleaseGrapple()
    {
        isAttach = false;
        isHookActive = false;
        isLineMax = false;
        hookDist = 0f;
        joint2D.connectedBody = null;
        joint2D.enabled = false;
        hook.gameObject.SetActive(false);
        line.enabled = false;
        hook.position = transform.position;
    }
    private Vector2 GetVisualAnchor()
    {
        Vector2 anchorPos = joint2D.connectedBody.transform.TransformPoint(joint2D.connectedAnchor);
        Vector2 dirToAnchor = (anchorPos - (Vector2)transform.position).normalized;

        Vector2 visualPos = anchorPos + dirToAnchor * visualOffset;

        return visualPos;
    }
}