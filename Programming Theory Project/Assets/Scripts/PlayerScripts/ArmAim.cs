using UnityEngine;

public class ArmAim : MonoBehaviour
{
    [SerializeField] private Transform cameraPivot;

    void LateUpdate()
    {
        transform.localRotation = cameraPivot.localRotation;
    }
}
