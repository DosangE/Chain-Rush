using UnityEngine;
using TMPro;

public class TutorialHUDUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private TextMeshProUGUI progressText;

    public void SetHint(string text)
    {
        if (hintText != null) hintText.text = text;
    }

    public void SetProgress(int current, int required)
    {
        if (progressText == null) return;

        required = Mathf.Max(1, required);
        current = Mathf.Clamp(current, 0, required);

        progressText.text = $"{current}/{required}";
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }
}
