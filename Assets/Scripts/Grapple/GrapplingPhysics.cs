using UnityEngine;

public class GrapplingPhysics : MonoBehaviour
{
    private Grappling g;

    public void Init(Grappling owner)
    {
        g = owner;
    }

    public void ClampFallSpeed()
    {
        if (g == null) return;

        var v = g.Rb.linearVelocity;
        if (v.y < -g.MaxFallSpeed) v.y = -g.MaxFallSpeed;
        g.Rb.linearVelocity = v;
    }

    public float GetGravityAccel()
    {
        if (g == null) return 0.0001f;
        float grav = -Physics2D.gravity.y * g.Rb.gravityScale;
        return Mathf.Max(0.0001f, grav);
    }

    public void ApplyDetachBoostByHeight(float boostHeight)
    {
        if (g == null) return;
        if (boostHeight <= 0f) return;

        float grav = GetGravityAccel();
        float vy = g.Rb.linearVelocity.y;

        float vTarget = Mathf.Sqrt((vy * vy) + 2f * grav * boostHeight);
        float deltaVy = vTarget - vy;

        if (deltaVy <= 0f) return;

        float impulse = g.Rb.mass * deltaVy;
        g.Rb.AddForce(Vector2.up * impulse, ForceMode2D.Impulse);
    }

    public Vector2 GetAnchorWorld()
    {
        if (g == null) return g.transform.position;

        if (g.Joint2D.connectedBody)
            return g.Joint2D.connectedBody.transform.TransformPoint(g.Joint2D.connectedAnchor);
        else
            return g.Joint2D.connectedAnchor;
    }

    public void Jump()
    {
        if (g == null) return;
        if (!g.IsGrounded) return;

        g.Rb.linearVelocity = new Vector2(g.Rb.linearVelocity.x, 0f);

        float grav = GetGravityAccel(isJumping: true);

        float targetHeight = g.BaseJumpHeight * g.JumpHeightMultiplier;

        float desiredV0 = Mathf.Sqrt(2f * grav * targetHeight);
        float impulse = desiredV0 * g.Rb.mass;

        g.Rb.AddForce(Vector2.up * impulse, ForceMode2D.Impulse);
    }

    public float GetGravityAccel(bool isJumping = false)
    {
        if (g == null) return 0.0001f;

        float grav = -Physics2D.gravity.y * g.Rb.gravityScale;

        if (isJumping)
            grav *= g.jumpGravityMultiplier;

        return Mathf.Max(0.0001f, grav);
    }

    public void ReleaseGrapple()
    {
        if (g == null) return;

        g.IsAttach = false;
        g.IsHookActive = false;
        g.IsLineMax = false;
        g.HookDist = 0f;

        g.Joint2D.connectedBody = null;
        g.Joint2D.enabled = false;

        g.Hook.gameObject.SetActive(false);
        g.Line.enabled = false;

        // ✅ 원래대로: 오프셋 적용 발사 지점으로 복귀
        g.Hook.position = g.ChainOriginWorld;
    }
}
