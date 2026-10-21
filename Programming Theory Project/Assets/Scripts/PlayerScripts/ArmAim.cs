
using UnityEngine;

public class ArmAim : MonoBehaviour
{
    [SerializeField] Transform cameraPivot;

    void LateUpdate()
    {
        // Make the arm follow the camera's vertical rotation
        transform.localRotation = cameraPivot.localRotation;
    }
}
