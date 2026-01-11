using System.Collections;
using System.Dynamic;
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

    public bool TryStartAttackAttempt()
    {
        if (hitsToDestroy <= 0) return false;
        if (_qteRunning) return false;
        if (Time.time < _nextAttackTime) return false;
        if (qte == null) return false;

        _nextAttackTime = Time.time + attackCooldown;
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
