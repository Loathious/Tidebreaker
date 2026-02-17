using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset;

    void LateUpdate()
    {
        if (target == null) return;

        transform.position = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            -10f
        );
    }
}