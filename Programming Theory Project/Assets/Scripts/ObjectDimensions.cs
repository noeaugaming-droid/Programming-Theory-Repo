
using UnityEngine;

public class MapDimensions : MonoBehaviour
{
    void Start()
    {
        Renderer[] renderers =
            GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.Log("No renderers found!");
            return;
        }

        Bounds mapBounds = renderers[0].bounds;

        foreach (Renderer renderer in renderers)
        {
            mapBounds.Encapsulate(renderer.bounds);
        }

        Vector3 mapSize = mapBounds.size;

        Debug.Log("Map Width (X): " + mapSize.x);
        Debug.Log("Map Height (Y): " + mapSize.y);
        Debug.Log("Map Length (Z): " + mapSize.z);
        Debug.Log("Map Center: " + mapBounds.center);
    }
}
