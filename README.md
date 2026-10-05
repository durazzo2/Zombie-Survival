## Zombie Stealth Extraction - AI in Video Games course project

This repository contains a Unity first-person stealth prototype built around zombie AI. The player spawns at the south edge of an outdoor map and has to reach an extraction point in the north without being caught. Zombies wander their areas, spot the player through vision, hear footsteps and thrown rocks, chase, remember the last-known position, search nearby and eventually give up and return to wandering.

The project is an AI architecture exercise rather than a content-heavy game. The interesting work lives in `Assets/_Project/Scripts`: a custom behavior tree (no third-party AI packages), perception (vision + hearing), a typed memory/blackboard, NavMesh movement, debug visualization, audio feedback, and editor tools that rebuild the complete demo level from code.

## Project Info

- Unity version: `6000.6.4f1` (Unity 6)
- Render pipeline: Universal Render Pipeline (URP 17)
- Main engine systems used: new Input System (project-wide actions), physics, NavMesh (`com.unity.ai.navigation`), audio, editor scripting, Unity Test Framework
- Main scene files:
  - `Assets/_Project/Scenes/ZombieStealth.unity` - the full demo level (first in Build Settings)
  - `Assets/_Project/Scenes/TestArena.unity` - small 30 x 30 m arena used while developing each AI feature
  - `Assets/_Project/Scenes/PlayerTest.unity` - movement-only test scene
- Generated content (rebuilt by the editor tools):
  - `Assets/_Project/Scenes/*_NavMesh.asset`
  - `Assets/_Project/Materials/*`, `Assets/_Project/Meshes/*`, `Assets/_Project/Prefabs/Rock.prefab`
  - `Assets/_Project/Audio/*` (synthesized placeholder sounds, only created if missing)

## What This Prototype Shows

The prototype is built around a stealth loop:

1. The player spawns at the south edge and picks one of three routes.
2. Zombies wander inside their own wander zones using random reachable NavMesh points.
3. Vision checks distance, field of view and line of sight; rocks, tree trunks, walls and tall bushes block it.
4. Footsteps emit noise events whose radius depends on movement style and the ground surface.
5. The player can throw rocks; the impact makes a loud noise that pulls zombies away.
6. A zombie that sees the player chases; if it gets close enough, the player is caught.
7. When line of sight is lost, the zombie runs to the last-known position and searches nearby points.
8. After the search finishes or times out, the zombie forgets the player and returns to wandering.
9. Reaching the extraction pad wins the level.

This makes the project useful for demonstrating behavior trees, perception-driven decisions, memory, NavMesh navigation and data-driven tuning through serialized fields.

## Controls

Input uses the project-wide Input System actions (`Assets/InputSystem_Actions.inputactions`).

| Action | Input |
| --- | --- |
| Move | `W`, `A`, `S`, `D` |
| Look | Mouse |
| Sprint | `Left Shift` |
| Crouch (toggle) | `C` |
| Throw rock | `Left Mouse` |
| Release / re-lock cursor | `Esc` / `Left Mouse` |
| AI debug HUD | `F1` |

Movement also affects stealth:

- Walking makes moderate footstep noise (4 m).
- Sprinting is faster but much louder (12 m).
- Crouching is slower, makes no AI noise and shortens the distance at which zombies can see you.
- The ground changes how far a step carries: gravel x1.3, dirt x1.0, grass x0.9, forest floor x0.8.

The player starts with 2 rocks (max 3). Glowing stone piles on the map give one more rock each.

## Running the Project

1. Open the repository folder in Unity Hub.
2. Use Unity `6000.6.4f1` or a compatible Unity 6 version.
3. Open `Assets/_Project/Scenes/ZombieStealth.unity`.
4. Press Play. Turn on **Gizmos** in the Game view to see the AI debug drawing, and press `F1` for the HUD.

If the level needs to be regenerated, use the editor menu:

```text
Tools > Zombie Stealth > Build Full Demo Scene
```

That command is defined in `Assets/_Project/Editor/ZombieDemoBuilder.cs` (layout) and `Assets/_Project/Editor/LevelBuilder.cs` (shared building code). It recreates the scene, lighting, terrain and cover, the player and all zombies, pickups and extraction, configures layers, assigns AI and audio components, bakes the NavMesh and validates it. The Console reports every reachability check; red lines mean something is unreachable.

Other tools in the same menu:

```text
Tools > Zombie Stealth > Build Test Arena
Tools > Zombie Stealth > Build Player Test Scene
```

Running the unit tests: `Window > General > Test Runner > EditMode > Run All`.

## Repository Layout

```text
Assets/_Project/
  Audio/                    Footstep and zombie/game state cues (placeholders unless replaced)
  Editor/
    LevelBuilder.cs         Shared builder: scene, terrain, obstacles, actors, NavMesh bake + validation
    ZombieDemoBuilder.cs    Full demo level layout (routes, zombies, pickups, spacing rules)
    TestArenaBuilder.cs     Small test arena layout
    EnvironmentArt.cs       Visual-only models: trees, rocks, bushes, zombie body, generated meshes
    PlaceholderAudio.cs     Synthesizes placeholder WAVs for every sound cue
    PlayerTestSceneBuilder.cs
  Scripts/
    AI/BehaviorTree/        Node, Composite, Selector, Sequence, Condition, ActionNode
    AI/Zombie/
      ZombieBrain.cs        Builds the tree; each tick: sense -> remember -> decide
      ZombieMemory.cs       Typed blackboard: last-known player position + heard noise
      ZombieMotor.cs        Thin NavMeshAgent wrapper (MoveTo, Stop, HasArrived, IsStuck, LookAround)
      Actions/              Wander, Chase, Catch, Search, Investigate
    AI/Perception/
      ZombieVision.cs       Distance, FOV, line of sight, crouch, proximity, grace period
      ZombieHearing.cs      Hears NoiseSystem events within their radius
      NoiseSystem.cs        Static noise event bus + NoiseEvent
    Player/                 PlayerMovement, PlayerLook, PlayerNoise, PlayerThrower
    Gameplay/               GameManager, ExtractionZone, WanderZone, Throwable, RockPickup, FootstepSurface
    Audio/                  ZombieAudio, ZombieFootstepAudio, PlayerFootstepAudio, GameAudio
    Debug/                  ZombieDebugView, DebugHud, NoiseDebugView, ZombieDebugText
  Tests/EditMode/
    BehaviorTreeTests.cs    Unit tests for the behavior tree framework
Packages/
  manifest.json             Unity package manifest
ProjectSettings/            Unity project and editor settings
```

## AI Architecture

Each zombie is the same set of components: `ZombieBrain`, `ZombieMotor`, `ZombieVision`, `ZombieHearing`, `ZombieDebugView` and `ZombieAudio`. There is no shared memory or communication between zombies; each one perceives, remembers and decides on its own.

The responsibilities are kept separate:

- **Perception senses** - `ZombieVision` and `ZombieHearing` only expose data.
- **The brain remembers** - `ZombieBrain` copies perception into `ZombieMemory` every tick.
- **The behavior tree decides** - conditions read memory/perception.
- **Actions move** - actions only drive the NavMeshAgent through `ZombieMotor`.

### Behavior Tree

The primitives live in `Assets/_Project/Scripts/AI/BehaviorTree` and are plain C# (no Unity dependency):

- `Node` defines `Tick()`, the `Success` / `Failure` / `Running` status and `Abort()`.
- `Selector` tries children in priority order and returns the first one that doesn't fail.
- `Sequence` runs children in order and stops at the first one that doesn't succeed.
- `Condition` wraps a `Func<bool>`.
- `ActionNode` gives actions an `OnStart` / `OnUpdate` / `OnStop` lifecycle.

The tree is re-evaluated from the root 10 times per second. `ZombieBrain.BuildTree()` creates:

```text
Selector ROOT
├─ Sequence CATCH        CanSeePlayer, PlayerInCatchRange (1.5 m), CatchPlayer
├─ Sequence CHASE        CanSeePlayer, ChasePlayer
├─ Sequence INVESTIGATE  HasHeardNoise, InvestigateNoise
├─ Sequence SEARCH       HasLastKnownPosition, SearchLastKnownPosition
└─ Action   WANDER
```

There is no explicit state machine. Every state change is simply "a higher-priority branch's condition became true". When that happens, the composite calls `Abort()` on the branch that was running, so its action receives `OnStop()` and starts fresh next time.

One rule matters here: the new branch's `OnStart` runs *before* the interrupted branch's `OnStop` (the selector has to tick the new branch to know it wins). For that reason `OnStop` never stops the shared motor; every action gives its own movement command when it starts. A unit test documents this order.

### Memory

`ZombieMemory` is a small typed blackboard rather than a string-keyed dictionary:

- `HasLastKnownPosition`, `LastKnownPosition`, `LastKnownTime`
- `HasHeardNoise`, `NoisePosition`, `NoiseTime`

It follows a **newer information wins** rule. A sighting clears any older noise, and a new noise (heard while the player is not visible) clears the old last-known position. So a rock thrown while a zombie is searching immediately redirects it to investigate the rock.

## Zombie States

`ZombieBrain.CurrentState` exposes the running top-level branch:

| State | Meaning |
| --- | --- |
| `WANDER` | Walk to random reachable points inside the wander zone, pause 1-3 s, repeat |
| `INVESTIGATE` | Walk to the remembered noise (snapped onto the NavMesh), look around ~2.5 s, forget it |
| `SEARCH` | Run to the last-known position, look around, visit 3 nearby reachable points, give up after 12 s |
| `CHASE` | Run at the player, re-pathing every tick |
| `CATCH` | Player within 1.5 m while visible: the run ends with CAUGHT |

The state, active BT path and current sub-step are shown above each zombie and in the `F1` HUD.

## Perception Systems

### Vision

`ZombieVision` checks the player every frame, in order:

1. Distance: 18 m, or x0.6 while the player crouches.
2. Field of view: 110 degrees, ignored inside a 2.5 m proximity radius.
3. Line of sight: `Physics.Linecast` from the zombie's eyes to the player's upper body against the `Obstacle` layer. Triggers count, so walk-through bushes still hide the player.

After direct sight is lost, `CanSeePlayer` stays true for a 0.4 s grace period. This prevents the zombie flickering between chasing and searching around thin tree trunks, and lets the last-known position follow the player to where they actually went behind cover.

### Hearing

`NoiseSystem` is a simple global noise broadcaster. Footsteps and rock impacts call:

```csharp
NoiseSystem.Emit(position, radius, NoiseType.Footstep);
```

Every `ZombieHearing` checks whether it is inside the radius and keeps the newest heard noise. There is no sound occlusion. The brain takes that noise each tick and writes it to memory only when the player is not visible, so hearing can never pull a zombie away from a chase.

## Movement and Search

- `ZombieMotor` is the only class that touches the `NavMeshAgent`. `MoveTo` snaps the target onto the NavMesh with `NavMesh.SamplePosition` and only accepts complete paths. `IsStuck` reports no real movement for 3 s so actions can recover.
- `WanderZone` gives random NavMesh points inside a circle.
- `SearchAction` spreads its search points around the last-known position (one per slice of the circle). Each point must be reachable from it.
- `InvestigateAction` resolves the noise position to a reachable point once and uses that same point for moving and for the arrival check. A newer noise re-targets it, and an unreachable noise is dropped.

## Distraction

`PlayerThrower` throws a physics rock (`Throwable`). On its first hard impact it emits one 15 m `Distraction` noise through the same `NoiseSystem`; later bounces are silent. The rock has no reference to any zombie, so the flow is:

```text
Throwable -> NoiseSystem -> ZombieHearing -> ZombieMemory -> Behavior Tree -> InvestigateAction
```

## The Demo Level

The full level is 100 x 120 m and is generated deterministically (hand-placed key features plus seeded scatter). A spacing system keeps at least 2.5 m between solid obstacles and keeps trails, spawn, extraction and zombie spots clear, so the NavMesh never gets gaps narrower than the agent.

| Area | Character | Zombies |
| --- | --- | --- |
| West forest | Dense mixed trees, tall bushes, logs, clearings; safest, longest route | Zombie 6 (middle of the forest, covers the wall strip) |
| Central rocks | Boulder clusters, a rocky ridge, a raised mound; most direct, highest pressure | Zombie 1 (south), Zombie 2 (centre) |
| East meadow | Open grass, gentle hills, sparse cover; fastest, most exposed | Zombie 3 (middle), Zombie 5 (north stretch) |
| North zone | Abandoned campsite, broken fences, mixed cover before extraction | Zombie 4 (south-east of the pad, never on it) |

Rock pickups are placed shortly before each risky stretch.

## Debugging and Feedback

The AI is meant to be readable while testing and presenting:

- `ZombieDebugView` (Gizmos) draws:
  - the vision cone (white = not seen, red = sees, orange = grace) and the proximity circle;
  - the line-of-sight ray (green = clear, red = blocked);
  - the last-known position marker and the remembered noise marker;
  - the resolved investigate target and the search points;
  - the NavMesh path and destination;
  - a label with state, vision, memory and BT path (font size adjustable).
- `WanderZone` draws its circle, and `ExtractionZone` its label.
- `NoiseDebugView` shows every noise as a fading ripple of its real radius (blue = footstep, yellow = rock).
- `DebugHud` (`F1`) lists every zombie: state, BT path, sub-step, vision, memory, movement and last audio cue, plus a colour legend.
- `ZombieAudio` plays 3D cues on state *transitions* only: spotted, chase, investigate, search, plus rare wander groans.
- `GameAudio` plays the caught/extracted sounds and one shared chase loop while any zombie is chasing.
- `PlayerFootstepAudio` and `ZombieFootstepAudio` play surface-dependent footsteps (forest, grass, gravel, dirt).

## Extending the Zombie AI

To add a new behavior:

1. Create an `ActionNode` subclass (implement `OnUpdate`, optionally `OnStart` / `OnStop`) and any `Condition`s it needs.
2. Store anything it needs to remember in `ZombieMemory`, written by `ZombieBrain.UpdateMemory()` from perception.
3. Insert a new `Sequence` into `ZombieBrain.BuildTree()`. Its position in the root `Selector` is its priority.
4. In `OnStart`, give the motor its own command (`MoveTo`, `Stop`, `LookAround`); never stop the motor in `OnStop`.

## Notes for Reviewers

- `Library/`, `Temp/`, `Logs/` and `UserSettings/` are generated by Unity and not part of the source.
- Scenes are generated by the editor tools. Manual edits to `ZombieStealth.unity` or `TestArena.unity` are overwritten when the builder runs again; tune values in the builder or in prefabs/materials instead.
- If zombie movement behaves oddly after changing the layout, rebuild the scene; the builder rebakes and validates the NavMesh.
- Sounds in `Assets/_Project/Audio` are generated placeholders unless replaced. Drop in a file with the same name to keep it across rebuilds.

## Current Status

The repo represents a playable stealth AI demo with:

- First-person movement with walk, sprint, crouch and rock throwing.
- Six independent zombies running the same custom behavior tree.
- Vision and hearing perception, memory with a newer-information-wins rule.
- Wander, investigate, search, chase and catch behaviors.
- Win (extraction) and lose (caught) states.
- Debug gizmos, an `F1` HUD, state-transition audio and surface-dependent footsteps.
- A reproducible, validated, editor-generated level.

It is best understood as an AI systems prototype and coursework demonstration of behavior-tree-driven stealth AI in Unity.
