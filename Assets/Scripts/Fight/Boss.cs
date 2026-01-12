using System.Collections;
using UnityEngine;

public class Boss : MonoBehaviour
{
    [Header("Boss HP (hits to destroy)")]
    [SerializeField] private int hitsToDestroy = 3;

    [Header("Attack Cooldown")]
    [SerializeField] private float attackCooldown = 10f;

    [Header("QTE")]
    [SerializeField] private BossQTE qte;

    [Header("Defeat")]
    [SerializeField] private bool destroyOnDefeat = true;

    public bool IsInQTE => _qteRunning;

    private bool _qteRunning;
    private float _nextAttackTime;

    public bool CanStartAttempt => !_qteRunning && Time.time >= _nextAttackTime && hitsToDestroy > 0;
    public float CooldownRemaining => Mathf.Max(0f, _nextAttackTime - Time.time);

    private GameManager gm => GameManager.Instance;

    private void Awake()
    {
        if (qte == null) qte = GetComponentInChildren<BossQTE>(true);
        if (qte == null)
            Debug.LogError("[Boss] BossQTE missing. Add BossQTE under Boss prefab and assign it.");
    }

    /// <summary>
    /// QTE를 시작한다. (여기서는 쿨타임을 찍지 않는다!)
    /// 쿨타임 시작은 PlayerAttack의 "보스 공격 마무리(복귀 끝)"에서 StartCooldownNow()로 호출한다.
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

    /// <summary>
    /// ✅ 보스 공격 "마무리 시점"에 쿨타임 시작 (PlayerAttack이 호출)
    /// </summary>
    public void StartCooldownNow()
    {
        _nextAttackTime = Time.time + attackCooldown;
    }

    private IEnumerator Co_RunQTE()
    {
        _qteRunning = true;

        qte.Begin();

        while (qte.IsRunning)
            yield return null;

        _qteRunning = false;

        if (qte.WasSuccess)
        {
            hitsToDestroy--;
            if (hitsToDestroy <= 0 && destroyOnDefeat)
                Destroy(gameObject);
        }
        else
        {
            // 실패 즉사
            if (GameManager.Instance != null)
                gm.GameOver();
            else
                Debug.LogError("[Boss] GameManager.Instance is null. Cannot set GameOver.");
        }
    }
}
