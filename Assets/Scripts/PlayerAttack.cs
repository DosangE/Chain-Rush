using System.Collections;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private Transform chainOrigin;
    [SerializeField] private LineRenderer chainLine;
    [SerializeField] private Grappling grappling;

    [Header("Player Components")]
    [SerializeField] private Rigidbody2D playerRb;
    [SerializeField] private Collider2D playerCollider;

    [Header("Attack Targeting")]
    [SerializeField] private LayerMask attackableMask;   // Enemy/Obstacle 레이어 포함
    [Tooltip("마우스가 콜라이더 위에 있을 때만 공격. 커서 판정 여유(월드 단위). 0이면 딱 찍어야 함.")]
    [SerializeField] private float cursorHitRadius = 0f; // 0이면 OverlapPoint, >0이면 OverlapCircle로 여유

    [Header("Target Offset")]
    [SerializeField] private float enemyTargetOffsetX = -2f; // +면 오른쪽, -면 왼쪽

    [Header("Fly To Target (Player Moves)")]
    [SerializeField] private float flyOutDuration = 0.10f;
    [SerializeField] private float hitHoldDuration = 0.06f;

    [Header("Return After Attack (Wall Kick Feel)")]
    [Tooltip("공격 종료 후 무조건 돌아갈 월드 Y 좌표")]
    [SerializeField] private float returnYWorld = 2.0f;
    [Tooltip("복귀 이동 시간(연출용). 0이면 즉시 텔레포트")]
    [SerializeField] private float returnDuration = 0.10f;
    [Tooltip("복귀할 때 호(arc)의 높이. 0이면 직선과 동일")]
    [SerializeField] private float returnArcHeight = 2.0f;
    [Tooltip("복귀 직후 플레이어 속도. (0,0)이면 완전 정지")]
    [SerializeField] private Vector2 postReturnVelocity = Vector2.zero;

    [SerializeField] private AnimationCurve flyEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Chain Visual")]
    [SerializeField] private float chainShootDuration = 0.06f;

    [Header("Cooldown")]
    [SerializeField] private float missCooldown = 0.5f; // 필요하면 미스에도 쿨타임

    [Header("Hit Emphasis (Slow Motion)")]
    [SerializeField] private bool useHitSlowMo = true;
    [SerializeField] private float hitTimeScale = 0.5f;
    [SerializeField] private float hitSlowMoDurationRealtime = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool debugDrawCursor = false;
    [SerializeField] private float debugRayDuration = 0.2f;

    [Header("Attack Hook Visual (Use Grappling Hook)")]
    [SerializeField] private bool showHookDuringAttack = true;
    [SerializeField] private float attackHookAngleOffset = 0f;

    private bool isAttacking;
    private float nextAttackAllowedTime;

    private void Reset()
    {
        cam = Camera.main;
        chainOrigin = transform;
        playerRb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
    }

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (chainOrigin == null) chainOrigin = transform;
        if (playerRb == null) playerRb = GetComponent<Rigidbody2D>();
        if (playerCollider == null) playerCollider = GetComponent<Collider2D>();

        if (chainLine != null)
        {
            chainLine.useWorldSpace = true;
            chainLine.positionCount = 2;
            chainLine.enabled = false;
        }
    }

    private void Update()
    {
        if (PlayerActionLock.IsLocked) return;

        if (Input.GetKeyDown(KeyCode.Space))
            TryAttack();
    }

    public void TryAttack()
    {
        if (isAttacking) return;
        if (Time.time < nextAttackAllowedTime) return;

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        // ===== 0) 마우스가 "콜라이더 안"에 있을 때만 공격 =====
        Vector2 origin = (Vector2)chainOrigin.position;

        Vector3 mp = Input.mousePosition;
        if (cam != null) mp.z = -cam.transform.position.z;
        Vector2 mouseWorld = (cam != null) ? (Vector2)cam.ScreenToWorldPoint(mp) : origin;

        if (debugDrawCursor)
        {
            Debug.DrawLine(mouseWorld + Vector2.left * 0.15f, mouseWorld + Vector2.right * 0.15f, Color.yellow, debugRayDuration);
            Debug.DrawLine(mouseWorld + Vector2.up * 0.15f, mouseWorld + Vector2.down * 0.15f, Color.yellow, debugRayDuration);
        }

        Collider2D hitCol = null;
        if (cursorHitRadius <= 0f) hitCol = Physics2D.OverlapPoint(mouseWorld, attackableMask);
        else hitCol = Physics2D.OverlapCircle(mouseWorld, cursorHitRadius, attackableMask);

        // 미스면 아무것도 안 함
        if (hitCol == null)
        {
            if (missCooldown > 0f)
                nextAttackAllowedTime = Time.time + missCooldown;
            yield break;
        }

        // ===== 1) 맞았을 때만 공격 시작 =====
        isAttacking = true;
        PlayerActionLock.Lock();

        // 2) 공격 시작 시 그래플 강제 해제
        if (grappling != null)
            grappling.ForceDetachForAttack();

        // 3) 붙는 지점: Enemy 중앙(우선) / 아니면 콜라이더 트랜스폼
        Enemy enemy = hitCol.GetComponentInParent<Enemy>();
        Vector2 targetPoint = (enemy != null) ? (Vector2)enemy.transform.position : (Vector2)hitCol.transform.position;
        targetPoint.x += enemyTargetOffsetX;

        // 4) "복귀용 X" 저장 (점프/그래플 상태 복구는 하지 않음)
        float startX = transform.position.x;

        // 5) 공격 중 물리/충돌 정지 (연출 안정화)
        bool prevSimulated = true;
        if (playerRb != null)
        {
            prevSimulated = playerRb.simulated;
            playerRb.linearVelocity = Vector2.zero;
            playerRb.angularVelocity = 0f;
            playerRb.simulated = false;
        }

        bool prevColliderEnabled = true;
        if (playerCollider != null)
        {
            prevColliderEnabled = playerCollider.enabled;
            playerCollider.enabled = false;
        }

        // 6) 훅/체인 ON + 발사 연출
        AttackHookOn();
        yield return ChainShootVisual(targetPoint, chainShootDuration);

        // 7) 플레이어가 타겟으로 이동
        Vector3 from = transform.position;
        yield return MovePlayerKeepingChain(from, targetPoint, flyOutDuration, targetPoint);

        // 8) 파괴
        if (enemy != null) enemy.OnHitByAttack();
        else Destroy(hitCol.gameObject);

        AttackHookOff();
        ChainOff();

        // 9) 슬로모
        if (useHitSlowMo)
            yield return HitSlowMo(hitTimeScale, hitSlowMoDurationRealtime);

        // 10) 홀드
        yield return HoldWithChain(targetPoint, hitHoldDuration);

        // ===== 11) 공격 종료 후: 무조건 (startX, returnYWorld)로 "호(arc)" 복귀 =====
        Vector2 returnPoint = new Vector2(startX, returnYWorld);

        if (returnDuration <= 0f)
        {
            transform.position = returnPoint;
        }
        else
        {
            // ★여기서 "호"로 이동
            yield return MovePlayerArcKeepingChain(transform.position, returnPoint, returnDuration, returnArcHeight, targetPoint);
        }

        // 12) 체인/훅 OFF
        ChainOff();
        AttackHookOff();

        // 13) 물리/충돌 복구 + "복귀 후 속도" 적용(점프 이어가기 제거)
        if (playerCollider != null)
            playerCollider.enabled = prevColliderEnabled;

        if (playerRb != null)
        {
            playerRb.simulated = prevSimulated;
            playerRb.linearVelocity = postReturnVelocity;
            playerRb.angularVelocity = 0f;
        }

        PlayerActionLock.Unlock();
        isAttacking = false;
    }

    private IEnumerator ChainShootVisual(Vector2 chainEnd, float duration)
    {
        if (chainLine == null) yield break;

        chainLine.enabled = true;
        chainLine.positionCount = 2;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = (duration <= 0f) ? 1f : Mathf.Clamp01(t / duration);

            Vector2 o = (Vector2)chainOrigin.position;
            Vector2 lerpedEnd = Vector2.Lerp(o, chainEnd, a);

            chainLine.SetPosition(0, o);
            chainLine.SetPosition(1, lerpedEnd);

            UpdateAttackHookVisual(o, lerpedEnd);
            yield return null;
        }

        Vector2 finalO = (Vector2)chainOrigin.position;
        chainLine.SetPosition(0, finalO);
        chainLine.SetPosition(1, chainEnd);
        UpdateAttackHookVisual(finalO, chainEnd);
    }

    private IEnumerator MovePlayerKeepingChain(Vector3 from, Vector3 to, float duration, Vector2 chainEnd)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = (duration <= 0f) ? 1f : Mathf.Clamp01(t / duration);
            float eased = (flyEase != null) ? flyEase.Evaluate(a) : a;

            transform.position = Vector3.Lerp(from, to, eased);

            if (chainLine != null && chainLine.enabled)
            {
                Vector2 o = (Vector2)chainOrigin.position;
                chainLine.SetPosition(0, o);
                chainLine.SetPosition(1, chainEnd);
                UpdateAttackHookVisual(o, chainEnd);
            }

            yield return null;
        }

        transform.position = to;

        if (chainLine != null && chainLine.enabled)
        {
            Vector2 o = (Vector2)chainOrigin.position;
            chainLine.SetPosition(0, o);
            chainLine.SetPosition(1, chainEnd);
            UpdateAttackHookVisual(o, chainEnd);
        }
    }

    // ★추가: 복귀를 "호(arc)"로 이동
    private IEnumerator MovePlayerArcKeepingChain(Vector3 from, Vector3 to, float duration, float arcHeight, Vector2 chainEnd)
    {
        float t = 0f;

        Vector2 aFrom = from;
        Vector2 aTo = to;

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = (duration <= 0f) ? 1f : Mathf.Clamp01(t / duration);
            float eased = (flyEase != null) ? flyEase.Evaluate(u) : u;

            // 기본 직선 보간
            Vector2 basePos = Vector2.Lerp(aFrom, aTo, eased);

            // 포물선 오프셋: 0->1에서 중간이 최대가 되도록(4t(1-t))
            float parabola = 4f * eased * (1f - eased);
            Vector2 arcOffset = Vector2.up * (arcHeight * parabola);

            Vector2 finalPos = basePos + arcOffset;
            transform.position = finalPos;

            // 체인/훅은 타겟에 계속 붙게(원래 코드 유지)
            if (chainLine != null && chainLine.enabled)
            {
                Vector2 o = (Vector2)chainOrigin.position;
                chainLine.SetPosition(0, o);
                chainLine.SetPosition(1, chainEnd);
                UpdateAttackHookVisual(o, chainEnd);
            }

            yield return null;
        }

        transform.position = to;

        if (chainLine != null && chainLine.enabled)
        {
            Vector2 o = (Vector2)chainOrigin.position;
            chainLine.SetPosition(0, o);
            chainLine.SetPosition(1, chainEnd);
            UpdateAttackHookVisual(o, chainEnd);
        }
    }

    private IEnumerator HoldWithChain(Vector2 chainEnd, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;

            if (chainLine != null && chainLine.enabled)
            {
                Vector2 o = (Vector2)chainOrigin.position;
                chainLine.SetPosition(0, o);
                chainLine.SetPosition(1, chainEnd);
                UpdateAttackHookVisual(o, chainEnd);
            }

            yield return null;
        }
    }

    private void ChainOff()
    {
        if (chainLine == null) return;
        chainLine.enabled = false;
    }

    private IEnumerator HitSlowMo(float scale, float durationRealtime)
    {
        float prevScale = Time.timeScale;
        float prevFixed = Time.fixedDeltaTime;

        Time.timeScale = Mathf.Clamp(scale, 0.01f, 1f);
        Time.fixedDeltaTime = prevFixed * Time.timeScale;

        yield return new WaitForSecondsRealtime(durationRealtime);

        Time.timeScale = prevScale;
        Time.fixedDeltaTime = prevFixed;
    }

    // ===== 공격용 훅 비주얼 =====
    private void AttackHookOn()
    {
        if (!showHookDuringAttack) return;
        if (grappling == null || grappling.Hook == null) return;

        if (!grappling.Hook.gameObject.activeSelf)
            grappling.Hook.gameObject.SetActive(true);
    }

    private void AttackHookOff()
    {
        if (grappling == null || grappling.Hook == null) return;

        if (grappling.Hook.gameObject.activeSelf)
            grappling.Hook.gameObject.SetActive(false);
    }

    private void UpdateAttackHookVisual(Vector2 chainStart, Vector2 chainEnd)
    {
        if (!showHookDuringAttack) return;
        if (grappling == null || grappling.Hook == null) return;

        grappling.Hook.position = chainEnd;

        Vector2 d = chainEnd - chainStart;
        if (d.sqrMagnitude < 1e-6f) d = Vector2.right;
        d.Normalize();

        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        grappling.Hook.rotation = Quaternion.Euler(0f, 0f, angle + attackHookAngleOffset);
    }
}
