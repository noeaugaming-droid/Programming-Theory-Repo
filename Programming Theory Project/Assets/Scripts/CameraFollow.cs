using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] GameObject player;
    [SerializeField] Vector3 offset;
    [SerializeField] Vector3 rotationOffset;

    // Update is called once per frame
    void LateUpdate()
    {
        transform.position = player.transform.position
            + player.transform.forward * offset.z
            + player.transform.right * offset.x
            + player.transform.up * offset.y;

        transform.rotation = player.transform.rotation * Quaternion.Euler(rotationOffset); 
    }


}
