using UnityEngine;

public class MapPattern : MonoBehaviour
{
    public Transform startPoint;
    public Transform endPoint;

    public float Length => endPoint.position.x - startPoint.position.x;
}
