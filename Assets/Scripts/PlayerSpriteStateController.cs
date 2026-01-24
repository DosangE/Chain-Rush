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
    [SerializeField] private float attackOutFps = 18f;
    [SerializeField] private Sprite attackOutSprite;

    [Header("Attack Return (back to start)")]
    [SerializeField] private Sprite[] attackReturnFrames;
    [SerializeField] private float attackReturnFps = 18f;

    [Header("Optional: Jump Up/Down")]
    [SerializeField] private Sprite jumpUpSprite;
    [SerializeField] private Sprite jumpDownSprite;
    [SerializeField] private float jumpUpDownThreshold = 0.05f;

    private VisualState currentState = VisualState.Run;

    private bool isAttackOut = false;
    private bool isAttackReturn = false;

    private Coroutine outCo;
    private Coroutine returnCo;

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
    }

    private void Update()
    {
        if (grappling == null || spriteRenderer == null) return;

        // ✅ 최우선: 공격 연출 (Return > Out)
        if (isAttackReturn)
        {
            ApplyState(VisualState.AttackReturn);
            return;
        }
        if (isAttackOut)
        {
            ApplyState(VisualState.AttackOut);
            return;
        }

        // 그 다음: 그래플/점프/달리기
        bool isAttach = grappling.IsAttach;
        bool isHookActive = grappling.IsHookActive;
        bool isGrounded = grappling.IsGrounded;

        bool isGrapplingAny = (isHookActive || isAttach);

        if (isGrapplingAny) ApplyState(VisualState.Grapple);
        else if (!isGrounded) ApplyState(VisualState.Jump);
        else ApplyState(VisualState.Run);
    }

    private void HandleAttackOutStart()
    {
        isAttackOut = true;

        // AttackOut 프레임이 있으면 코루틴 시작
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
        // 다음 Update에서 상태 재결정
    }

    private void HandleAttackReturnStart()
    {
        isAttackReturn = true;

        if (attackReturnFrames != null && attackReturnFrames.Length > 0)
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
        // 다음 Update에서 상태 재결정
    }

    private void ApplyState(VisualState next)
    {
        if (currentState == next) return;
        currentState = next;

        // Run 애니메이터 on/off (Run일 때만 켬)
        if (runAnimator != null)
            runAnimator.enabled = (next == VisualState.Run);

        // AttackOut 상태가 아니면 Out 코루틴 정리
        if (next != VisualState.AttackOut && outCo != null)
        {
            StopCoroutine(outCo);
            outCo = null;
        }

        // AttackReturn 상태가 아니면 Return 코루틴 정리
        if (next != VisualState.AttackReturn && returnCo != null)
        {
            StopCoroutine(returnCo);
            returnCo = null;
        }

        switch (next)
        {
            case VisualState.Run:
                // runAnimator가 sprite를 갱신
                break;

            case VisualState.Jump:
                SetJumpSprite();
                break;

            case VisualState.Grapple:
                if (grappleSprite != null)
                    spriteRenderer.sprite = grappleSprite;
                break;

            case VisualState.AttackOut:
                // 프레임이 없으면 단일 스프라이트
                if ((attackOutFrames == null || attackOutFrames.Length == 0) && attackOutSprite != null)
                    spriteRenderer.sprite = attackOutSprite;
                break;

            case VisualState.AttackReturn:
                // 프레임이 없으면 유지(원하면 단일 sprite를 추가로 만들어도 됨)
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

    private IEnumerator CoLoopAttackOutFrames()
    {
        if (runAnimator != null) runAnimator.enabled = false;

        float frameTime = 1f / Mathf.Max(1f, attackOutFps);
        int idx = 0;

        while (true)
        {
            spriteRenderer.sprite = attackOutFrames[idx];
            idx = (idx + 1) % attackOutFrames.Length;
            yield return new WaitForSeconds(frameTime);
        }
    }

    private IEnumerator CoLoopAttackReturnFrames()
    {
        if (runAnimator != null) runAnimator.enabled = false;

        float frameTime = 1f / Mathf.Max(1f, attackReturnFps);
        int idx = 0;

        while (true)
        {
            spriteRenderer.sprite = attackReturnFrames[idx];
            idx = (idx + 1) % attackReturnFrames.Length;
            yield return new WaitForSeconds(frameTime);
        }
    }
}
