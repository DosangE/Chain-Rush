using UnityEngine;

public class GrapplingVisual : MonoBehaviour
{
    private Grappling g;

    public void Init(Grappling owner)
    {
        g = owner;
    }

    public void UpdateHookVisual()
    {
        if (g == null) return;

        bool shouldShowHook = g.IsHookActive || g.IsAttach;

        if (!shouldShowHook)
        {
            if (g.Hook.gameObject.activeSelf)
                g.Hook.gameObject.SetActive(false);
            return;
        }

        if (!g.Hook.gameObject.activeSelf)
            g.Hook.gameObject.SetActive(true);

        // 1) 훅 위치 결정
        Vector2 hookPos;
        if (g.IsAttach && g.Joint2D.enabled)
        {
            hookPos = GetAnchorWorld();
            g.Hook.position = hookPos;
        }
        else
        {
            hookPos = g.Hook.position;
        }

        // 2) 라인 끝은 훅 위치에서 살짝 "뒤로" 당겨서 겹침 방지
        Vector2 start = g.ChainOriginWorld; // ✅ 시작점 오프셋 적용
        Vector2 end = g.Hook.position;
        Vector2 dir = end - start;

        Vector2 lineEnd = end;

        if (dir.sqrMagnitude > 1e-6f)
        {
            dir.Normalize();

            float back = Mathf.Min(g.ChainEndBackOffset, Vector2.Distance(start, end));
            lineEnd = end - dir * back;
        }

        if (g.Line.enabled)
            g.Line.SetPosition(1, lineEnd);

        // 3) 회전: "발사 중"에는 launchDir 기준 / "붙은 상태"에는 시작점->훅 방향 기준
        if (g.IsHookActive && !g.IsAttach)
        {
            dir = g.LaunchDir.normalized;
        }
        else
        {
            dir = (Vector2)g.Hook.position - g.ChainOriginWorld; // ✅ 기준 통일
            if (dir.sqrMagnitude > 1e-6f) dir.Normalize();
            else dir = Vector2.right;
        }

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        g.Hook.rotation = Quaternion.Euler(0f, 0f, angle + g.HookAngleOffset);
    }

    private Vector2 GetAnchorWorld()
    {
        if (g.Joint2D.connectedBody)
            return g.Joint2D.connectedBody.transform.TransformPoint(g.Joint2D.connectedAnchor);
        else
            return g.Joint2D.connectedAnchor;
    }
}
