using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class HeartBlink : MonoBehaviour
{
    [SerializeField] private Sprite frameA;
    [SerializeField] private Sprite frameB;

    [Tooltip("프레임 전환 간격(초)")]
    [SerializeField] private float interval = 0.25f;

    private Image img;
    private float t;
    private bool toggle;

    private void Awake()
    {
        img = GetComponent<Image>();
        ApplySprite();
    }

    private void Update()
    {
        // 비활성화 되면 Update 안 돔
        t += Time.unscaledDeltaTime; // 일시정지(타임스케일)에도 UI는 돌게 하고 싶으면 unscaled 추천
        if (t >= interval)
        {
            t = 0f;
            toggle = !toggle;
            ApplySprite();
        }
    }

    private void ApplySprite()
    {
        if (img == null) return;
        img.sprite = toggle ? frameB : frameA;
    }

    public void SetFrames(Sprite a, Sprite b)
    {
        frameA = a;
        frameB = b;
        toggle = false;
        t = 0f;
        ApplySprite();
    }
}
