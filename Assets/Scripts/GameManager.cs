using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Playing,
    GameOver,
    Paused
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState State { get; private set; } = GameState.Playing;

    private MapMover mapMover;
    private Grappling player; // Jump&Grapple 스크립트 쪽
    private GameObject gameOverUI;   // Canvas 안의 패널

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 처음에는 UI 끄기
        if (gameOverUI != null)
            gameOverUI.SetActive(false);
    }

    public void GameOver()
    {
        if (State == GameState.GameOver) return; // 중복 호출 방지

        State = GameState.GameOver;

        // 1. 맵 이동 정지
        if (mapMover != null)
            mapMover.StopMove();

        // 2. 플레이어 낙하 연출
        if (player != null)
            player.OnDeath();   // 아래에서 구현

        // 3. 게임오버 UI 표시
        if (gameOverUI != null)
            gameOverUI.SetActive(true);
    }

    // UI 버튼에서 호출할 것들
    public void Retry()
    {
        // 현재 씬 다시 로드
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        // 에디터에서는 종료 안 됨
        Application.Quit();
    }
}
