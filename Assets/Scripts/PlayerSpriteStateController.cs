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
    [SerializeField] private Sprite[] attackOutFrames;
    [SerializeField] private float attackOutFrameTime = 0.06f;
    [SerializeField] private Sprite attackOutSprite;

    [Header("Attack Return (come back)")]
    [SerializeField] private float attackReturnFrameTime = 0.06f;

    [Header("Jump Frames")]
    [SerializeField] private Sprite[] jumpFrames;
    [SerializeField] private float jumpFrameTime = 0.06f;

    [Header("Optional: Jump Up/Down")]
    [SerializeField] private Sprite jumpUpSprite;
    [SerializeField] private Sprite jumpDownSprite;
    [SerializeField] private float jumpUpDownThreshold = 0.05f;

    // ✅ 점프에서만 쓰는 오프셋
    [Header("Jump Only Offset")]
    [SerializeField] private Vector2 jumpOnlyOffset = Vector2.zero;

    private Vector3 spriteOriginalLocalPos;

    private VisualState currentState = VisualState.Run;

    private bool isAttackOut = false;
    private bool isAttackReturn = false;

    private Coroutine outCo;
    private Coroutine returnCo;

    private Coroutine jumpCo;
    private bool isJumpAnimating = false;

    // ✅ 이번 프레임 최종적으로 적용할 오프셋 여부 (LateUpdate에서만 실제 적용)
    private bool wantsJumpOffsetThisFrame = false;

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

        spriteOriginalLocalPos = spriteRenderer.transform.localPosition;
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

        ResetSpriteOffset();
    }

    private void Update()
    {
        if (grappling == null || spriteRenderer == null) return;

        // ✅ 매 프레임 기본값: 오프셋 안 씀 (LateUpdate에서 이 값으로 결정)
        wantsJumpOffsetThisFrame = false;

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

        bool isGrounded = grappling.IsGrounded;

        if (!isGrounded)
        {
            // jumpFrames 있으면 코루틴
            if (!isJumpAnimating && jumpFrames != null && jumpFrames.Length > 0)
                StartJumpAnimation();

            ApplyState(VisualState.Jump);

            // ✅ 점프일 때만 오프셋 요청
            wantsJumpOffsetThisFrame = true;
        }
        else
        {
            StopJumpAnimation();
            ApplyState(VisualState.Run);
        }
    }

    private void LateUpdate()
    {
        // ✅ 스프라이트 변경이 모두 끝난 뒤 최종적으로 위치만 결정
        if (wantsJumpOffsetThisFrame)
            ApplyJumpOnlyOffset();
        else
            ResetSpriteOffset();
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

    private void ApplyJumpOnlyOffset()
    {
        spriteRenderer.transform.localPosition =
            spriteOriginalLocalPos + (Vector3)jumpOnlyOffset;
    }

    private void ResetSpriteOffset()
    {
        spriteRenderer.transform.localPosition = spriteOriginalLocalPos;
    }
}
