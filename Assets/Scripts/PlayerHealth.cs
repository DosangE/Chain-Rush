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

    private void Awake()
    {
        currentHp = maxHp;
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

    private IEnumerator InvincibleRoutine()
    {
        IsInvincible = true;
        yield return new WaitForSeconds(invincibleTime);
        IsInvincible = false;
    }
}
