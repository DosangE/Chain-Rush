using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MapManager.currentMapSpeed 값을 기준(baseSpeed=13)으로,
/// Ground 사이 "갭"만 속도에 따라 부드럽게 늘리는 스케일러
/// + (추가) enemy/props 등 다른 Root들도 같이 보정 지원
/// </summary>
public class MapChunkGapScaler : MonoBehaviour
{
    [Header("필수 참조")]
    [SerializeField] private Transform groundRoot;
    [SerializeField] private Transform loofRoot;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Header("추가 보정 대상 Root들 (Enemy/Props/Pickups 등)")]
    [Tooltip("여기에 넣은 Root들의 자식들도 갭 보정에 따라 같이 이동/재배치됩니다.")]
    [SerializeField] private Transform[] extraRoots;

    [Header("갭 판정")]
    [SerializeField] private float gapEpsilon = 0.02f;

    [Header("속도 기준 (B안: 부드러운 보간)")]
    [Tooltip("이 속도 이하에서는 갭 보정 없음")]
    [SerializeField] private float baseSpeed = 13f;

    [Tooltip("이 속도에서 갭 보정이 최대값에 도달(권장: 실제 최고 속도 23)")]
    [SerializeField] private float maxSpeed = 23f;

    [Header("갭 보정 (갭 1개당)")]
    [Tooltip("speed=maxSpeed 일 때, 갭 1개당 추가 거리")]
    [SerializeField] private float addPerGap_AtMaxSpeed = 10f;

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
        List<Transform> extras = CollectExtras(); // ✅ enemy/props 등

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
            List<(Transform t, float ratio)> loofRatios = new List<(Transform, float)>();
            for (int k = 0; k < loofs.Count; k++)
            {
                var t = loofs[k];
                float x = t.localPosition.x;
                if (x > leftEdge && x < rightEdge)
                {
                    float ratio = (x - leftEdge) / originalGap;
                    loofRatios.Add((t, ratio));
                }
            }

            // ✅ 2-추가. 이 gap 안에 있는 extra들의 "비율" 미리 저장
            List<(Transform t, float ratio)> extraRatiosInGap = new List<(Transform, float)>();
            for (int k = 0; k < extras.Count; k++)
            {
                var t = extras[k];
                float x = t.localPosition.x;
                if (x > leftEdge && x < rightEdge)
                {
                    float ratio = (x - leftEdge) / originalGap;
                    extraRatiosInGap.Add((t, ratio));
                }
            }

            // ✅ 3. ground 이동 (i+1 이후 누적)
            for (int g = i + 1; g < grounds.Count; g++)
                ShiftX(grounds[g], addPerGap);

            // ✅ 4. endPoint 이동
            ShiftX(endPoint, addPerGap);

            // ✅ 4-추가. extraRoot 자식들: gap의 "오른쪽"에 있는 것들은 같이 밀어줌(누적)
            // (gap 내부에 있는 것들은 아래에서 ratio로 재배치하므로 여기서 건드리지 않음)
            for (int k = 0; k < extras.Count; k++)
            {
                Transform t = extras[k];
                float x = t.localPosition.x;
                if (x >= rightEdge)
                    ShiftX(t, addPerGap);
            }

            // ✅ 5. loof 재배치 (저장된 비율 기준)
            for (int k = 0; k < loofRatios.Count; k++)
            {
                var pair = loofRatios[k];
                float newX = leftEdge + pair.ratio * newGap;
                SetX(pair.t, newX);
            }

            // ✅ 5-추가. extra 재배치 (저장된 비율 기준)
            for (int k = 0; k < extraRatiosInGap.Count; k++)
            {
                var pair = extraRatiosInGap[k];
                float newX = leftEdge + pair.ratio * newGap;
                SetX(pair.t, newX);
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

    private List<Transform> CollectExtras()
    {
        List<Transform> list = new List<Transform>();
        if (extraRoots == null || extraRoots.Length == 0) return list;

        for (int r = 0; r < extraRoots.Length; r++)
        {
            Transform root = extraRoots[r];
            if (root == null) continue;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform t = root.GetChild(i);
                if (t.gameObject.activeInHierarchy)
                    list.Add(t);
            }
        }
        return list;
    }

    /// <summary>
    /// B안: baseSpeed(13)~maxSpeed(23) 구간에서 addPerGap를 0~addPerGap_AtMaxSpeed로 보간
    /// </summary>
    private float GetAddPerGap(float speed)
    {
        if (maxSpeed <= baseSpeed + 0.0001f)
            return 0f;

        if (speed <= baseSpeed + 0.001f)
            return 0f;

        float t = Mathf.InverseLerp(baseSpeed, maxSpeed, speed);
        return Mathf.Lerp(0f, addPerGap_AtMaxSpeed, t);
    }

    private List<Transform> CollectGrounds()
    {
        List<Transform> list = new List<Transform>();
        if (groundRoot == null) return list;

        for (int i = 0; i < groundRoot.childCount; i++)
        {
            Transform t = groundRoot.GetChild(i);
            if (t.gameObject.activeInHierarchy)
                list.Add(t);
        }
        return list;
    }

    // ===== 아래는 기존 코드 유지(다른 곳에서 호출 가능성 때문에 보존) =====

    private void ShiftRightSide(float cutXLocal, float shift)
    {
        ShiftChildren(groundRoot, cutXLocal, shift);

        if (loofRoot != null)
            ShiftChildren(loofRoot, cutXLocal, shift);

        if (endPoint != null)
        {
            Vector3 p = endPoint.localPosition;
            p.x += shift;
            endPoint.localPosition = p;
        }
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
