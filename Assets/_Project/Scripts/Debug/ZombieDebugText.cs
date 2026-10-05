using UnityEngine;
using ZombieStealth.AI.Perception;
using ZombieStealth.AI.Zombie;

namespace ZombieStealth.Debugging
{
    /// <summary>
    /// Builds the (rich-text, coloured) debug strings describing what a zombie is thinking.
    /// Shared by the world-space label (ZombieDebugView) and the F1 HUD (DebugHud).
    /// </summary>
    public static class ZombieDebugText
    {
        /// <summary>Progress inside the running Search/Investigate action, or null if neither runs.</summary>
        public static string SubStep(ZombieBrain brain, ZombieMotor motor)
        {
            if (brain.Search.IsRunning)
            {
                var search = brain.Search;
                return search.CurrentStep == AI.Zombie.Actions.SearchAction.Step.GoToLastKnownPosition
                    ? "going to LKP"
                    : $"{search.CurrentStep}, point {Mathf.Max(0, search.CurrentPointIndex + 1)}/{search.SearchPoints.Count}";
            }

            if (brain.Investigate.IsRunning)
            {
                var investigate = brain.Investigate;
                var memory = brain.Memory;
                string snap = investigate.ResolvedTarget is Vector3 target && memory.HasHeardNoise
                    ? $", snapped {Vector3.Distance(memory.NoisePosition, target):0.0} m"
                    : "";
                return $"{investigate.CurrentStep}, arrived: {(motor.HasArrived ? "yes" : "no")}{snap}";
            }

            return null;
        }

        /// <summary>BT state name coloured like its gizmos (CHASE/CATCH red, SEARCH purple, INVESTIGATE blue).</summary>
        public static string State(ZombieBrain brain)
        {
            string color = brain.CurrentState switch
            {
                "CATCH" or "CHASE" => "#ff3322",
                "SEARCH" => "#b366ff",
                "INVESTIGATE" => "#66bfff",
                _ => "#ffffff",
            };
            return $"<color={color}>{brain.CurrentState}</color>";
        }

        public static string Vision(ZombieVision vision)
        {
            if (vision.HasDirectSight)
                return "<color=#ff3322>SEES PLAYER</color>";
            if (vision.InGracePeriod)
                return "<color=#ff9900>SEES PLAYER (grace)</color>";

            string reason = vision.LastResult switch
            {
                ZombieVision.Result.Blocked => "LOS blocked",
                ZombieVision.Result.OutsideFieldOfView => "outside FOV",
                ZombieVision.Result.OutOfRange => "out of range",
                _ => "no player",
            };
            return $"<color=#aaaaaa>{reason} ({vision.DistanceToPlayer:0.0} m)</color>";
        }

        public static string LastKnown(ZombieMemory memory)
        {
            return memory.HasLastKnownPosition
                ? $"<color=#b366ff>LKP {memory.SecondsSinceLastKnown:0.0} s ago</color>"
                : "<color=#aaaaaa>LKP none</color>";
        }

        public static string Noise(ZombieMemory memory)
        {
            return memory.HasHeardNoise
                ? $"<color=#66bfff>noise {memory.SecondsSinceNoise:0.0} s ago</color>"
                : "<color=#aaaaaa>noise none</color>";
        }

        public static string Movement(ZombieMotor motor)
        {
            if (motor.Destination is not Vector3 destination)
                return "<color=#aaaaaa>standing</color>";

            Vector3 offset = destination - motor.transform.position;
            offset.y = 0f;
            return $"<color=#00ffff>→ ({destination.x:0.0}, {destination.z:0.0})</color>  {offset.magnitude:0.0} m at {motor.Agent.speed:0.0} m/s";
        }
    }
}
