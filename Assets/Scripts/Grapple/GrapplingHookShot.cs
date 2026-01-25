using UnityEngine;

public class GrapplingHookShot : MonoBehaviour
{
    private Grappling g;

    // ====== 튜닝 ======
    [Header("Attach Instant Swing Tuning")]
    [SerializeField] private bool instantSwingOnAttach = true;

    // 붙는 순간 로프 슬랙 제거(0이면 기존과 동일)
    [SerializeField] private float attachDistanceShrink = 0.08f;

    // 붙는 순간 접선 속도가 너무 작으면 최소 이 속도는 확보
    [SerializeField] private float minTangentialSpeed = 2.0f;

    public void Init(Grappling owner)
    {
        g = owner;
    }

    public void StartHookShot()
    {
        if (g == null) return;

        g.Hook.SetParent(null);
        // ✅ 시작 위치: 오프셋 적용 발사 지점
        g.Hook.position = g.ChainOriginWorld;

        g.IsHookActive = true;
        g.IsLineMax = false;
        g.IsAttach = false;

        g.HookDist = 0f;

        g.Hook.gameObject.SetActive(true);
        g.Line.enabled = true;
    }

    public void ShootHook()
    {
        if (g == null) return;

        // ✅ origin: 오프셋 적용 발사 지점
        Vector2 origin = g.ChainOriginWorld;
        Vector2 dir = g.LaunchDir.normalized;

        float nextDist = Mathf.Min(g.HookDist + g.HookSpeed * Time.deltaTime, g.MaxGrappleDistance);

        const float skin = 0.05f;
        Vector2 start = origin + dir * skin;
        float rayLen = Mathf.Max(0.001f, nextDist - skin);

        RaycastHit2D hit = Physics2D.Raycast(start, dir, rayLen, g.GrappleLayer);

        if (hit.collider != null)
        {
            float hitDistance = Vector2.Distance(origin, hit.point);
            if (hitDistance < g.MinGrappleDistance)
            {
                g.IsLineMax = true;
                return;
            }

            g.Joint2D.autoConfigureConnectedAnchor = false;
            g.Joint2D.autoConfigureDistance = false;
            g.Joint2D.enableCollision = true;

            if (hit.rigidbody != null)
            {
                g.Joint2D.connectedBody = hit.rigidbody;
                g.Joint2D.connectedAnchor = hit.rigidbody.transform.InverseTransformPoint(hit.point);
            }
            else
            {
                g.Joint2D.connectedBody = null;
                g.Joint2D.connectedAnchor = hit.point;
            }

            // ====== (1) 슬랙 제거: 붙는 순간부터 팽팽하게 ======
            float distForJoint = hitDistance;
            if (attachDistanceShrink > 0f)
            {
                distForJoint = Mathf.Max(g.MinGrappleDistance, hitDistance - attachDistanceShrink);
            }

            g.Joint2D.distance = distForJoint;
            g.RopeLockDistance = distForJoint;

            g.Joint2D.maxDistanceOnly = false;
            g.Joint2D.enabled = true;

            g.IsAttach = true;
            g.IsHookActive = false;
            g.Hook.position = hit.point;

            g.PlayGrappleAttachSFX();

            // ====== (2) 붙는 즉시 스윙 속도 정리 ======
            if (instantSwingOnAttach)
            {
                ApplyInstantSwingVelocity(hit.point);
            }

            return;
        }

        g.HookDist = nextDist;
        g.Hook.position = origin + dir * g.HookDist;

        if (g.HookDist >= g.MaxGrappleDistance)
            g.IsLineMax = true;
    }

    private void ApplyInstantSwingVelocity(Vector2 anchorWorld)
    {
        // 로프 방향(앵커 -> 플레이어)
        Vector2 ropeDir = (Vector2)g.transform.position - anchorWorld;
        if (ropeDir.sqrMagnitude < 1e-6f) return;
        ropeDir.Normalize();

        Vector2 v = g.Rb.linearVelocity;

        // 로프의 방사 성분 제거
        Vector2 radial = Vector2.Dot(v, ropeDir) * ropeDir;
        Vector2 tangential = v - radial;

        // 접선 속도가 너무 작으면 최소 스윙 속도 보장
        float tMag = tangential.magnitude;
        if (tMag < minTangentialSpeed)
        {
            Vector2 perp = new Vector2(-ropeDir.y, ropeDir.x);
            float sign = (v.x >= 0f) ? 1f : -1f;
            tangential = perp * (minTangentialSpeed * sign);
        }

        g.Rb.linearVelocity = tangential;
    }

    public void BeginLeftClickCooldown()
    {
        if (g == null) return;
        if (g.LeftClickCooldownAfterGrapple <= 0f) return;
        g.LeftClickCooldownTimer = g.LeftClickCooldownAfterGrapple;
    }

    public void ReturnHook()
    {
        if (g == null) return;

        g.IsHookActive = false;
        g.IsLineMax = false;
        g.HookDist = 0f;

        g.Hook.gameObject.SetActive(false);
        g.Line.enabled = false;

        // ✅ 반환 위치도 오프셋 적용 발사 지점
        g.Hook.position = g.ChainOriginWorld;
    }
}
