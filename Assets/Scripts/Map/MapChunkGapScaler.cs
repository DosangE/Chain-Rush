using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MapManager.currentMapSpeed 값을 기준(15)으로,
/// Ground 사이 갭만 늘리는 스케일러
/// </summary>
public class MapChunkGapScaler : MonoBehaviour
{
    [Header("필수 참조")]
    [SerializeField] private Transform groundRoot;
    [SerializeField] private Transform loofRoot;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Header("갭 판정")]
    [SerializeField] private float gapEpsilon = 0.02f;

    [Header("속도 기준")]
    [SerializeField] private float baseSpeed = 15f;

    [Header("속도별 갭 보정 (갭 1개당)")]
    [Tooltip("speed = 20 일 때 추가 거리")]
    [SerializeField] private float addPerGap_Speed20 = 5f;

    [Tooltip("speed = 25 일 때 추가 거리")]
    [SerializeField] private float addPerGap_Speed25 = 10f;

    [Header("Bounds 계산")]
    [SerializeField] private bool preferColliderBounds = true;

    public void Apply(float currentMapSpeed)
    {
        float addPerGap = GetAddPerGap(currentMapSpeed);
        if (Mathf.Approximately(addPerGap, 0f)) return;

        var grounds = CollectGrounds();
        if (grounds.Count <= 1) return;

        grounds.Sort((a, b) => a.localPosition.x.CompareTo(b.localPosition.x));
        List<Transform> loofs = CollectLoofs();

        for (int i = 0; i < grounds.Count - 1; i++)
        {
            Transform left = grounds[i];
            Transform right = grounds[i + 1];

            // ✅ 1. 기존 gap 정보 고정
            float leftEdge = GetRightEdgeLocalX(left);
            float rightEdge = GetLeftEdgeLocalX(right);
            float originalGap = rightEdge - leftEdge;

            if (originalGap <= gapEpsilon)
                continue;

            float newGap = originalGap + addPerGap;

            // ✅ 2. 이 gap 안에 있는 loof들의 "비율" 미리 저장
            List<(Transform loof, float ratio)> loofRatios = new List<(Transform, float)>();

            foreach (var loof in loofs)
            {
                float x = loof.localPosition.x;
                if (x > leftEdge && x < rightEdge)
                {
                    float ratio = (x - leftEdge) / originalGap;
                    loofRatios.Add((loof, ratio));
                }
            }

            // ✅ 3. ground 이동 (i+1 이후 누적)
            for (int g = i + 1; g < grounds.Count; g++)
            {
                ShiftX(grounds[g], addPerGap);
            }

            // ✅ 4. endPoint 이동
            ShiftX(endPoint, addPerGap);

            // ✅ 5. loof 재배치 (저장된 비율 기준)
            foreach (var pair in loofRatios)
            {
                float newX = leftEdge + pair.ratio * newGap;
                SetX(pair.loof, newX);
            }
        }
    }

    private void SetX(Transform t, float x)
    {
        if (t == null) return;
        Vector3 p = t.localPosition;
        p.x = x;
        t.localPosition = p;
    }


    private void ShiftX(Transform t, float dx)
    {
        if (t == null) return;
        Vector3 p = t.localPosition;
        p.x += dx;
        t.localPosition = p;
    }

    private List<Transform> CollectLoofs()
    {
        List<Transform> list = new List<Transform>();
        if (loofRoot == null) return list;

        for (int i = 0; i < loofRoot.childCount; i++)
        {
            Transform t = loofRoot.GetChild(i);
            if (t.gameObject.activeInHierarchy)
                list.Add(t);
        }
        return list;
    }

    private float GetAddPerGap(float speed)
    {
        if (Mathf.Approximately(speed, baseSpeed))
            return 0f;

        if (Mathf.Approximately(speed, 20f))
            return addPerGap_Speed20;

        if (Mathf.Approximately(speed, 25f))
            return addPerGap_Speed25;

        // 정의 안 된 속도는 안전하게 보정 없음
        return 0f;
    }

    private List<Transform> CollectGrounds()
    {
        List<Transform> list = new List<Transform>();
        for (int i = 0; i < groundRoot.childCount; i++)
        {
            Transform t = groundRoot.GetChild(i);
            if (t.gameObject.activeInHierarchy)
                list.Add(t);
        }
        return list;
    }

    private void ShiftRightSide(float cutXLocal, float shift)
    {
        // 1) Ground 자식들
        ShiftChildren(groundRoot, cutXLocal, shift);

        // 2) Loof 자식들 (✅ 요구사항)
        if (loofRoot != null)
            ShiftChildren(loofRoot, cutXLocal, shift);
        // 3) EndPoint는 갭이 생기면 무조건 이동 (✅ 빈칸 방지 핵심)
        if (endPoint != null)
        {
            Vector3 p = endPoint.localPosition;
            p.x += shift;
            endPoint.localPosition = p;
        }

        // StartPoint는 고정 (청크 접합 기준이므로)
    }


    private void ShiftChildren(Transform root, float cutXLocal, float shift)
    {
        if (root == null) return;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform t = root.GetChild(i);
            if (t.localPosition.x >= cutXLocal)
            {
                Vector3 p = t.localPosition;
                p.x += shift;
                t.localPosition = p;
            }
        }
    }

    private float GetLeftEdgeLocalX(Transform t)
    {
        if (preferColliderBounds)
        {
            Collider2D col = t.GetComponent<Collider2D>();
            if (col != null)
                return WorldToLocalX(col.bounds.min.x);
        }

        Renderer r = t.GetComponent<Renderer>();
        if (r != null)
            return WorldToLocalX(r.bounds.min.x);

        return t.localPosition.x;
    }

    private float GetRightEdgeLocalX(Transform t)
    {
        if (preferColliderBounds)
        {
            Collider2D col = t.GetComponent<Collider2D>();
            if (col != null)
                return WorldToLocalX(col.bounds.max.x);
        }

        Renderer r = t.GetComponent<Renderer>();
        if (r != null)
            return WorldToLocalX(r.bounds.max.x);

        return t.localPosition.x;
    }

    private float WorldToLocalX(float worldX)
    {
        Vector3 w = new Vector3(worldX, transform.position.y, transform.position.z);
        return transform.InverseTransformPoint(w).x;
    }
}
