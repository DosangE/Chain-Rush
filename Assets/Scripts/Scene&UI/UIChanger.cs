using UnityEngine;
using UnityEngine.SceneManagement;

public class UIChanger : MonoBehaviour
{
    private void Start()
    {
        SoundManager.instance.PlayBGM(SoundManager.instance.lobbyBGM);
    }
    public void OnClickStart()
    {
        SoundManager.instance.PlayClickSound();
        SceneManager.LoadScene("InGameScene");
    }

    public void OnClickTutorial()
    {
        SoundManager.instance.PlayClickSound();
        SceneManager.LoadScene("TutorialScene");
    }

    public void OnClickQuit()
    {
        SoundManager.instance.PlayClickSound();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
