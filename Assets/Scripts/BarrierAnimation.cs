using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BarrierAnimation : MonoBehaviour
{
    [Header("Barrier Frames")]
    [SerializeField] private Sprite[] frames;

    [Header("Animation")]
    [SerializeField] private float frameInterval = 0.08f;
    [SerializeField] private bool playOnEnable = true;

    private SpriteRenderer sr;
    private float timer;
    private int frameIndex;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        ResetAnimation();
    }

    private void OnEnable()
    {
        if (playOnEnable)
            ResetAnimation();
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0)
            return;

        timer += Time.deltaTime;
        if (timer >= frameInterval)
        {
            timer -= frameInterval;
            frameIndex = (frameIndex + 1) % frames.Length;
            sr.sprite = frames[frameIndex];
        }
    }

    private void ResetAnimation()
    {
        timer = 0f;
        frameIndex = 0;
        if (frames != null && frames.Length > 0)
            sr.sprite = frames[0];
    }
}
