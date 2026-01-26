using System.Collections;
using UnityEngine;

public class PlayerSpriteStateController : MonoBehaviour
{
    public enum VisualState { Run, Jump, Grapple, AttackOut, AttackReturn }

    [Header("Refs")]
    [SerializeField] private Grappling grappling;
    [SerializeField] private PlayerAttack playerAttack;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Run (your running looper)")]
    [SerializeField] private PlayerSpriteAnimator runAnimator;

    [Header("Sprites - Basic")]
    [SerializeField] private Sprite jumpSprite;
    [SerializeField] private Sprite grappleSprite;

    [Header("Attack Out (fly to target)")]
    [Tooltip("AttackOut도 프레임으로 돌리고 싶으면 여기에 넣어라. 비워두면 attackOutSprite(단일) 사용.")]
    [SerializeField] private Sprite[] attackOutFrames;
    [Tooltip("AttackOut 한 프레임당 시간(초). 예: 0.06f")]
    [SerializeField] private float attackOutFrameTime = 0.06f;
    [SerializeField] private Sprite attackOutSprite;

    [Header("Attack Return (come back)")]
    [Tooltip("AttackReturn 한 프레임당 시간(초). 예: 0.06f")]
    [SerializeField] private float attackReturnFrameTime = 0.06f;

    [Header("Jump Frames")]
    [Tooltip("점프를 프레임(예: 11장)으로 돌리고 싶으면 여기에 넣어라. 비워두면 아래 Jump Up/Down 또는 jumpSprite 사용.")]
    [SerializeField] private Sprite[] jumpFrames;   // 11장
    [SerializeField] private float jumpFrameTime = 0.06f;

    [Header("Optional: Jump Up/Down (jumpFrames 비었을 때만 사용)")]
    [SerializeField] private Sprite jumpUpSprite;
    [SerializeField] private Sprite jumpDownSprite;
    [SerializeField] private float jumpUpDownThreshold = 0.05f;

    private VisualState currentState = VisualState.Run;

    private bool isAttackOut = false;
    private bool isAttackReturn = false;

    private Coroutine outCo;
    private Coroutine returnCo;

    private Coroutine jumpCo;
    private bool isJumpAnimating = false;

    private void Reset()
    {
        grappling = GetComponentInParent<Grappling>();
        playerAttack = GetComponentInParent<PlayerAttack>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        runAnimator = GetComponent<PlayerSpriteAnimator>();
    }

    private void Awake()
    {
        if (grappling == null) grappling = GetComponentInParent<Grappling>();
        if (playerAttack == null) playerAttack = GetComponentInParent<PlayerAttack>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (runAnimator == null) runAnimator = GetComponent<PlayerSpriteAnimator>();
    }

    private void OnEnable()
    {
        if (playerAttack != null)
        {
            playerAttack.OnAttackOutStart += HandleAttackOutStart;
            playerAttack.OnAttackOutEnd += HandleAttackOutEnd;
            playerAttack.OnAttackReturnStart += HandleAttackReturnStart;
            playerAttack.OnAttackReturnEnd += HandleAttackReturnEnd;
        }
    }

    private void OnDisable()
    {
        if (playerAttack != null)
        {
            playerAttack.OnAttackOutStart -= HandleAttackOutStart;
            playerAttack.OnAttackOutEnd -= HandleAttackOutEnd;
            playerAttack.OnAttackReturnStart -= HandleAttackReturnStart;
            playerAttack.OnAttackReturnEnd -= HandleAttackReturnEnd;
        }

        StopJumpAnimation();
        if (outCo != null) { StopCoroutine(outCo); outCo = null; }
        if (returnCo != null) { StopCoroutine(returnCo); returnCo = null; }
    }

    private void Update()
    {
        if (grappling == null || spriteRenderer == null) return;

        // ✅ 최우선: 공격 연출 (Return > Out)
        if (isAttackReturn)
        {
            StopJumpAnimation();
            ApplyState(VisualState.AttackReturn);
            return;
        }
        if (isAttackOut)
        {
            StopJumpAnimation();
            ApplyState(VisualState.AttackOut);
            return;
        }

        bool isAttach = grappling.IsAttach;
        bool isHookActive = grappling.IsHookActive;
        bool isGrounded = grappling.IsGrounded;

        bool isGrapplingAny = (isHookActive || isAttach);

        // ✅ 핵심: 공중이면(점프든 그래플링이든) Jump 비주얼을 쓴다.
        if (!isGrounded)
        {
            // jumpFrames가 있으면 점프 프레임 코루틴을 돌린다 (그래플링 중에도 유지)
            if (!isJumpAnimating && jumpFrames != null && jumpFrames.Length > 0)
                StartJumpAnimation();

            ApplyState(VisualState.Jump);
        }
        else
        {
            StopJumpAnimation();
            ApplyState(VisualState.Run);
        }

        // 참고:
        // grappleSprite를 "특정 조건에서만" 쓰고 싶다면 여기서 예외 처리하면 됨.
        // 예: isAttach일 때만 grappleSprite를 강제로 쓰고 싶다 -> ApplyState(Jump) 대신 Grapple로 보내거나,
        // ApplyState(Jump) 후 spriteRenderer.sprite만 overwrite 하는 식으로.
    }

    private void HandleAttackOutStart()
    {
        isAttackOut = true;

        StopJumpAnimation();

        if (attackOutFrames != null && attackOutFrames.Length > 0)
        {
            if (outCo != null) StopCoroutine(outCo);
            outCo = StartCoroutine(CoLoopAttackOutFrames());
        }

        ApplyState(VisualState.AttackOut);
    }

    private void HandleAttackOutEnd()
    {
        isAttackOut = false;

        if (outCo != null)
        {
            StopCoroutine(outCo);
            outCo = null;
        }
    }

    private void HandleAttackReturnStart()
    {
        isAttackReturn = true;

        StopJumpAnimation();

        // (네 기존 코드 유지) Return도 attackOutFrames로 루프
        if (attackOutFrames != null && attackOutFrames.Length > 0)
        {
            if (returnCo != null) StopCoroutine(returnCo);
            returnCo = StartCoroutine(CoLoopAttackReturnFrames());
        }

        ApplyState(VisualState.AttackReturn);
    }

    private void HandleAttackReturnEnd()
    {
        isAttackReturn = false;

        if (returnCo != null)
        {
            StopCoroutine(returnCo);
            returnCo = null;
        }
    }

    private void ApplyState(VisualState next)
    {
        if (currentState == next) return;
        currentState = next;

        if (runAnimator != null)
            runAnimator.enabled = (next == VisualState.Run);

        if (next != VisualState.AttackOut && outCo != null)
        {
            StopCoroutine(outCo);
            outCo = null;
        }

        if (next != VisualState.AttackReturn && returnCo != null)
        {
            StopCoroutine(returnCo);
            returnCo = null;
        }

        switch (next)
        {
            case VisualState.Run:
                break;

            case VisualState.Jump:
                // jumpFrames가 있으면 코루틴이 관리
                if (jumpFrames == null || jumpFrames.Length == 0)
                    SetJumpSprite();
                break;

            case VisualState.Grapple:
                if (grappleSprite != null)
                    spriteRenderer.sprite = grappleSprite;
                break;

            case VisualState.AttackOut:
                if ((attackOutFrames == null || attackOutFrames.Length == 0) && attackOutSprite != null)
                    spriteRenderer.sprite = attackOutSprite;
                break;

            case VisualState.AttackReturn:
                break;
        }
    }

    private void SetJumpSprite()
    {
        if (jumpUpSprite != null || jumpDownSprite != null)
        {
            float vy = grappling.Rb != null ? grappling.Rb.linearVelocity.y : 0f;

            if (vy > jumpUpDownThreshold && jumpUpSprite != null)
                spriteRenderer.sprite = jumpUpSprite;
            else if (vy < -jumpUpDownThreshold && jumpDownSprite != null)
                spriteRenderer.sprite = jumpDownSprite;
            else if (jumpSprite != null)
                spriteRenderer.sprite = jumpSprite;
            else if (jumpUpSprite != null)
                spriteRenderer.sprite = jumpUpSprite;
            else if (jumpDownSprite != null)
                spriteRenderer.sprite = jumpDownSprite;

            return;
        }

        if (jumpSprite != null)
            spriteRenderer.sprite = jumpSprite;
    }

    private void StartJumpAnimation()
    {
        if (jumpFrames == null || jumpFrames.Length == 0) return;

        isJumpAnimating = true;

        if (jumpCo != null)
            StopCoroutine(jumpCo);

        jumpCo = StartCoroutine(CoLoopJumpFrames());
    }

    private void StopJumpAnimation()
    {
        if (!isJumpAnimating) return;

        isJumpAnimating = false;

        if (jumpCo != null)
        {
            StopCoroutine(jumpCo);
            jumpCo = null;
        }
    }

    private IEnumerator CoLoopAttackOutFrames()
    {
        if (runAnimator != null) runAnimator.enabled = false;

        float frameTime = Mathf.Max(0.0001f, attackOutFrameTime);
        int idx = 0;

        while (true)
        {
            if (attackOutFrames == null || attackOutFrames.Length == 0)
                yield break;

            spriteRenderer.sprite = attackOutFrames[idx];
            idx = (idx + 1) % attackOutFrames.Length;
            yield return new WaitForSeconds(frameTime);
        }
    }

    private IEnumerator CoLoopAttackReturnFrames()
    {
        if (runAnimator != null) runAnimator.enabled = false;

        float frameTime = Mathf.Max(0.0001f, attackReturnFrameTime);
        int idx = 0;

        while (true)
        {
            if (attackOutFrames == null || attackOutFrames.Length == 0)
                yield break;

            spriteRenderer.sprite = attackOutFrames[idx];
            idx = (idx + 1) % attackOutFrames.Length;
            yield return new WaitForSeconds(frameTime);
        }
    }

    private IEnumerator CoLoopJumpFrames()
    {
        if (runAnimator != null)
            runAnimator.enabled = false;

        float frameTime = Mathf.Max(0.0001f, jumpFrameTime);
        int idx = 0;

        while (true)
        {
            if (jumpFrames == null || jumpFrames.Length == 0)
                yield break;

            spriteRenderer.sprite = jumpFrames[idx];
            idx = (idx + 1) % jumpFrames.Length;
            yield return new WaitForSeconds(frameTime);
        }
    }
}
