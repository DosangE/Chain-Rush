using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public enum GameState { Playing, Paused, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameState State { get; private set; } = GameState.Playing;

    public bool IsInputLocked => (State != GameState.Playing);

    private MapMover mapMover;
    private Grappling player; // Jump&Grapple 스크립트 쪽

    [Header("UI Panels (각 씬의 오브젝트를 연결하거나, 자동 탐색 사용)")]
    public GameObject gameUI;
    public GameObject pausePopupUI;
    public GameObject gameOverUI;
    public GameObject qteUI;
    public GameObject playerHealthUI;
    public GameObject clearUI;

    [Header("Pause Popup CanvasGroup (권장)")]
    [SerializeField] private CanvasGroup pausePopupCanvasGroup;

    [Header("Scene Names")]
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("SFX")]
    [SerializeField] private AudioClip clickSFX;
    [SerializeField] private AudioClip boomSFX;
    [SerializeField] private AudioClip speedupSFX;
    [SerializeField] private AudioClip s_swooshSFX;
    [SerializeField] private AudioClip q_swooshSFX;
    [SerializeField] private AudioClip bossHitSFX;
    [SerializeField] private AudioClip wrongQTESFX;
    [SerializeField] private AudioClip clearSFX;
    [SerializeField] private AudioClip barrierSFX;
    // UI 클릭을 위해 필요 (없으면 자동 생성)
    private EventSystem eventSystem;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        // 필요한 참조 자동 탐색(원하면 제거 가능)
        if (mapMover == null) mapMover = FindObjectOfType<MapMover>();
        if (player == null) player = FindObjectOfType<Grappling>();

        // EventSystem 체크/생성
        eventSystem = FindObjectOfType<EventSystem>();
        if (eventSystem == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            eventSystem = es.GetComponent<EventSystem>();
        }
        if (SoundManager.instance != null)
            SoundManager.instance.PlayBGM(SoundManager.instance.mainBGM);
        HideAllUI();

        // PausePopup에 CanvasGroup 안 달려있으면 자동으로 찾아보기 (선택)
        if (pausePopupCanvasGroup == null && pausePopupUI != null)
            pausePopupCanvasGroup = pausePopupUI.GetComponent<CanvasGroup>();
    }

    private void HideAllUI()
    {
        if (gameUI != null) gameUI.SetActive(false);
        if (pausePopupUI != null) pausePopupUI.SetActive(false);
        if (gameOverUI != null) gameOverUI.SetActive(false);
        if (qteUI != null) qteUI.SetActive(false);
        if (clearUI != null) clearUI.SetActive(false);
    }
    private void Update()
    {
        // if (State == GameState.Playing && Input.GetKeyDown(KeyCode.Escape))
        // {
        //     PauseGame();
        // }
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

        ApplyPauseUIInteractivity(false);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        State = GameState.Playing;
        HideAllUI();
        ApplyPauseUIInteractivity(false);

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void SetGameOver()
    {
        State = GameState.GameOver;
    }

    public void GameOver()
    {
        if (State == GameState.GameOver) return;

        State = GameState.GameOver;

        if (gameOverUI != null) gameOverUI.SetActive(true);
        if (pausePopupUI != null) pausePopupUI.SetActive(false);
        if (qteUI != null) qteUI.SetActive(false);
        if (playerHealthUI != null) playerHealthUI.SetActive(false);

        // timeScale 0
        Time.timeScale = 0f;

        // 맵 이동 정지
        if (mapMover != null)
            mapMover.StopMove();

        // 플레이어 연출
        if (player != null)
            player.OnDeath();

        ApplyPauseUIInteractivity(false);
    }

    public void PauseGame()
    {
        State = GameState.Paused;

        if (pausePopupUI != null) pausePopupUI.SetActive(true);
        if (gameOverUI != null) gameOverUI.SetActive(false);

        Time.timeScale = 0f;

        ApplyPauseUIInteractivity(true);
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        State = GameState.Playing;

        if (pausePopupUI != null) pausePopupUI.SetActive(false);

        ApplyPauseUIInteractivity(false);
    }

    private void ApplyPauseUIInteractivity(bool enablePopupOnly)
    {
        // PausePopup 쪽
        if (pausePopupCanvasGroup != null)
        {
            pausePopupCanvasGroup.interactable = enablePopupOnly;
            pausePopupCanvasGroup.blocksRaycasts = enablePopupOnly;
            pausePopupCanvasGroup.ignoreParentGroups = true;
        }

        // GameUI는 pause 중 클릭 막고 싶다면(원하는 동작)
        if (gameUI != null)
        {
            var cg = gameUI.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.interactable = !enablePopupOnly;
                cg.blocksRaycasts = !enablePopupOnly;
            }
        }
    }
    public void OnGameClear()
    {
        // 하드모드 해금
        HardModeSave.Unlock();

        // 클리어 UI 표시
        if (clearUI != null)
            clearUI.SetActive(true);

        PlayClearSFX();

        Time.timeScale = 0f;
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        if (SoundManager.instance == null) return;   // TitleScene에서 생성 안 됐으면 null 가능
        SoundManager.instance.PlaySFX(clip);         // 네 SoundManager 함수 그대로 사용
    }

    public void PlayClickSFX() => PlaySFX(clickSFX);
    public void PlayBoomSFX() => PlaySFX(boomSFX);
    public void PlaySpeedupSFX() => PlaySFX(speedupSFX);
    public void PlaySwooshSFX() => PlaySFX(s_swooshSFX);
    public void PlayQuickSwooshSFX() => PlaySFX(q_swooshSFX);
    public void PlayBossHitSFX() => PlaySFX(bossHitSFX);
    public void PlayWrongQTESFX() => PlaySFX(wrongQTESFX);
    public void PlayClearSFX() => PlaySFX(clearSFX);
    public void PlayBarrierSFX() => PlaySFX(barrierSFX);

    public void ReturnToLobby()
    {
        Time.timeScale = 1f;
        State = GameState.Playing;
        HideAllUI();
        ApplyPauseUIInteractivity(false);

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
