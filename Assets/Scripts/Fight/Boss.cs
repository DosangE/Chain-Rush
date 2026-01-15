using System.Collections;
using UnityEngine;

public class Boss : MonoBehaviour, IAttackable
{
    [Header("Boss HP (hits to destroy)")]
    [SerializeField] private int hitsToDestroy = 3;

    [Header("Attack Cooldown (optional)")]
    [SerializeField] private float attackCooldown = 10f;

    [Header("QTE")]
    [SerializeField] private BossQTE qte;

    [Header("Defeat")]
    [SerializeField] private bool destroyOnDefeat = true;

    public bool IsInQTE => _qteRunning;

    private bool _qteRunning;
    private float _nextAttackTime;

    public bool CanStartAttempt => !_qteRunning && Time.time >= _nextAttackTime && hitsToDestroy > 0;

    private void Awake()
    {
        if (qte == null) qte = GetComponentInChildren<BossQTE>(true);
        if (qte == null)
            Debug.LogError("[Boss] BossQTE missing. Add BossQTE under Boss prefab and assign it.");
    }

    /// <summary>
    /// 기존(시간 쿨타임 체크 포함) 시작
    /// </summary>
    public bool TryStartAttackAttempt()
    {
        if (hitsToDestroy <= 0) return false;
        if (_qteRunning) return false;
        if (Time.time < _nextAttackTime) return false;
        if (qte == null) return false;

        StartCoroutine(Co_RunQTE());
        return true;
    }

    public bool ForceStartAttackAttempt()
    {
        if (hitsToDestroy <= 0) return false;
        if (_qteRunning) return false;
        if (qte == null) return false;

        StartCoroutine(Co_RunQTE());
        return true;
    }

    private IEnumerator Co_RunQTE()
    {
        _qteRunning = true;

        qte.Begin();

        while (qte.IsRunning)
            yield return null;

        _qteRunning = false;

        // ✅ QTE 종료 후 쿨타임 (원하면 attackCooldown=0으로 꺼도 됨)
        if (attackCooldown > 0f)
            _nextAttackTime = Time.time + attackCooldown;

        if (qte.WasSuccess)
        {
            // ✅ 성공 = "공격 기회 1회" 부여 (HP는 여기서 깎지 않는다)
            var pa = FindObjectOfType<PlayerAttack>();
            if (pa != null) pa.GrantBossHitCredit(1);
        }
        else
        {
            // 실패 = 플레이어 피해 + 게임오버 처리(기존 로직 유지)
            var ph = FindObjectOfType<PlayerHealth>();
            if (ph != null)
            {
                ph.BossTakeDamage(1);
                if (ph.CurrentHp <= 0)
                {
                    if (GameManager.Instance != null)
                        GameManager.Instance.GameOver();
                }
            }
            else
            {
                Debug.LogError("[Boss] PlayerHealth not found.");
            }

        }
        // ✅ QTE 결과를 MapManager에 통보 (실패 시에만 기준점 갱신하려고)
        MapManager mm = FindObjectOfType<MapManager>();
        if (mm != null)
            mm.OnBossQTEResult(qte.WasSuccess);

    }

    public void OnHitByAttack()
    {
        if (hitsToDestroy <= 0) return;

        hitsToDestroy--;

        if (hitsToDestroy <= 0 && destroyOnDefeat)
        {
            MapManager mm = FindObjectOfType<MapManager>();
            if (mm != null)
            {
                mm.AdvanceSpeedStage();
                mm.OnBossDefeated();
            }

            Destroy(gameObject);
        }

    }
}
