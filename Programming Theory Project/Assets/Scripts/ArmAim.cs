
using UnityEngine;

[DefaultExecutionOrder(150)]
public class ArmAim : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraPivot;

    private void LateUpdate()
    {
        if (cameraPivot == null ||
            transform.parent == null)
        {
            return;
        }

        // Convert the camera's world aiming rotation
        // into the arm's local rotation.
        //
        // This keeps the arm aligned with mouse aim
        // even when the player is performing a flip.
        transform.localRotation =
            Quaternion.Inverse(transform.parent.rotation) *
            cameraPivot.rotation;
    }
}
