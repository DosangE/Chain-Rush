using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Playing, Paused, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameState State { get; private set; } = GameState.Playing;

    private MapMover mapMover;
    private Grappling player; // Jump&Grapple 스크립트 쪽

    [Header("UI Panels (각 씬의 오브젝트를 연결하거나, 자동 탐색 사용)")]
    public GameObject gameUI;
    public GameObject pausePopupUI;
    public GameObject gameOverUI;

    [Header("Scene Names")]
    [SerializeField] private string titleSceneName = "TitleScene";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
        HideAllUI();
    }

    private void HideAllUI()
    {
        if (gameUI != null) gameUI.SetActive(false);
        if (pausePopupUI != null) pausePopupUI.SetActive(false);
        if (gameOverUI != null) gameOverUI.SetActive(false);
    }

    private void Update()
    {
        if (State == GameState.Playing && Input.GetKeyDown(KeyCode.Escape))
        {
            PauseGame();
        }
        else if (State == GameState.Paused && Input.GetKeyDown(KeyCode.Escape))
        {
            ResumeGame();
        }
    }

    // 점수 코루틴 (timeScale 0이면 WaitForSeconds가 멈추는 건 정상)
    private IEnumerator IE_AddScore()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
        }
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        State = GameState.Playing;

        if (gameUI != null) gameUI.SetActive(true);
        if (pausePopupUI != null) pausePopupUI.SetActive(false);
        if (gameOverUI != null) gameOverUI.SetActive(false);
    }
    
    public void SetGameOver()
    {
        State = GameState.GameOver;
    }


    public void GameOver()
    {
        if (State == GameState.GameOver) return;

        State = GameState.GameOver;

        // UI
        if (gameOverUI != null) gameOverUI.SetActive(true);
        if (pausePopupUI != null) pausePopupUI.SetActive(false);

        // timeScale 0
        Time.timeScale = 0f;

        // 맵 이동 정지
        if (mapMover != null)
            mapMover.StopMove();

        // 플레이어 연출
        if (player != null)
            player.OnDeath();
    }

    public void PauseGame()
    {
        State = GameState.Paused;
        if (pausePopupUI != null) pausePopupUI.SetActive(true);
        if (gameOverUI != null) gameOverUI.SetActive(false);
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        State = GameState.Playing;
        if (pausePopupUI != null) pausePopupUI.SetActive(false);
    }

    public void ReturnToLobby()
    {
        Time.timeScale = 1f;
        State = GameState.Playing;
        HideAllUI();

        SceneManager.LoadScene(titleSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}