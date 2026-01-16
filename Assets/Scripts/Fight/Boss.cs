using System.Collections;
using UnityEngine;

public class Boss : MonoBehaviour, IAttackable
{
    [Header("Boss HP")]
    [SerializeField] private int hitsToDestroy = 3;

    [Header("Attack Cooldown")]
    [SerializeField] private float attackCooldown = 10f;

    [Header("QTE")]
    [SerializeField] private BossQTE qte;

    [Header("Defeat")]
    [SerializeField] private bool destroyOnDefeat = true;

    private bool _qteRunning;
    private float _nextAttackTime;

    public bool IsInQTE => _qteRunning;
    public bool CanStartAttempt =>
        !_qteRunning && Time.time >= _nextAttackTime && hitsToDestroy > 0;

    private void Awake()
    {
        if (qte == null)
            qte = GetComponentInChildren<BossQTE>(true);

        if (qte == null)
            Debug.LogError("[Boss] BossQTE missing.");
    }

    public bool TryStartAttackAttempt()
    {
        if (!CanStartAttempt || qte == null) return false;
        StartCoroutine(Co_RunQTE());
        return true;
    }

    public bool ForceStartAttackAttempt()
    {
        if (_qteRunning || hitsToDestroy <= 0 || qte == null) return false;
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

        if (attackCooldown > 0f)
            _nextAttackTime = Time.time + attackCooldown;

        if (qte.WasSuccess)
        {
            var pa = FindObjectOfType<PlayerAttack>();
            if (pa != null) pa.GrantBossHitCredit(1);
        }
        else
        {
            var ph = FindObjectOfType<PlayerHealth>();
            if (ph != null)
            {
                ph.BossTakeDamage(1);
                if (ph.CurrentHp <= 0 && GameManager.Instance != null)
                    GameManager.Instance.GameOver();
            }
        }

        var mm = FindObjectOfType<MapManager>();
        if (mm != null)
            mm.OnBossQTEResult(qte.WasSuccess);
    }

    public void OnHitByAttack()
    {
        if (hitsToDestroy <= 0) return;

        hitsToDestroy--;

        if (hitsToDestroy <= 0 && destroyOnDefeat)
        {
            var mm = FindObjectOfType<MapManager>();
            if (mm != null)
            {
                mm.AdvanceSpeedStage();
                mm.OnBossDefeated();

                var ph = FindObjectOfType<PlayerHealth>();
                if (ph != null) ph.Heal(1);
            }

            Destroy(gameObject);
        }
    }
}
