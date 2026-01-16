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
        for (int i = 0; i < heartSlots.Length; i++)
        {
            if (heartSlots[i] == null) continue;
            heartSlots[i].SetActive(i < current);
        }
    }

    public void ResetBlinkAt(int index)
    {
        if (heartSlots == null) return;
        if (index < 0 || index >= heartSlots.Length) return;

        GameObject slot = heartSlots[index];
        if (slot == null) return;

        // slot은 이미 OnHpChanged에서 켜졌겠지만, 혹시 꼬였으면 안전하게 켜둠
        if (!slot.activeSelf) slot.SetActive(true);

        // 해당 슬롯 아래 HeartBlink만 리셋
        HeartBlink blink = slot.GetComponentInChildren<HeartBlink>(true);
        if (blink != null)
            blink.ResetBlink();
    }
}
