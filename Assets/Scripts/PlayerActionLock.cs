using UnityEngine;

public class PlayerActionLock : MonoBehaviour
{
    public static bool IsLocked { get; private set; }

    public static void Lock() => IsLocked = true;
    public static void Unlock() => IsLocked = false;
}
