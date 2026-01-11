using UnityEngine;

public class GrapplingScaling : MonoBehaviour
{
    private Grappling g;

    public void Init(Grappling owner)
    {
        g = owner;
    }

    public void ApplyMapSpeedScaling()
    {
        if (g == null) return;
        if (g.MapManager == null) return;

        float speedRatio = g.MapManager.currentMapSpeed / g.MapManager.baseMapSpeed;

        // 점프/낙하감 압축용 중력 스케일
        g.Rb.gravityScale = g.BaseGravity * Mathf.Pow(speedRatio, 0.60f);

        // 그래플 발사(시각/체감 템포)
        g.HookSpeed = g.BaseHookSpeed;

        // (현재 스윙만 쓰므로 아래 3개는 안 써도 되지만, 기존 변수 유지 차원에서 남겨둠)
        g.RopeRetractSpeed = g.BaseRopeRetractSpeed * speedRatio;
        g.LiftForce = g.BaseLiftForce * speedRatio;
        g.MaxLiftSpeed = g.BaseMaxLiftSpeed * speedRatio;

        // 낙하 상한
        g.MaxFallSpeed = g.BaseMaxFallSpeed * speedRatio;
    }
}
