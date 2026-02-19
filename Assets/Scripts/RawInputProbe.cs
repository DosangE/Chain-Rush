using UnityEngine;

public class RawInputProbe : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0)) Debug.Log("PROBE LMB");
        if (Input.GetKeyDown(KeyCode.Space)) Debug.Log("PROBE SPACE");
    }
}
