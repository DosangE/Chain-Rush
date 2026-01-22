using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerSpriteAnimator : MonoBehaviour
{
    [Header("Sprites (size must be 15)")]
    [SerializeField] private Sprite[] sprites = new Sprite[15];

    [Header("Animation Settings")]
    [SerializeField] private float frameTime = 0.1f; // 한 프레임당 시간 (초)

    private SpriteRenderer spriteRenderer;
    private int currentIndex = 0;
    private float timer = 0f;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
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
