using UnityEngine;

public class Enemy : MonoBehaviour, IAttackable
{
    public void OnHitByAttack()
    {
        Destroy(gameObject);
    }
}
