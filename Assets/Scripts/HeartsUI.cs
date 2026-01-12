using UnityEngine;
using UnityEngine.UI;

public class HeartsUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Hearts (3 images)")]
    [SerializeField] private Image[] hearts = new Image[3];

    [Header("Sprites")]
    [SerializeField] private Sprite fullHeart;
    [SerializeField] private Sprite emptyHeart;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.OnHpChanged += HandleHpChanged;
            HandleHpChanged(playerHealth.CurrentHp, playerHealth.MaxHp);
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnHpChanged -= HandleHpChanged;
    }

    private void HandleHpChanged(int current, int max)
    {
        // max=3 전제지만, 혹시 늘려도 대응되게 작성
        for (int i = 0; i < hearts.Length; i++)
        {
            if (hearts[i] == null) continue;

            bool filled = (i < current);
            hearts[i].sprite = filled ? fullHeart : emptyHeart;
            hearts[i].enabled = true;
        }
    }
}
