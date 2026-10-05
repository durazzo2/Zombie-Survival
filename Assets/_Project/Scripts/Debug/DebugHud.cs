using UnityEngine;
using UnityEngine.InputSystem;
using ZombieStealth.AI.Perception;
using ZombieStealth.AI.Zombie;
using ZombieStealth.Audio;

namespace ZombieStealth.Debugging
{
    /// <summary>
    /// F1 on-screen panel: what each zombie is thinking right now (state, BT path, perception, memory,
    /// movement), plus a colour legend for the Gizmos. Debug/presentation only — reads, never changes, the AI.
    /// Works without Gizmos, and in builds.
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        [SerializeField] bool visibleOnStart;
        [SerializeField, Range(10, 32)] int fontSize = 16;
        [SerializeField] bool showLegend = true;

        const float Width = 520f;
        const float Margin = 12f;

        bool visible;
        ZombieBrain[] zombies;
        GameAudio gameAudio;
        GUIStyle textStyle;
        GUIStyle hintStyle;

        void Start()
        {
            visible = visibleOnStart;
            zombies = FindObjectsByType<ZombieBrain>();
            System.Array.Sort(zombies, (a, b) => string.CompareOrdinal(a.name, b.name)); // stable order in the panel
            gameAudio = FindAnyObjectByType<GameAudio>();
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                visible = !visible;
        }

        void OnGUI()
        {
            if (textStyle == null || textStyle.fontSize != fontSize)
            {
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize, richText = true, wordWrap = false };
                hintStyle = new GUIStyle(textStyle) { fontSize = Mathf.RoundToInt(fontSize * 0.8f) };
            }

            if (!visible)
            {
                GUI.Label(new Rect(Margin, Screen.height - 34f, 300f, 30f), "<color=#cccccc>F1: AI debug</color>", hintStyle);
                return;
            }

            string text = "<b>AI DEBUG</b>  <color=#888888>(F1 to hide)</color>";
            if (gameAudio != null)
                text += gameAudio.ChaseLoopActive ? "   <color=#ff3322>chase music ON</color>" : "   <color=#888888>chase music off</color>";
            text += "\n";
            foreach (ZombieBrain zombie in zombies)
                text += "\n" + DescribeZombie(zombie);
            if (showLegend)
                text += "\n" + Legend;

            float height = textStyle.CalcHeight(new GUIContent(text), Width - 20f) + 16f;
            var area = new Rect(Screen.width - Width - Margin, Margin, Width, height);
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none); // twice = darker background
            GUI.Label(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f), text, textStyle);
        }

        /// <summary>Compact block (4–5 lines) so several zombies fit on screen.</summary>
        string DescribeZombie(ZombieBrain brain)
        {
            var motor = brain.GetComponent<ZombieMotor>();
            var vision = brain.GetComponent<ZombieVision>();
            int small = Mathf.RoundToInt(fontSize * 0.85f);

            string text = $"<b>{brain.name}:  <size={Mathf.RoundToInt(fontSize * 1.25f)}>{ZombieDebugText.State(brain)}</size></b>" +
                          $"   <size={small}><color=#aaaaaa>{brain.ActivePath}</color></size>\n";
            if (ZombieDebugText.SubStep(brain, motor) is string subStep)
                text += $"  Step:    {subStep}\n";
            text += $"  Vision:  {ZombieDebugText.Vision(vision)}\n" +
                    $"  Memory:  {ZombieDebugText.LastKnown(brain.Memory)}   {ZombieDebugText.Noise(brain.Memory)}\n" +
                    $"  Moving:  {ZombieDebugText.Movement(motor)}";
            if (brain.TryGetComponent(out ZombieAudio zombieAudio))
                text += $"   <color=#888888>audio: {zombieAudio.LastCue}</color>";
            text += "\n";
            return text;
        }

        const string Legend =
            "<b>Legend</b>\n" +
            "  Cone: <color=#ffffff>white</color> not seen · <color=#ff3322>red</color> sees · <color=#ff9900>orange</color> grace\n" +
            "  LOS ray: <color=#00ff00>clear</color> / <color=#ff0000>blocked</color>    Path: <color=#ffff00>yellow</color>, destination: <color=#00ffff>cyan</color>\n" +
            "  Last known: <color=#ff33ff>magenta</color> live / <color=#9966ff>purple</color> stored\n" +
            "  Noise: <color=#66bfff>blue</color> rings = heard, cube = resolved target\n" +
            "  Ripples: <color=#66bfff>footstep</color> / <color=#ffff00>rock</color>    Search points: <color=#ff8c00>orange</color>, grey = visited\n" +
            "  Wander zone: <color=#4dccff>light-blue circle</color>";
    }
}
