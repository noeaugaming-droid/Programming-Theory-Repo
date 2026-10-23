
using UnityEngine;

public class ArmAim : MonoBehaviour
{
    [SerializeField] private Transform cameraPivot;

    void LateUpdate()
    {
        if (cameraPivot != null)
            transform.localRotation = cameraPivot.localRotation;
    }
}
