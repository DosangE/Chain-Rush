using UnityEngine;

public static class HardModeSave
{
    private const string HARD_UNLOCK_KEY = "HardModeUnlocked";

    public static bool IsUnlocked()
    {
        return PlayerPrefs.GetInt(HARD_UNLOCK_KEY, 0) == 1;
    }

    public static void Unlock()
    {
        PlayerPrefs.SetInt(HARD_UNLOCK_KEY, 1);
        PlayerPrefs.Save();
    }
}
