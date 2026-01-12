using UnityEngine;

public class HeartsUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject[] heartSlots = new GameObject[3]; // HeartSlot_0..2

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.OnHpChanged += OnHpChanged;
            OnHpChanged(playerHealth.CurrentHp, playerHealth.MaxHp);
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnHpChanged -= OnHpChanged;
    }

    private void OnHpChanged(int current, int max)
    {
        // current=2면 0,1번만 켜짐. 2번은 꺼짐.
        for (int i = 0; i < heartSlots.Length; i++)
        {
            if (heartSlots[i] == null) continue;
            heartSlots[i].SetActive(i < current);
        }
    }
}
