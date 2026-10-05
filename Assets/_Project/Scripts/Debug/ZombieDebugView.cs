using UnityEngine;
using ZombieStealth.AI.Perception;
using ZombieStealth.AI.Zombie;
using ZombieStealth.AI.Zombie.Actions;

namespace ZombieStealth.Debugging
{
    /// <summary>
    /// Draws the zombie's AI state with Gizmos: state label, vision cone, line-of-sight ray,
    /// remembered last-known player position, remembered noise, search points, current destination and NavMesh path.
    /// Visible in the Scene view, and in the Game view when its "Gizmos" button is on.
    /// </summary>
    [RequireComponent(typeof(ZombieBrain), typeof(ZombieMotor), typeof(ZombieVision))]
    public class ZombieDebugView : MonoBehaviour
    {
        [Tooltip("Font size (pixels) of the text above the zombie. Marker labels and the BT path scale with it.")]
        [SerializeField, Range(10, 48)] int labelFontSize = 22;

#if UNITY_EDITOR
        static readonly Color IdleColor = new Color(1f, 1f, 1f, 0.6f);
        static readonly Color GraceColor = new Color(1f, 0.6f, 0f);
        static readonly Color SeesColor = new Color(1f, 0.15f, 0.1f);
        static readonly Color ClearRayColor = Color.green;
        static readonly Color BlockedRayColor = Color.red;
        static readonly Color MemoryLiveColor = new Color(1f, 0.2f, 1f);         // magenta: being updated right now
        static readonly Color MemoryStoredColor = new Color(0.6f, 0.3f, 1f);     // purple: stored, player not seen
        static readonly Color SearchTargetColor = new Color(1f, 0.55f, 0f);      // orange, solid: current target
        static readonly Color SearchPendingColor = new Color(1f, 0.55f, 0f, 0.8f); // orange, wire: not visited yet
        static readonly Color SearchVisitedColor = new Color(0.5f, 0.5f, 0.5f, 0.8f); // grey: visited
        static readonly Color NoiseMemoryColor = new Color(0.4f, 0.75f, 1f);     // light blue: remembered noise

        static Texture2D labelBackground;
        GUIStyle labelStyle;    // big label above the zombie (dark background for readability)
        GUIStyle markerStyle;   // smaller labels on world markers (LAST KNOWN, NOISE, S1...)

        int SmallSize => Mathf.RoundToInt(labelFontSize * 0.7f);

        void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            var brain = GetComponent<ZombieBrain>();
            var motor = GetComponent<ZombieMotor>();
            var vision = GetComponent<ZombieVision>();

            UpdateStyles();

            DrawMovement(motor);
            DrawVision(vision);
            DrawMemory(brain.Memory, vision);
            DrawSearch(brain.Search);
            DrawNoiseMemory(brain.Memory);
            DrawInvestigateTarget(brain.Investigate, brain.Memory);

            string state = brain.CurrentState;
            if (ZombieDebugText.SubStep(brain, motor) is string subStep)
                state += $" <size={SmallSize}>({subStep})</size>";
            string memory = $"{ZombieDebugText.LastKnown(brain.Memory)}  {ZombieDebugText.Noise(brain.Memory)}";
            string label = $"{state}\n{ZombieDebugText.Vision(vision)}\n{memory}\n<size={SmallSize}>{brain.ActivePath}</size>";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.4f, label, labelStyle);
        }

        void UpdateStyles()
        {
            if (labelStyle != null && labelStyle.fontSize == labelFontSize)
                return;

            if (labelBackground == null)
            {
                labelBackground = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                labelBackground.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
                labelBackground.Apply();
            }

            labelStyle = new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                fontSize = labelFontSize,
                richText = true,
                alignment = TextAnchor.LowerCenter,
                padding = new RectOffset(8, 8, 4, 4),
                normal = { textColor = Color.white, background = labelBackground }
            };
            markerStyle = new GUIStyle(labelStyle) { fontSize = SmallSize, normal = { textColor = Color.white, background = null } };
        }

        static void DrawMovement(ZombieMotor motor)
        {
            if (motor.Destination is not Vector3 destination)
                return;

            // Current NavMesh path
            Vector3[] corners = motor.Agent.path.corners;
            Gizmos.color = Color.yellow;
            for (int i = 0; i < corners.Length - 1; i++)
                Gizmos.DrawLine(corners[i], corners[i + 1]);

            // Destination marker
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(destination, 0.3f);
            Gizmos.DrawLine(destination, destination + Vector3.up * 2f);
        }

        void DrawVision(ZombieVision vision)
        {
            Color stateColor = vision.HasDirectSight ? SeesColor : vision.InGracePeriod ? GraceColor : IdleColor;
            Vector3 feet = transform.position + Vector3.down;   // capsule pivot is 1 m above the ground
            Vector3 coneStart = new Vector3(transform.position.x, feet.y + 0.05f, transform.position.z);

            // Full view radius (faint) and the radius actually used right now (shorter when the player crouches).
            UnityEditor.Handles.color = new Color(1f, 1f, 1f, 0.15f);
            UnityEditor.Handles.DrawWireDisc(coneStart, Vector3.up, vision.ViewDistance);

            // Field-of-view cone at the effective distance
            Vector3 coneEdge = Quaternion.Euler(0f, -vision.FieldOfView / 2f, 0f) * transform.forward;
            UnityEditor.Handles.color = new Color(stateColor.r, stateColor.g, stateColor.b, 0.12f);
            UnityEditor.Handles.DrawSolidArc(coneStart, Vector3.up, coneEdge, vision.FieldOfView, vision.EffectiveViewDistance);
            UnityEditor.Handles.color = stateColor;
            UnityEditor.Handles.DrawWireArc(coneStart, Vector3.up, coneEdge, vision.FieldOfView, vision.EffectiveViewDistance);
            UnityEditor.Handles.DrawLine(coneStart, coneStart + coneEdge * vision.EffectiveViewDistance);
            UnityEditor.Handles.DrawLine(coneStart, coneStart + Quaternion.Euler(0f, vision.FieldOfView, 0f) * coneEdge * vision.EffectiveViewDistance);

            // Proximity awareness circle
            UnityEditor.Handles.color = new Color(stateColor.r, stateColor.g, stateColor.b, 0.5f);
            UnityEditor.Handles.DrawWireDisc(coneStart, Vector3.up, vision.ProximityRadius);

            // Line-of-sight ray: only drawn when it was actually tested (player in range and in view/close).
            if (vision.LastResult == ZombieVision.Result.Visible || vision.LastResult == ZombieVision.Result.Blocked)
            {
                Gizmos.color = vision.IsLineOfSightBlocked ? BlockedRayColor : ClearRayColor;
                Gizmos.DrawLine(vision.EyePosition, vision.PlayerTargetPosition);
            }
        }

        void DrawMemory(ZombieMemory memory, ZombieVision vision)
        {
            if (!memory.HasLastKnownPosition)
                return;

            Vector3 p = memory.LastKnownPosition;
            Color color = vision.CanSeePlayer ? MemoryLiveColor : MemoryStoredColor;

            // Big X on the ground + a tall pole so it's easy to spot from the player's view.
            Gizmos.color = color;
            const float size = 0.6f;
            Gizmos.DrawLine(p + new Vector3(-size, 0.05f, -size), p + new Vector3(size, 0.05f, size));
            Gizmos.DrawLine(p + new Vector3(-size, 0.05f, size), p + new Vector3(size, 0.05f, -size));
            Gizmos.DrawLine(p, p + Vector3.up * 2.5f);
            Gizmos.DrawWireSphere(p + Vector3.up * 2.5f, 0.15f);

            UnityEditor.Handles.color = color;
            UnityEditor.Handles.DrawDottedLine(transform.position, p + Vector3.up, 4f);
            UnityEditor.Handles.Label(p + Vector3.up * 2.8f, "LAST KNOWN", markerStyle);
        }

        void DrawSearch(SearchAction search)
        {
            if (!search.IsRunning)
                return;

            for (int i = 0; i < search.SearchPoints.Count; i++)
            {
                Vector3 p = search.SearchPoints[i] + Vector3.up * 0.3f;
                if (i < search.CurrentPointIndex)
                {
                    Gizmos.color = SearchVisitedColor;
                    Gizmos.DrawWireSphere(p, 0.2f);
                }
                else if (i == search.CurrentPointIndex)
                {
                    Gizmos.color = SearchTargetColor;
                    Gizmos.DrawSphere(p, 0.35f);
                }
                else
                {
                    Gizmos.color = SearchPendingColor;
                    Gizmos.DrawWireSphere(p, 0.35f);
                }
                UnityEditor.Handles.Label(p + Vector3.up * 0.5f, $"S{i + 1}", markerStyle);
            }
        }

        void DrawNoiseMemory(ZombieMemory memory)
        {
            if (!memory.HasHeardNoise)
                return;

            Vector3 p = memory.NoisePosition + Vector3.up * 0.05f;
            UnityEditor.Handles.color = NoiseMemoryColor;
            UnityEditor.Handles.DrawWireDisc(p, Vector3.up, 0.3f);
            UnityEditor.Handles.DrawWireDisc(p, Vector3.up, 0.6f);
            UnityEditor.Handles.DrawLine(p, p + Vector3.up * 2f);
            UnityEditor.Handles.DrawDottedLine(transform.position, p + Vector3.up, 4f);
            UnityEditor.Handles.Label(p + Vector3.up * 2.3f, "NOISE", markerStyle);
        }

        /// <summary>Resolved (NavMesh) investigate target, linked to the original noise position.</summary>
        void DrawInvestigateTarget(InvestigateAction investigate, ZombieMemory memory)
        {
            if (!investigate.IsRunning || investigate.ResolvedTarget is not Vector3 target)
                return;

            UnityEditor.Handles.color = NoiseMemoryColor;
            UnityEditor.Handles.DrawWireCube(target + Vector3.up * 0.25f, Vector3.one * 0.5f);
            if (memory.HasHeardNoise)
                UnityEditor.Handles.DrawDottedLine(memory.NoisePosition, target, 3f);
            UnityEditor.Handles.Label(target + Vector3.up * 0.9f, "RESOLVED", markerStyle);
        }
#endif
    }
}
