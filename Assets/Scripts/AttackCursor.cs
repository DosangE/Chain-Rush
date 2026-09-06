using UnityEngine;

public class AttackCursor : MonoBehaviour
{
    [Header("Cursors")]
    [SerializeField] private Texture2D attackableCursor;
    [SerializeField] private Vector2 hotspot = Vector2.zero;
    [SerializeField] private CursorMode mode = CursorMode.Auto;

    [Header("Targeting")]
    [SerializeField] private LayerMask attackableMask;
    [SerializeField] private float cursorHitRadius = 0f;

    [Header("Rules (Boss)")]
    [SerializeField] private PlayerAttack playerAttack; // bossHitCredit 체크용
    [SerializeField] private bool blockDuringQTE = true;

    private Camera cam;
    private bool isAttackCursor;

    private void Awake()
    {
        cam = Camera.main;
        if (playerAttack == null) playerAttack = FindObjectOfType<PlayerAttack>();
    }

    private void OnEnable()
    {
        // 이 씬 들어올 때는 일단 기본(Project Settings 커서)로 시작
        SetNormal();
    }

    private void OnDisable()
    {
        // 이 오브젝트가 비활성/씬 종료될 때는 반드시 기본으로 복귀
        SetNormal();
    }

    private void Update()
    {
        // 입력 잠금 상태면 공격커서 금지
        if (PlayerActionLock.IsLocked || (GameManager.Instance != null && GameManager.Instance.IsInputLocked))
        {
            SetNormal();
            return;
        }

        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);

        Collider2D col = (cursorHitRadius <= 0f)
            ? Physics2D.OverlapPoint(mouseWorld, attackableMask)
            : Physics2D.OverlapCircle(mouseWorld, cursorHitRadius, attackableMask);

        if (col == null)
        {
            SetNormal();
            return;
        }

        // Boss 규칙: QTE 중이면 X, bossHitCredit 없으면 X
        Boss boss = col.GetComponentInParent<Boss>();
        if (boss != null)
        {
            if (blockDuringQTE && boss.IsInQTE)
            {
                SetNormal();
                return;
            }

            if (playerAttack != null && playerAttack.HasBossHitCredit())
            {
                SetAttackable();
            }
            else
            {
                SetNormal();
            }
            return;
        }

        // Enemy/기타: IAttackable이면 OK
        IAttackable atk = col.GetComponentInParent<IAttackable>();
        if (atk != null)
        {
            SetAttackable();
            return;
        }

        SetNormal();
    }

    private void SetAttackable()
    {
        if (isAttackCursor) return;
        isAttackCursor = true;
        Cursor.SetCursor(attackableCursor, hotspot, mode);
    }

    private void SetNormal()
    {
        if (!isAttackCursor) return;
        isAttackCursor = false;
        Cursor.SetCursor(null, Vector2.zero, mode); // Project Settings 기본 커서로 복귀
    }
}
