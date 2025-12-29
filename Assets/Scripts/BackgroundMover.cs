using UnityEngine;

public class BackgroundLooper : MonoBehaviour
{
    public Transform BG1;
    public Transform BG2;
    public Transform mapMover;

    public float parallax = 0.3f;
    public float spriteWidth = 61.44f;

    private Transform leftBG;
    private Transform rightBG;

    private float lastMapX;

    void Start()
    {
        leftBG = BG1;
        rightBG = BG2;

        lastMapX = mapMover.position.x;

        // 두 장을 정확히 붙이기
        BG2.position = BG1.position + new Vector3(spriteWidth, 0, 0);
    }

    void LateUpdate()
    {
        float deltaX = mapMover.position.x - lastMapX;

        // 패럴랙스 이동
        BG1.position += new Vector3(deltaX * parallax, 0, 0);
        BG2.position += new Vector3(deltaX * parallax, 0, 0);

        lastMapX = mapMover.position.x;

        // 반복 처리
        if (mapMover.position.x - leftBG.position.x >= spriteWidth)
        {
            leftBG.position = rightBG.position + new Vector3(spriteWidth, 0, 0);

            // swap
            var temp = leftBG;
            leftBG = rightBG;
            rightBG = temp;
        }
    }
}
