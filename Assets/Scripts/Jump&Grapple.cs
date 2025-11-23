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
    private bool isDead = false;
    private bool isGrounded;

    [Header("감지 대상 레이어")]
    [SerializeField] private LayerMask grappleLayer;    //  Raycast로 감지할 수 있는 레이어
    private Vector2 launchDir = new Vector2(0.5f, 0.6f).normalized;   // 줄이 발사되는 방향
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

    [Header("스윙/상승 설정")]
    [SerializeField] private float xProximityThreshold = 1.0f; // 앵커X와 플레이어X가 이 값 이하로 가까워지면 상승
    [SerializeField] private float ropeRetractSpeed = 4.0f;     // 상승 중 로프 감아들이는 속도(거리/초)
    [SerializeField] private float liftForce = 30f;             // 상승 중 위로 가하는 힘(연속 Force)
    [SerializeField] private float maxLiftSpeed = 10f;           // 상승 중 최고 상승 속도 제한
    [SerializeField] private float freeFallExtra = 5f; // 연결 직후 허용할 추가 여유 길이
    [SerializeField] private float detachLiftForce = 12f;  // 그래플 해제 순간 위로 튕겨주는 힘
    private float ropeLockDistance = 0f;



    [Header("낙하 속도 상한")]
    [SerializeField] private float maxFallSpeed = 10f; //
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
        //CheckLineBlocked();   // 항상 실행

        line.SetPosition(0, transform.position);

        if (isAttach && joint2D.enabled)
        {
            HandleAttachedState();  // 연결된 상태에서의 처리
        }
        else
        {
            HandleDetachedState();  // 연결되지 않은 상태에서의 처리
        }

    }
    void FixedUpdate()
    {

        var v = rb.linearVelocity;
        // 아래(음수)로 너무 빠르면 잘라내기
        if (v.y < -maxFallSpeed) v.y = -maxFallSpeed;

        rb.linearVelocity = v;
    }

    private Vector2 GetAnchorWorld()
    {
        if (!joint2D) return transform.position; // 방어
        if (joint2D.connectedBody)
            return joint2D.connectedBody.transform.TransformPoint(joint2D.connectedAnchor);
        else
            return joint2D.connectedAnchor; // world-space when connectedBody == null
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

    void Jump()
    {
        if (isGrounded)
        {
            // 수직속도 리셋 후 점프 (보다 일관된 점프감)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
    }
    private void CheckLineBlocked() // 줄이 장애물에 막혔는지 검사
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
        if (!joint2D || !line) return;
        if (!joint2D.enabled) return;

        Vector2 anchorPos = GetAnchorWorld();

        bool clickReleased = !Input.GetKey(KeyCode.Mouse0);
        bool hitGround = isGrounded;
        bool forceDetach = anchorPos.x < transform.position.x;

        // 해제 조건
        if (hitGround || forceDetach)
        {
            if (forceDetach)
                rb.AddForce(Vector2.up * detachLiftForce, ForceMode2D.Impulse);

            ReleaseGrapple();
            ReturnHook();
            return;
        }

        // 🔥 좌클릭 유지 중 → 줄 길이 고정
        joint2D.maxDistanceOnly = false;  // distance 고정을 허용하도록 설정
        joint2D.distance = ropeLockDistance;

        // 비주얼 업데이트
        line.SetPosition(1, GetVisualAnchor());
    }


    private void HandleDetachedState()
    {
        // 1) 입력 처리
        if (Input.GetKeyDown(KeyCode.Mouse0) && !isHookActive)
        {
            if (isGrounded) Jump();
            else StartHookShot();
        }

        // 2) 훅 이동/상태 갱신
        if (isHookActive && !isAttach)
        {
            if (isLineMax || isGrounded) ReturnHook();
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
        Vector2 origin = transform.position;
        Vector2 dir = launchDir.normalized; // (1,1).normalized

        float nextDist = Mathf.Min(hookDist + hookSpeed * Time.deltaTime, maxGrappleDistance);

        // self-hit 방지용 스킨
        const float skin = 0.05f;
        Vector2 start = origin + dir * skin;
        float rayLen = Mathf.Max(0.001f, nextDist - skin);

        RaycastHit2D hit = Physics2D.Raycast(start, dir, rayLen, grappleLayer);

        if (hit.collider != null)
        {
            float hitDistance = Vector2.Distance(origin, hit.point);
            if (hitDistance < minGrappleDistance)
            {
                isLineMax = true;
                return;
            }

            // 정적/동적 앵커 세팅
            joint2D.autoConfigureConnectedAnchor = false;
            joint2D.autoConfigureDistance = false;
            joint2D.enableCollision = true;

            if (hit.rigidbody != null)
            {
                joint2D.connectedBody = hit.rigidbody;
                joint2D.connectedAnchor = hit.rigidbody.transform.InverseTransformPoint(hit.point);
            }
            else
            {
                joint2D.connectedBody = null;
                joint2D.connectedAnchor = hit.point;
            }

            // 🔥 핵심: freeFallExtra 제거하고 정확한 거리로 고정
            joint2D.distance = hitDistance;
            ropeLockDistance = hitDistance;

            joint2D.maxDistanceOnly = false;
            joint2D.enabled = true;

            isAttach = true;
            hook.position = hit.point;
            return;
        }


    NoHit:
        // 충돌 없음 → 계속 연장
        hookDist = nextDist;
        hook.position = origin + dir * hookDist;

        if (hookDist >= maxGrappleDistance)
            isLineMax = true;
    }


    private void ReturnHook()
    {
        isHookActive = false;
        isLineMax = false;
        hookDist = 0f;
        hook.gameObject.SetActive(false);
        line.enabled = false;
        hook.position = transform.position;
        Debug.Log("[ReturnHook]");
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
        Debug.Log("[ReleaseGrapple]");
    }
    private Vector2 GetVisualAnchor()
    {
        Vector2 anchorPos = GetAnchorWorld();
        Vector2 dirToAnchor = ((Vector2)transform.position - anchorPos);
        if (dirToAnchor.sqrMagnitude < 1e-6f) return anchorPos;
        return anchorPos + dirToAnchor.normalized * (-visualOffset);
    }
    public void OnDeath()
    {
        isDead = true;

        // 1. 입력 막기(위에서 isDead 체크로 해결)
        // 2. 그래플 줄 끊기, joint 비활성화 등
        //    Grappling 스크립트에 public 메서드 만들어 호출해도 됨
        //    ex) grappling.Detach();

        // 3. 낙하 연출용으로 약간 아래 방향 속도 주기 (선택)
        rb.linearVelocity = new Vector2(0, 0);
        rb.gravityScale = 3f; // 평소보다 조금 더 크게 주면 '쭉 떨어지는 느낌'

        // 4. 애니메이션이 있다면:
        // animator.SetTrigger("Die");
    }
}