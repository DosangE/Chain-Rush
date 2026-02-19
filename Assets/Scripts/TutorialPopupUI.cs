using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialPopupUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Texts (TMP)")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;

    [Header("Button")]
    [SerializeField] private Button closeButton;

    [Header("Content Roots")]
    [SerializeField] private GameObject tutorialContentRoot; // ContentTutorial
    [SerializeField] private GameObject finishContentRoot;   // ContentFinish

    [Header("Finish Buttons")]
    [SerializeField] private Button robbyButton;
    [SerializeField] private Button gameStartButton;


    private TutorialFlow flow;
    private bool isFinishMode = false;

    private void Awake()
    {
        if (root == null) root = gameObject;

        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);

        if (robbyButton != null)
            robbyButton.onClick.AddListener(OnRobbyClicked);

        if (gameStartButton != null)
            gameStartButton.onClick.AddListener(OnGameStartClicked);

        HideImmediate();
    }


    public void Bind(TutorialFlow tutorialFlow)
    {
        flow = tutorialFlow;
    }

    public void Show(string title, string body)
    {
        isFinishMode = false;
        if (tutorialContentRoot != null) tutorialContentRoot.SetActive(true);
        if (finishContentRoot != null) finishContentRoot.SetActive(false);

        titleText.text = title;
        bodyText.text = body;
        root.SetActive(true);
    }

    public void HideImmediate()
    {
        if (root != null)
            root.SetActive(false);
    }

    private void OnCloseClicked()
    {
        HideImmediate();

        if (isFinishMode)
            flow?.ResumeFromFinish();
        else
            flow?.ResumeFromPopup();
    }

    private void OnRobbyClicked()
    {
        flow?.OnClickRobby();
    }

    private void OnGameStartClicked()
    {
        flow?.OnClickGameStart();
    }
    public void ShowFinish(string title, string body)
    {
        isFinishMode = true;
        if (tutorialContentRoot != null) tutorialContentRoot.SetActive(false);
        if (finishContentRoot != null) finishContentRoot.SetActive(true);

        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;

        root.SetActive(true);
    }

}