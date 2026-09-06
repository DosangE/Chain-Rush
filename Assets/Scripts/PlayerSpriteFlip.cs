using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerSpriteAnimator : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite[] sprites = new Sprite[15];

    [Header("Animation Settings")]
    [SerializeField] private float frameTime = 0.1f;

    private SpriteRenderer spriteRenderer;
    private int currentIndex = 0;
    private float timer = 0f;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        timer = 0f;
        currentIndex = 0;

        if (sprites != null && sprites.Length > 0)
            spriteRenderer.sprite = sprites[currentIndex]; // ✅ 첫 프레임 즉시 세팅
    }

    private void Update()
    {
        if (sprites == null || sprites.Length == 0)
            return;

        timer += Time.deltaTime;

        if (timer >= frameTime)
        {
            timer -= frameTime;
            AdvanceFrame();
        }
    }

    private void AdvanceFrame()
    {
        currentIndex++;

        if (currentIndex >= sprites.Length)
            currentIndex = 0;

        spriteRenderer.sprite = sprites[currentIndex];
    }
}
