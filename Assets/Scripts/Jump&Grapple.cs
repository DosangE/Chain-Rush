using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grappling : MonoBehaviour
{
    [Header("컴포넌트 설정")]
    [SerializeField] private LineRenderer line;                 // 연결 줄의 시각적 표현
    [SerializeField] private Transform hook;                    // 줄이 발사되고 도달할 위치(시각용)
    [SerializeField] private DistanceJoint2D joint2D;           // 물리적으로 연결을 담당
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private MapManager mapManager;

    [Header("점프")]
    [SerializeField] private float jumpForce = 15f;

    [Header("감지 대상 레이어")]
    [SerializeField] private LayerMask grappleLayer;            // Raycast로 감지할 레이어

    [Header("줄 설정")]
    [SerializeField] private float maxGrappleDistance = 7f;
    [SerializeField] private float minGrappleDistance = 1f;
    [SerializeField] private float hookSpeed = 25f;             // 줄 발사 속도(시각용 hook 이동)
    [SerializeField] private float visualOffset = 0.05f;        // 줄 길이 보정 값

    // 그래플 발사 입력 버퍼(원래 있던 변수들 유지: 현재 미사용)
    bool queuedGrapple = false;
    float queuedGrappleUntil = 0f;
    const float jumpBufferTime = 0.15f;

    [Header("스윙/상승 설정(현재는 스윙만 사용)")]
    [SerializeField] private float xProximityThreshold = 1.0f;  // (미사용)
    [SerializeField] private float ropeRetractSpeed = 4.0f;      // (미사용)
    [SerializeField] private float liftForce = 30f;              // (미사용)
    [SerializeField] private float maxLiftSpeed = 10f;           // (미사용)
    [SerializeField] private float freeFallExtra = 5f;           // (미사용)

    private float ropeLockDistance = 0f;                         // 연결 직후 고정된 줄 길이

    [Header("자동 해제 + 위로 부스트(높이 고정)")]
    [Tooltip("플레이어가 앵커에 이 거리 이하로 가까워지면 자동 해제")]
    [SerializeField] private float autoDetachDistance = 0.9f;

    [Tooltip("autoDetach 해제 순간, 추가로 더 올라가게 할 높이(유닛). 중력/속도와 무관하게 일정해짐")]
    [SerializeField] private float autoDetachBoostHeight = 2.0f;

    [Header("기존 forceDetach 해제 + 위로 부스트(높이 고정)")]
    [Tooltip("forceDetach 해제 순간, 추가로 더 올라가게 할 높이(유닛)")]
    [SerializeField] private float forceDetachBoostHeight = 1.0f;

    [Header("낙하 속도 상한")]
    [SerializeField] private float maxFallSpeed = 15f;

    // 상태
    private bool isDead = false;
    private bool isGrounded;

    private Vector2 launchDir = new Vector2(0.5f, 0.6f).normalized;
    private bool isHookActive;
    private bool isLineMax;
    private bool isAttach;
    private float hookDist = 0f;

    // base values
    private float baseJumpForce;
    private float baseGravity;
    private float baseHookSpeed;
    private float baseRopeRetractSpeed;
    private float baseLiftForce;
    private float baseMaxLiftSpeed;
    private float baseMaxFallSpeed;

    // 점프 높이 고정용
    private float baseJumpHeight;

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

        // base 저장
        baseJumpForce = jumpForce;
        baseGravity = rb.gravityScale;
        baseHookSpeed = hookSpeed;
        baseRopeRetractSpeed = ropeRetractSpeed;
        baseLiftForce = liftForce;
        baseMaxLiftSpeed = maxLiftSpeed;
        baseMaxFallSpeed = maxFallSpeed;

        // 기준 점프 높이 계산(현재 세팅을 “정답 높이”로)
        float g0 = -Physics2D.gravity.y * baseGravity;
        float v0 = baseJumpForce / Mathf.Max(0.0001f, rb.mass);
        baseJumpHeight = (v0 * v0) / (2f * Mathf.Max(0.0001f, g0));
    }

    void Update()
    {
        if (isDead) return;

        // 필요하면 켜세요: 줄이 장애물에 끼면 자동 해제
        // CheckLineBlocked();

        ApplyMapSpeedScaling();

        line.SetPosition(0, transform.position);

        if (isAttach && joint2D.enabled)
        {
            HandleAttachedState();
        }
        else
        {
            HandleDetachedState();
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;

        var v = rb.linearVelocity;
        if (v.y < -maxFallSpeed) v.y = -maxFallSpeed;
        rb.linearVelocity = v;
    }

    private void ApplyMapSpeedScaling()
    {
        if (mapManager == null) return;

        float speedRatio = mapManager.currentMapSpeed / mapManager.baseMapSpeed;

        // 점프/낙하감 압축용 중력 스케일
        rb.gravityScale = baseGravity * Mathf.Pow(speedRatio, 0.60f);

        // 그래플 발사(시각/체감 템포)
        hookSpeed = baseHookSpeed * speedRatio;

        // (현재 스윙만 쓰므로 아래 3개는 안 써도 되지만, 기존 변수 유지 차원에서 남겨둠)
        ropeRetractSpeed = baseRopeRetractSpeed * speedRatio;
        liftForce = baseLiftForce * speedRatio;
        maxLiftSpeed = baseMaxLiftSpeed * speedRatio;

        // 낙하 상한
        maxFallSpeed = baseMaxFallSpeed * speedRatio;
    }

    private float GetGravityAccel()
    {
        // g의 "크기"(양수)
        float g = -Physics2D.gravity.y * rb.gravityScale;
        return Mathf.Max(0.0001f, g);
    }

    /// <summary>
    /// 해제 순간 "추가로 더 올라갈 높이"를 보장하는 임펄스 적용.
    /// 현재 y속도(vy)와 중력(g)을 고려해 필요한 Δv를 계산한다.
    /// </summary>
    private void ApplyDetachBoostByHeight(float boostHeight)
    {
        if (boostHeight <= 0f) return;

        float g = GetGravityAccel();
        float vy = rb.linearVelocity.y;

        // 목표: 현재 상태에서 추가로 boostHeight만큼 더 올라가게 만드는 y속도 증가
        // vTarget^2 = vy^2 + 2 g h
        float vTarget = Mathf.Sqrt((vy * vy) + 2f * g * boostHeight);
        float deltaVy = vTarget - vy;

        // 이미 충분히 위로 가고 있으면(또는 계산상 음수면) 추가로 줄 필요 없음
        if (deltaVy <= 0f) return;

        float impulse = rb.mass * deltaVy;
        rb.AddForce(Vector2.up * impulse, ForceMode2D.Impulse);
    }

    private Vector2 GetAnchorWorld()
    {
        if (!joint2D) return transform.position;
        if (joint2D.connectedBody)
            return joint2D.connectedBody.transform.TransformPoint(joint2D.connectedAnchor);
        else
            return joint2D.connectedAnchor;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = true;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = false;
    }

    void Jump()
    {
        if (!isGrounded) return;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

        float g = GetGravityAccel();
        float desiredV0 = Mathf.Sqrt(2f * g * baseJumpHeight);
        float impulse = desiredV0 * rb.mass;

        jumpForce = impulse; // 디버깅용
        rb.AddForce(Vector2.up * impulse, ForceMode2D.Impulse);
    }

    private void CheckLineBlocked()
    {
        Vector2 targetPos;

        if (isAttach && joint2D.enabled)
        {
            targetPos = GetAnchorWorld();
        }
        else if (isHookActive)
        {
            targetPos = hook.position;
        }
        else
        {
            return;
        }

        Vector2 dir = targetPos - (Vector2)transform.position;
        float distance = dir.magnitude;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir.normalized, distance, grappleLayer);

        if (hit.collider != null &&
            hit.collider.attachedRigidbody != joint2D.connectedBody &&
            hit.collider.gameObject != gameObject)
        {
            Debug.Log($"[LineBlocked]  {hit.collider.name} 끼어듦 → 즉시 해제");
            ReleaseGrapple();
            ReturnHook();
        }
    }

    private void HandleAttachedState()
    {
        if (!joint2D || !line) return;
        if (!joint2D.enabled) return;

        Vector2 anchorPos = GetAnchorWorld();

        float distToAnchor = Vector2.Distance(transform.position, anchorPos);
        bool autoDetach = distToAnchor <= autoDetachDistance;

        bool clickReleased = !Input.GetKey(KeyCode.Mouse0);
        bool hitGround = isGrounded;
        bool forceDetach = anchorPos.x < transform.position.x;

        // 0) 클릭 놓기: 그냥 해제 (부스트 없음)
        if (clickReleased)
        {
            ReleaseGrapple(); ReturnHook(); return;
        }

        // 1) autoDetach: 부스트 높이 고정 적용
        if (autoDetach)
        {
            ApplyDetachBoostByHeight(autoDetachBoostHeight);
            ReleaseGrapple(); ReturnHook(); return;
        }

        // 2) 기존 해제: forceDetach일 때만(원하면 hitGround도 부스트 줄 수 있음)
        if (hitGround || forceDetach)
        {
            if (forceDetach)
                ApplyDetachBoostByHeight(forceDetachBoostHeight);

            ReleaseGrapple(); ReturnHook(); return;
        }

        // === 스윙: 줄 길이 고정 ===
        joint2D.maxDistanceOnly = false;
        joint2D.distance = ropeLockDistance;

        // 비주얼 업데이트
        line.SetPosition(1, GetVisualAnchor());
    }

    private void HandleDetachedState()
    {
        // 입력 처리
        if (Input.GetKeyDown(KeyCode.Mouse0) && !isHookActive)
        {
            if (isGrounded) Jump();
            else StartHookShot();
        }

        // 훅 이동/상태 갱신
        if (isHookActive && !isAttach)
        {
            if (isLineMax || isGrounded) ReturnHook();
            else ShootHook();
        }

        if (line.enabled)
            line.SetPosition(1, hook.position);
    }

    private void StartHookShot()
    {
        hook.SetParent(null);
        hook.position = transform.position;

        isHookActive = true;
        isLineMax = false;
        isAttach = false;

        hookDist = 0f;

        hook.gameObject.SetActive(true);
        line.enabled = true;
    }

    private void ShootHook()
    {
        Vector2 origin = transform.position;
        Vector2 dir = launchDir.normalized;

        float nextDist = Mathf.Min(hookDist + hookSpeed * Time.deltaTime, maxGrappleDistance);

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

            // 연결 직후 줄 길이 고정
            joint2D.distance = hitDistance;
            ropeLockDistance = hitDistance;

            joint2D.maxDistanceOnly = false;
            joint2D.enabled = true;

            isAttach = true;
            hook.position = hit.point;
            return;
        }

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

        rb.linearVelocity = new Vector2(0, 0);
        rb.gravityScale = 3f;

        ReleaseGrapple();
    }
}
