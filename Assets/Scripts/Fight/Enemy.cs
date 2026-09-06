using UnityEngine;

public class Enemy : MonoBehaviour, IAttackable
{
    [Header("VFX")]
    [Tooltip("적 파괴 시 재생될 파티클 프리팹")]
    [SerializeField] private ParticleSystem destroyParticle;

    public void OnHitByAttack()
    {
        FindObjectOfType<TutorialFlow>()?.ReportEnemyHit();
        PlayDestroyParticle();
        Destroy(gameObject);
    }

    private void PlayDestroyParticle()
    {
        if (destroyParticle == null) return;

        // 월드 좌표 기준으로 파티클 생성
        ParticleSystem ps = Instantiate(
            destroyParticle,
            transform.position,
            Quaternion.identity
        );

        // 파티클이 끝나면 자동 제거
        Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax);
    }
}
