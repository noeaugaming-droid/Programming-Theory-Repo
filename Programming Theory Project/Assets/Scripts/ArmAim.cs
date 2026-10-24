
using UnityEngine;

public class ArmAim : MonoBehaviour
{
    #region Camera_And_Aiming_Variables
    // Values and references used by this part of the script.
    [SerializeField] private Transform cameraPivot;

    void LateUpdate()
    {
        if (cameraPivot != null)
            transform.localRotation = cameraPivot.localRotation;
    }
    #endregion
}
