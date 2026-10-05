using UnityEngine;
using UnityEngine.AI;

namespace ZombieStealth.Gameplay
{
    /// <summary>
    /// A circular area (centre = this transform, plus a radius) that a zombie wanders inside.
    /// Answers "where could I go?" — random points that are on the NavMesh and inside the circle.
    /// </summary>
    public class WanderZone : MonoBehaviour
    {
        [SerializeField] float radius = 8f;
        [SerializeField] float navMeshSampleDistance = 1f;

        public float Radius => radius;

        public bool TryGetRandomPoint(out Vector3 point)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);

                // Snap to the closest NavMesh point; reject it if that pushed it outside the zone.
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas)
                    && Contains(hit.position))
                {
                    point = hit.position;
                    return true;
                }
            }

            point = default;
            return false;
        }

        public bool Contains(Vector3 position)
        {
            Vector3 offset = position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            UnityEditor.Handles.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, radius);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, name);
        }
#endif
    }
}
