using UnityEngine;
using TMPro;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class TheyBeBugginFloorGuard : MonoBehaviour
{
    private Collider floor;
    private Collider[] bodyColliders;
    private Rigidbody rb;
    private void Awake() { rb = GetComponent<Rigidbody>(); bodyColliders = GetComponentsInChildren<Collider>(); }
    private void OnEnable() { LocateFloor(); KeepAboveFloor(); }
    private void FixedUpdate() { KeepAboveFloor(); }
    private void LateUpdate() { KeepAboveFloor(); }
    private void LocateFloor()
    {
        if (floor) return;
        var supervisor = FindFirstObjectByType<TheyBeBugginSpawnSupervisor>();
        if (supervisor) floor = supervisor.BaseFloor;
    }
    private void KeepAboveFloor()
    {
        if (!floor) LocateFloor();
        if (!floor) return;
        float lowest = float.PositiveInfinity;
        foreach (var c in bodyColliders)
            if (c && c.enabled && !c.isTrigger) lowest = Mathf.Min(lowest, c.bounds.min.y);
        if (float.IsPositiveInfinity(lowest)) lowest = transform.position.y;
        float shift = floor.bounds.max.y + 0.035f - lowest;
        if (shift <= 0f) return;
        transform.position += Vector3.up * shift;
        if (rb && !rb.isKinematic && rb.linearVelocity.y < 0f)
        {
            Vector3 v = rb.linearVelocity; v.y = 0f; rb.linearVelocity = v;
        }
        Physics.SyncTransforms();
    }
}

