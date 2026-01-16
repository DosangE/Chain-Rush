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
    [SerializeField] private LayerMask attackableMask;
    [Tooltip("마우스가 콜라이더 위에 있을 때만 공격. 커서 판정 여유(월드 단위). 0이면 딱 찍어야 함.")]
    [SerializeField] private float cursorHitRadius = 0f;

    [Header("Target Offset")]
    [SerializeField] private float enemyTargetOffsetX = -2f;

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
    [SerializeField] private float missCooldown = 0.5f;

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

    [Header("Boss Attack Rules")]
    [Tooltip("보스 QTE 중이거나 쿨타임이면, 보스 공격 시도 자체를 막음(보스만).")]
    [SerializeField] private bool blockBossAttackDuringCooldownOrQTE = true;

    private bool isAttacking;
    private float nextAttackAllowedTime;

    private int bossHitCredit = 0;

    public void GrantBossHitCredit(int amount)
    {
        if (amount <= 0) return;

        bossHitCredit = 1;
    }


    private bool ConsumeBossHitCredit()
    {
        if (bossHitCredit <= 0) return false;
        bossHitCredit--;
        return true;
    }

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
        if (GameManager.Instance != null && GameManager.Instance.IsInputLocked)
            return;

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

        if (hitCol == null)
        {
            if (missCooldown > 0f)
                nextAttackAllowedTime = Time.time + missCooldown;
            yield break;
        }

        // ===== Boss 분기 =====
        Boss boss = hitCol.GetComponentInParent<Boss>();
        if (boss != null)
        {
            // ✅ QTE 중이면 보스 공격 시도 자체 차단
            if (blockBossAttackDuringCooldownOrQTE && boss.IsInQTE)
                yield break;

            // ✅ 공격권 없으면 보스는 공격 불가
            if (!ConsumeBossHitCredit())
                yield break;

            isAttacking = true;
            PlayerActionLock.Lock();

            if (grappling != null)
                grappling.ForceDetachForAttack();

            Vector2 bossPoint = (Vector2)boss.transform.position;
            bossPoint.x += enemyTargetOffsetX;

            float startX = transform.position.x;

            bool prevSimulated = true;
            bool prevColliderEnabled = true;

            if (playerRb != null)
            {
                prevSimulated = playerRb.simulated;
                playerRb.linearVelocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
                playerRb.simulated = false;
            }

            if (playerCollider != null)
            {
                prevColliderEnabled = playerCollider.enabled;
                playerCollider.enabled = false;
            }

            AttackHookOn();
            yield return ChainShootVisual(bossPoint, chainShootDuration);

            Vector3 from = transform.position;
            yield return MovePlayerKeepingChain(from, bossPoint, flyOutDuration, bossPoint);

            // ✅ 보스 HP 1 감소(공격권 1회 사용)
            boss.OnHitByAttack();
            if (useHitSlowMo)
                yield return HitSlowMo(hitTimeScale, hitSlowMoDurationRealtime);
            AttackHookOff();
            ChainOff();

            // 복귀
            Vector2 returnPoint = new Vector2(startX, returnYWorld);
            if (returnDuration <= 0f)
            {
                transform.position = returnPoint;
            }
            else
            {
                yield return MovePlayerArcKeepingChain(transform.position, returnPoint, returnDuration, returnArcHeight, bossPoint);
            }

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
            yield break;
        }

        // ===== 이하 Enemy/Obstacle 기존 로직 그대로 =====
        isAttacking = true;
        PlayerActionLock.Lock();

        if (grappling != null)
            grappling.ForceDetachForAttack();

        Enemy enemy = hitCol.GetComponentInParent<Enemy>();
        Vector2 targetPoint = (enemy != null) ? (Vector2)enemy.transform.position : (Vector2)hitCol.transform.position;
        targetPoint.x += enemyTargetOffsetX;

        float startX2 = transform.position.x;

        bool prevSimulated2 = true;
        if (playerRb != null)
        {
            prevSimulated2 = playerRb.simulated;
            playerRb.linearVelocity = Vector2.zero;
            playerRb.angularVelocity = 0f;
            playerRb.simulated = false;
        }

        bool prevColliderEnabled2 = true;
        if (playerCollider != null)
        {
            prevColliderEnabled2 = playerCollider.enabled;
            playerCollider.enabled = false;
        }

        AttackHookOn();
        yield return ChainShootVisual(targetPoint, chainShootDuration);

        Vector3 from2 = transform.position;
        yield return MovePlayerKeepingChain(from2, targetPoint, flyOutDuration, targetPoint);

        if (enemy != null) enemy.OnHitByAttack();
        else Destroy(hitCol.gameObject);

        AttackHookOff();
        ChainOff();

        if (useHitSlowMo)
            yield return HitSlowMo(hitTimeScale, hitSlowMoDurationRealtime);

        yield return HoldWithChain(targetPoint, hitHoldDuration);

        Vector2 returnPoint2 = new Vector2(startX2, returnYWorld);
        if (returnDuration <= 0f) transform.position = returnPoint2;
        else yield return MovePlayerArcKeepingChain(transform.position, returnPoint2, returnDuration, returnArcHeight, targetPoint);

        ChainOff();
        AttackHookOff();

        if (playerCollider != null)
            playerCollider.enabled = prevColliderEnabled2;

        if (playerRb != null)
        {
            playerRb.simulated = prevSimulated2;
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

            Vector2 basePos = Vector2.Lerp(aFrom, aTo, eased);

            float parabola = 4f * eased * (1f - eased);
            Vector2 arcOffset = Vector2.up * (arcHeight * parabola);

            Vector2 finalPos = basePos + arcOffset;
            transform.position = finalPos;

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
    public bool HasBossHitCredit()
    {
        return bossHitCredit > 0;
    }

}
