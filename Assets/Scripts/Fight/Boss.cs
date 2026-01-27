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

    [Header("Barrier")]
    [Tooltip("보스 기본 배리어 오브젝트(보스 자식 Barrier 등). 비우면 자식에서 'Barrier' 이름으로 자동 탐색.")]
    [SerializeField] private GameObject barrierRoot;

    [Header("Defeat")]
    [SerializeField] private bool destroyOnDefeat = true;

    [Header("VFX")]
    [Tooltip("보스 처치(파괴) 시 재생될 파티클 프리팹")]
    [SerializeField] private ParticleSystem defeatParticle;

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

        // Barrier 자동 탐색(선택)
        if (barrierRoot == null)
        {
            Transform t = transform.Find("Barrier");
            if (t != null) barrierRoot = t.gameObject;
        }

        // 기본 상태: 배리어 ON
        SetBarrierActive(true);
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

        // QTE 들어갈 때는 기본적으로 배리어 ON(무적 상태 연출)
        SetBarrierActive(true);

        qte.Begin();

        while (qte.IsRunning)
            yield return null;

        _qteRunning = false;

        if (attackCooldown > 0f)
            _nextAttackTime = Time.time + attackCooldown;

        if (qte.WasSuccess)
        {
            // ✅ QTE 성공 = 취약 상태 진입 -> 배리어 OFF
            SetBarrierActive(false);

            var pa = FindObjectOfType<PlayerAttack>();
            if (pa != null) pa.GrantBossHitCredit(1);
        }
        else
        {
            // ✅ QTE 실패 = 계속 무적 -> 배리어 ON 유지
            SetBarrierActive(true);

            var ph = FindObjectOfType<PlayerHealth>();
            if (ph != null)
            {
                ph.BossTakeDamage(1);
                if (ph.CurrentHp <= 0 && GameManager.Instance != null)
                {
                    if (qte != null) qte.Cancel();   // ✅ 사망 시 QTE 강제 종료
                    GameManager.Instance.GameOver();
                }
            }
        }

        // ✅ MapManager에게 QTE 결과 알림(기존 기능 유지)
        var mm = FindObjectOfType<MapManager>();
        if (mm != null)
            mm.OnBossQTEResult(qte.WasSuccess);
    }

    public void OnHitByAttack()
    {
        // (플레이어 공격 코드에서) 공격권 없으면 여기까지 안 오게 되어있음
        if (hitsToDestroy <= 0) return;

        hitsToDestroy--;

        if (hitsToDestroy <= 0 && destroyOnDefeat)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PlayBoomSFX();

            // ✅ 보스 처치 파티클
            PlayDefeatParticle();

            var mm = FindObjectOfType<MapManager>();
            if (mm != null)
            {
                // ✅ 여기서 mm.AdvanceSpeedStage()는 더 이상 호출하지 않음.
                // 스테이지/SpeedUp 연출은 MapManager.OnBossDefeated() 내부에서 처리하도록 변경됨.
                mm.OnBossDefeated();

                var ph = FindObjectOfType<PlayerHealth>();
                if (ph != null) ph.Heal(1);
            }

            Destroy(gameObject);
            return;
        }
    }

    private void PlayDefeatParticle()
    {
        if (defeatParticle == null) return;

        ParticleSystem ps = Instantiate(defeatParticle, transform.position, Quaternion.identity);

        // 파티클 끝나면 자동 제거 (StopAction=Destroy면 이 줄 없어도 됨)
        Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax);
    }

    public void SetBarrierActive(bool active)
    {
        GameManager.Instance.PlayBarrierSFX();
        if (barrierRoot == null) return;
        if (barrierRoot.activeSelf == active) return;
        barrierRoot.SetActive(active);
    }
}
