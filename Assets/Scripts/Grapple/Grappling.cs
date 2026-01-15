using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grappling : MonoBehaviour
{
    [Header("컴포넌트 설정")]
    [SerializeField] private LineRenderer line;
    [SerializeField] private Transform hook;
    [SerializeField] private DistanceJoint2D joint2D;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private MapManager mapManager;

    [Header("점프")]
    [SerializeField] private float jumpForce = 15f;

    [Header("감지 대상 레이어")]
    [SerializeField] private LayerMask grappleLayer;

    [Header("줄 설정")]
    [SerializeField] private float maxGrappleDistance = 7f;
    [SerializeField] private float minGrappleDistance = 1f;
    [SerializeField] private float hookSpeed = 25f;
    [SerializeField] private float visualOffset = 0.05f;

    [Header("Force Detach Tuning")]
    [SerializeField]
    [Tooltip("플레이어 기준, 앵커가 이 거리만큼 뒤로 가야 강제 해제")]
    private float forceDetachBackOffset = 0.5f;

    [Header("스윙/상승 설정(현재는 스윙만 사용)")]
    [SerializeField] private float xProximityThreshold = 1.0f;  // (미사용)
    [SerializeField] private float ropeRetractSpeed = 4.0f;      // (미사용)
    [SerializeField] private float liftForce = 30f;              // (미사용)
    [SerializeField] private float maxLiftSpeed = 10f;           // (미사용)
    [SerializeField] private float freeFallExtra = 5f;           // (미사용)

    [Header("자동 해제 + 위로 부스트(높이 고정)")]
    [SerializeField] private float autoDetachDistance = 0.1f;
    [SerializeField] private float autoDetachBoostHeight = 2.0f;

    [Header("기존 forceDetach 해제 + 위로 부스트(높이 고정)")]
    [SerializeField] private float forceDetachBoostHeight = 1.0f;

    [Header("좌클릭 쿨타임(그래플 이후)")]
    [SerializeField] private float leftClickCooldownAfterGrapple = 0.25f;

    private float leftClickCooldownTimer = 0f;

    [Header("점프 튜닝")]
    [SerializeField] public float jumpHeightMultiplier = 0.75f; // 1=기존, 0.75=25% 낮춤
    public float JumpHeightMultiplier => jumpHeightMultiplier;

    [SerializeField] public float jumpGravityMultiplier = 1.25f;
    public float JumpGravityMultiplier => jumpGravityMultiplier;

    [Header("낙하 속도 상한")]
    [SerializeField] private float maxFallSpeed = 15f;

    [Header("Chain End Offset")]
    [SerializeField] private float chainEndBackOffset = 0.15f;

    [Header("Hook Visual")]
    [SerializeField] private float hookAngleOffset = 0f;

    private bool pendingLeftClick = false;

    private bool isDead = false;
    private bool isGrounded;
    private bool isTensionHolding;

    private Vector2 launchDir = new Vector2(0.5f, 0.55f).normalized;
    private bool isHookActive;
    private bool isLineMax;
    private bool isAttach;
    private float hookDist = 0f;

    private float ropeLockDistance = 0f;

    private float baseJumpForce;
    private float baseGravity;
    private float baseHookSpeed;
    private float baseRopeRetractSpeed;
    private float baseLiftForce;
    private float baseMaxLiftSpeed;
    private float baseMaxFallSpeed;

    private float baseJumpHeight;

    private GrapplingVisual visual;
    private GrapplingScaling scaling;
    private GrapplingPhysics physicsComp;
    private GrapplingHookShot hookShot;

    public LineRenderer Line => line;
    public Transform Hook => hook;
    public DistanceJoint2D Joint2D => joint2D;
    public Rigidbody2D Rb => rb;
    public MapManager MapManager => mapManager;

    public LayerMask GrappleLayer => grappleLayer;

    public float MaxGrappleDistance => maxGrappleDistance;
    public float MinGrappleDistance => minGrappleDistance;

    public float HookSpeed { get => hookSpeed; set => hookSpeed = value; }
    public float VisualOffset => visualOffset;

    public float RopeRetractSpeed { get => ropeRetractSpeed; set => ropeRetractSpeed = value; }
    public float LiftForce { get => liftForce; set => liftForce = value; }
    public float MaxLiftSpeed { get => maxLiftSpeed; set => maxLiftSpeed = value; }

    public float MaxFallSpeed { get => maxFallSpeed; set => maxFallSpeed = value; }

    public float ChainEndBackOffset => chainEndBackOffset;
    public float HookAngleOffset => hookAngleOffset;

    public bool IsDead => isDead;
    public bool IsGrounded => isGrounded;

    public Vector2 LaunchDir => launchDir;

    public bool IsHookActive { get => isHookActive; set => isHookActive = value; }
    public bool IsLineMax { get => isLineMax; set => isLineMax = value; }
    public bool IsAttach { get => isAttach; set => isAttach = value; }

    public float HookDist { get => hookDist; set => hookDist = value; }

    public float RopeLockDistance { get => ropeLockDistance; set => ropeLockDistance = value; }
    public bool IsTensionHolding { get => isTensionHolding; set => isTensionHolding = value; }

    public float AutoDetachDistance => autoDetachDistance;
    public float AutoDetachBoostHeight => autoDetachBoostHeight;
    public float ForceDetachBoostHeight => forceDetachBoostHeight;

    public float LeftClickCooldownAfterGrapple => leftClickCooldownAfterGrapple;
    public float LeftClickCooldownTimer { get => leftClickCooldownTimer; set => leftClickCooldownTimer = value; }

    public float BaseJumpForce => baseJumpForce;
    public float BaseGravity => baseGravity;
    public float BaseHookSpeed => baseHookSpeed;
    public float BaseRopeRetractSpeed => baseRopeRetractSpeed;
    public float BaseLiftForce => baseLiftForce;
    public float BaseMaxLiftSpeed => baseMaxLiftSpeed;
    public float BaseMaxFallSpeed => baseMaxFallSpeed;

    public float BaseJumpHeight => baseJumpHeight;

    void Awake()
    {
        visual = GetComponent<GrapplingVisual>();
        scaling = GetComponent<GrapplingScaling>();
        physicsComp = GetComponent<GrapplingPhysics>();
        hookShot = GetComponent<GrapplingHookShot>();

        if (visual == null) visual = gameObject.AddComponent<GrapplingVisual>();
        if (scaling == null) scaling = gameObject.AddComponent<GrapplingScaling>();
        if (physicsComp == null) physicsComp = gameObject.AddComponent<GrapplingPhysics>();
        if (hookShot == null) hookShot = gameObject.AddComponent<GrapplingHookShot>();

        visual.Init(this);
        scaling.Init(this);
        physicsComp.Init(this);
        hookShot.Init(this);
    }

    void Start()
    {
        line.positionCount = 2;
        line.startWidth = 0.3f;
        line.endWidth = 0.3f;
        line.useWorldSpace = true;

        isHookActive = false;
        isAttach = false;
        isLineMax = false;

        line.enabled = false;
        hook.gameObject.SetActive(false);
        joint2D.enabled = false;

        baseJumpForce = jumpForce;
        baseGravity = rb.gravityScale;
        baseHookSpeed = hookSpeed;
        baseRopeRetractSpeed = ropeRetractSpeed;
        baseLiftForce = liftForce;
        baseMaxLiftSpeed = maxLiftSpeed;
        baseMaxFallSpeed = maxFallSpeed;

        float g0 = -Physics2D.gravity.y * baseGravity;
        float v0 = baseJumpForce / Mathf.Max(0.0001f, rb.mass);
        baseJumpHeight = (v0 * v0) / (2f * Mathf.Max(0.0001f, g0));
    }

    void Update()
    {
        if (isDead) return;

        // ✅ 잠금 중에도 GetKeyDown을 "저장"만 해둔다 (유실 방지)
        if (Input.GetKeyDown(KeyCode.Mouse0))
            pendingLeftClick = true;

        if (PlayerActionLock.IsLocked)
            return;

        if (GameManager.Instance != null && GameManager.Instance.IsInputLocked)
            return;

        if (leftClickCooldownTimer > 0f)
            leftClickCooldownTimer -= Time.deltaTime;

        ApplyMapSpeedScaling();

        line.SetPosition(0, transform.position);

        if (isAttach && joint2D.enabled) HandleAttachedState();
        else HandleDetachedState();

        UpdateHookVisual();
    }


    void FixedUpdate()
    {
        if (isDead) return;
        physicsComp.ClampFallSpeed();
    }

    private void UpdateHookVisual() => visual.UpdateHookVisual();
    private void ApplyMapSpeedScaling() => scaling.ApplyMapSpeedScaling();

    void Jump() => physicsComp.Jump();

    private void StartHookShot() => hookShot.StartHookShot();
    private void ShootHook() => hookShot.ShootHook();

    private void BeginLeftClickCooldown() => hookShot.BeginLeftClickCooldown();

    private void ReturnHook() => hookShot.ReturnHook();
    private void ReleaseGrapple() => physicsComp.ReleaseGrapple();

    private Vector2 GetAnchorWorld() => physicsComp.GetAnchorWorld();
    private void ApplyDetachBoostByHeight(float boostHeight) => physicsComp.ApplyDetachBoostByHeight(boostHeight);

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

    private void HandleAttachedState()
    {
        if (!joint2D || !joint2D.enabled) return;

        Vector2 anchorPos = GetAnchorWorld();
        float distToAnchor = Vector2.Distance(transform.position, anchorPos);

        bool hitGround = isGrounded;

        // 앵커가 플레이어보다 "조금 더 뒤"에 갔을 때 해제
        bool forceDetach = anchorPos.x < (transform.position.x - forceDetachBackOffset);

        if (hitGround || forceDetach)
        {
            if (forceDetach)
            {
                ApplyDetachBoostByHeight(forceDetachBoostHeight);
                BeginLeftClickCooldown();
            }

            ReleaseGrapple();
            ReturnHook();
            return;
        }

        // 좌클릭 상태에 따른 장력 제어
        bool holdingMouse = Input.GetKey(KeyCode.Mouse0);

        if (holdingMouse)
        {
            joint2D.maxDistanceOnly = false;
            joint2D.distance = ropeLockDistance;
            isTensionHolding = true;
        }
        else
        {
            if (isTensionHolding)
            {
                joint2D.distance = distToAnchor;
                isTensionHolding = false;
            }

            joint2D.maxDistanceOnly = false;
        }
    }

    private void HandleDetachedState()
    {
        bool canUseLeftClickForGrapple = (leftClickCooldownTimer <= 0f);

        if (pendingLeftClick && !isHookActive)
        {
            pendingLeftClick = false;

            if (isGrounded)
            {
                Jump();
            }
            else
            {
                if (canUseLeftClickForGrapple)
                    StartHookShot();
                else
                    Debug.Log("Grapple CoolTime!");
            }
        }

        if (isHookActive && !isAttach)
        {
            if (isLineMax || isGrounded) ReturnHook();
            else ShootHook();
        }
    }


    public void OnDeath()
    {
        isDead = true;

        rb.linearVelocity = new Vector2(0, 0);
        rb.gravityScale = 3f;

        ReleaseGrapple();
    }

    public void ForceDetachForAttack()
    {
        isTensionHolding = false;

        ReleaseGrapple();
    }
}
