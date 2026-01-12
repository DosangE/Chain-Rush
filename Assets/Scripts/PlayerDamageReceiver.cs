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
}
