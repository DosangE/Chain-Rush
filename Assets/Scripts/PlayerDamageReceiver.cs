using UnityEngine;

public class PlayerDamageReceiver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerHealth health;

    [Header("Damage")]
    [SerializeField] private int touchDamage = 1;

    [Tooltip("Enemy, Boss 등 '몸통' 레이어만")]
    [SerializeField] private LayerMask damagingLayers;

    [SerializeField] private float hitCooldown = 0.2f;

    [Header("SFX")]
    [SerializeField] private AudioClip HitSFX;
    public void PlayHitSFX() => PlaySFX(HitSFX);
    private float nextAllowedTime;

    private void Awake()
    {
        if (health == null)
            health = GetComponent<PlayerHealth>();

        Debug.Log($"[DamageReceiver] Awake on {name}, rb={GetComponent<Rigidbody2D>() != null}");
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {

        TryApplyDamage(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyDamage(other);
    }

    private void TryApplyDamage(Collider2D col)
    {
        if (health == null) return;
        if (Time.time < nextAllowedTime) return;

        int layer = col.gameObject.layer;
        if (((1 << layer) & damagingLayers.value) == 0) return;

        bool damaged = health.TakeDamage(touchDamage);
        if (!damaged) return;
        
        PlayHitSFX();

        nextAllowedTime = Time.time + hitCooldown;

        Debug.Log($"[DamageReceiver] damaged by {col.name}");

        // 🔥 여기서 Enemy 삭제
        Enemy enemy = col.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            Debug.Log($"[DamageReceiver] destroy enemy {enemy.name}");
            Destroy(enemy.gameObject);
        }

        // HP 0이면 게임오버
        if (health.CurrentHp <= 0 && GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
        }
    }
    private void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        if (SoundManager.instance == null) return;   // TitleScene에서 생성 안 됐으면 null 가능
        SoundManager.instance.PlaySFX(clip);         // 네 SoundManager 함수 그대로 사용
    }
}
