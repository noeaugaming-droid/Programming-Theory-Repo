using UnityEngine;

// Each enemy gets a personal approach bias; no shared group leader or route.
[DisallowMultipleComponent]
public class EnemyApproachVariation : MonoBehaviour
{
    #region Approach_Variables
    [Header("Individual Route Preference")]
    [SerializeField, Range(0f, 1f)] private float lateralInfluence = 0.65f;
    [SerializeField, Min(0.1f)] private float directionChangeSeconds = 3.5f;
    [SerializeField, Min(0f)] private float approachVariation = 0.35f;
    [SerializeField, Min(0f)] private float closeRange = 5f;
    #endregion

    #region Private_Variables
    private int side = 1;
    private float seed;
    private float nextChange;
    private Transform player;
    #endregion

    #region Public_Methods
    public void Initialize(int preferredSide, float uniqueSeed)
    {
        side = preferredSide < 0 ? -1 : 1;
        seed = uniqueSeed;
        nextChange = Time.time + Random.Range(1f, directionChangeSeconds);
    }

    // Adds a stable, individual side preference to the direction supplied by Spider/Beetle.
    public Vector3 AdjustDirection(Vector3 desired, Vector3 position, Vector3 normal)
    {
        if (player == null)
        {
            GameObject target = GameObject.FindGameObjectWithTag("Player");
            if (target != null) player = target.transform;
        }
        if (player == null || desired.sqrMagnitude < 0.0001f) return desired;
        if (Time.time >= nextChange)
        {
            side = Random.value < 0.5f ? -1 : 1;
            nextChange = Time.time + directionChangeSeconds * Random.Range(0.7f, 1.4f);
        }

        Vector3 toward = Vector3.ProjectOnPlane(player.position - position, normal).normalized;
        if (toward.sqrMagnitude < 0.0001f) return desired;
        Vector3 lateral = Vector3.Cross(normal, toward).normalized;
        float distance = Vector3.Distance(position, player.position);
        float strength = lateralInfluence * Mathf.Clamp01(distance / Mathf.Max(0.1f, closeRange));
        float variation = Mathf.Sin(Time.time * 0.65f + seed) * approachVariation;
        return Vector3.ProjectOnPlane(desired + lateral * (side * strength + variation), normal).normalized;
    }
    #endregion
}
