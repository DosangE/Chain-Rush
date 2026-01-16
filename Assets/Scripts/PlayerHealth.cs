using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHp = 3;

    [SerializeField, Tooltip("디버그용(인스펙터에서 보이게)")]
    private int currentHp;

    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;

    [Header("Invincibility")]
    [SerializeField] private float invincibleTime = 0.8f;
    public bool IsInvincible { get; private set; }

    public event Action<int, int> OnHpChanged; // (current, max)
    public event Action OnDied;

    [Header("UI")]
    [SerializeField] private HeartsUI heartsUI;

    private void Awake()
    {
        currentHp = maxHp;
        if (heartsUI == null) heartsUI = FindObjectOfType<HeartsUI>();
        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    public void ResetHp()
    {
        StopAllCoroutines();
        IsInvincible = false;

        currentHp = maxHp;
        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    public bool TakeDamage(int amount)
    {
        if (amount <= 0) return false;
        if (currentHp <= 0) return false;

        if (IsInvincible)
        {
            Debug.Log("[HP] damage ignored (invincible)");
            return false;
        }

        currentHp = Mathf.Max(0, currentHp - amount);
        Debug.Log($"[HP] -{amount} => {currentHp}/{maxHp}");
        OnHpChanged?.Invoke(currentHp, maxHp);

        if (currentHp <= 0)
        {
            Debug.Log("[HP] died");
            OnDied?.Invoke();
        }
        else
        {
            StartCoroutine(InvincibleRoutine());
        }

        return true;
    }

    public bool BossTakeDamage(int amount)
    {
        if (amount <= 0) return false;
        if (currentHp <= 0) return false;

        currentHp = Mathf.Max(0, currentHp - amount);
        Debug.Log($"[HP] -{amount} => {currentHp}/{maxHp}");
        OnHpChanged?.Invoke(currentHp, maxHp);

        if (currentHp <= 0)
        {
            Debug.Log("[HP] died");
            OnDied?.Invoke();
        }

        return true;
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        if (currentHp <= 0) return;

        // 최대 체력 초과 금지
        if (currentHp >= maxHp) return;

        int prevHp = currentHp;
        currentHp = Mathf.Min(currentHp + amount, maxHp);

        // 슬롯 켜기/끄기 먼저 반영
        OnHpChanged?.Invoke(currentHp, maxHp);

        // 실제로 증가했을 때만 "새로 켜진 슬롯"을 frameA로 동기화
        if (currentHp > prevHp)
        {
            int healedIndex = currentHp - 1;

            if (heartsUI == null) heartsUI = FindObjectOfType<HeartsUI>();
            if (heartsUI != null)
                heartsUI.ResetBlinkAt(healedIndex);
        }
    }

    private IEnumerator InvincibleRoutine()
    {
        IsInvincible = true;
        yield return new WaitForSeconds(invincibleTime);
        IsInvincible = false;
    }
}
