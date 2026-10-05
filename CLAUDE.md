# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

"Zombie Stealth Extraction": a small first-person stealth game for a university **AI in Video Games** course. The point is to demonstrate game AI clearly: a **custom C# Behavior Tree** (no third-party BT/AI packages), vision + hearing perception, last-known-position memory, investigation, search and NavMesh pathfinding. Readability beats cleverness. The student must be able to explain every major class in a presentation.

Full approved plan: `~/.claude/plans/pasted-content-id-075f-you-are-merry-willow.md`. Its "Approved adjustments" section overrides anything else in it.

**Development is phase by phase.** Do only the phase the user approved, verify it compiles, report the changed files, and stop. Scope is frozen: no combat, inventory, Terrain, procedural generation, advanced animation or extra systems. Use one zombie until Phase 14.

## Current status

- Phase 0 (structure/Git) — **done**: `_Project/` folders, 3 asmdefs, local git (no commits yet, no remote).
- Phase 1 (player) — **done, user-approved**: `Scripts/Player/PlayerMovement.cs` (CharacterController, walk/sprint/crouch toggle, stand-up ceiling check, exposes `IsCrouching`/`IsSprinting`), `PlayerLook.cs` (yaw on body, pitch on child camera, Esc unlocks / LMB relocks cursor). Both find the camera with `GetComponentInChildren<Camera>()`, so no inspector wiring. Test scene: `Tools → Zombie Stealth → Build Player Test Scene` → `Assets/_Project/Scenes/PlayerTest.unity`.
- Phase 2 (test arena + NavMesh) — **done, user-approved**: `Editor/TestArenaBuilder.cs`, menu `Tools → Zombie Stealth → Build Test Arena`. It:
  - creates the layers `Obstacle`, `Player`, `Zombie`, `Throwable` (via `SerializedObject` on TagManager);
  - builds a 30×30 m arena under the `Environment` root (ground, walls, 3 rocks, 4 trees, 1 bush);
  - spawns the player at (0,0,-12) via `PlayerTestSceneBuilder.CreatePlayer`;
  - bakes the `NavMeshSurface` on `Environment` (Children, Physics Colliders, default Humanoid agent, radius 0.5) and saves the data to `Scenes/TestArena_NavMesh.asset`;
  - logs a spawn→north `CalculatePath` sanity check.

  Conventions: every obstacle is on the `Obstacle` layer and static. Rocks use a MeshCollider (non-uniform scale). Tree canopies have no collider. The bush is a trigger with `NavMeshModifier.ignoreFromBuild` (walk-through, will block vision). Materials (URP Lit) are created once in `_Project/Materials/` and reused.
- Phase 3 (BT framework) — **done, user-approved (11/11 tests pass)**:
  - `Scripts/AI/BehaviorTree/`: `Node` (+ `NodeStatus`), `Composite`, `Selector`, `Sequence`, `Condition` (wraps `Func<bool>`), `ActionNode` (OnStart/OnUpdate/OnStop).
  - Pure C#, no UnityEngine.
  - Tests: `Tests/EditMode/BehaviorTreeTests.cs` (11 tests).
- Phase 4 (one zombie + Wander) — **done, user-approved**:
  - `AI/Zombie/ZombieMotor` wraps the NavMeshAgent:
    - `MoveTo` = SamplePosition + `CalculatePath`, and only a PathComplete path is accepted;
    - `HasArrived` uses horizontal distance;
    - `IsStuck` = slower than 0.1 m/s for 3 s, or the path is invalid.
  - `AI/Zombie/ZombieBrain` builds the tree in `BuildTree()` and ticks it every 0.1 s. It exposes `CurrentState` / `ActivePath` by following the Running children. `randomSeed` (0 = random).
  - `AI/Zombie/Actions/WanderAction`: always Running. Move → arrive → wait 1–3 s → repeat. When stuck, it picks a new point.
  - `Gameplay/WanderZone`: centre + radius. `TryGetRandomPoint` = random in circle → SamplePosition → must still be inside the circle.
  - `Debug/ZombieDebugView`: Gizmos for path, destination and state label.
  - The test arena builder now adds a zombie at (3,0,6) and a WanderZone at (0,0,3), r = 8. It wires private fields with the `SetField` (SerializedObject) helper.
- Phase 5 (vision) — **done, user-approved**: `AI/Perception/ZombieVision`, updated every frame in `Update`.
  - Finds the player with `FindAnyObjectByType<PlayerMovement>()`; obstacle mask = `LayerMask.GetMask("Obstacle")`.
  - Checks in order: distance (×0.6 when crouching) → flat FOV angle (110°), skipped within 2.5 m proximity → `Physics.Linecast(eye, player at 75% height, Obstacle, QueryTriggerInteraction.Collide)`.
  - `CanSeePlayer` = last direct sight ≤ 0.4 s ago. `LastSeenPosition` = player feet at the last **direct** sight.
  - Exposes `LastResult` (NoPlayer/OutOfRange/OutsideFieldOfView/Blocked/Visible), `DistanceToPlayer`, `IsLineOfSightBlocked`, `InGracePeriod`.
  - The brain doesn't read vision yet. `ZombieDebugView` reads it directly (cone, proximity disc, LOS ray, label line).
- Phase 6 (chase + catch) — **done, user-approved**:
  - Tree: `ROOT[ CATCH(CanSeePlayer, PlayerInCatchRange ≤1.5 m, CatchPlayer), CHASE(CanSeePlayer, ChasePlayer), WANDER ]`. Each branch builds its own Condition instance.
  - `ChasePlayerAction` sets the run speed (4.2) and calls `motor.MoveTo(vision.PlayerPosition)` every tick.
  - `CatchPlayerAction` stops the motor and calls `GameManager.PlayerCaught()`.
  - Speeds/catch distance are serialized on `ZombieBrain`. Actions set speed in `OnStart` via `ZombieMotor.SetSpeed`. Wander walks at 1.6.
  - `Gameplay/GameManager`: disables PlayerMovement/PlayerLook, sets `Time.timeScale = 0`, shows "CAUGHT" via OnGUI, and reloads the scene by build index after 2.5 real seconds.
  - The builder adds a GameManager object and puts `TestArena.unity` in Build Settings (needed for the reload).
- Phase 7 (last-known position) — **done, user-approved**:
  - `AI/Zombie/ZombieMemory` is a plain C# class with `HasLastKnownPosition`, `LastKnownPosition`, `LastKnownTime`, `SecondsSinceLastKnown`, `RememberPlayerAt()`. `ForgetPlayer()` (called only when a search finishes).
  - `ZombieBrain` owns it (`brain.Memory`). Each tick runs sense → `UpdateMemory()` → `root.Tick()`.
  - `UpdateMemory`: `if (vision.CanSeePlayer) Memory.RememberPlayerAt(vision.PlayerPosition)`. Because CanSeePlayer includes the grace period, the LKP ends where the player actually went behind cover.
  - Vision does not know memory exists.
  - The debug view draws the LKP as an X + pole + dotted line from the zombie: magenta while it's being updated, purple while stored. The label line reads "LKP n s ago".
  - The tree is unchanged.
- Phase 8 (search) — **done, user-approved**:
  - Tree: `ROOT[ CATCH, CHASE, SEARCH(HasLastKnownPosition, SearchLastKnownPosition), WANDER ]`.
  - `Actions/SearchAction` is **one action with internal steps**: GoToLastKnownPosition (run) → LookAround → GoToSearchPoint/LookAround ×N (search speed 3) → `memory.ForgetPlayer()` + Success. It also finishes on a timeout (12 s from start).
  - **Why one action:** a reactive Sequence re-ticks from its first child, so a separate MoveTo node would re-path every tick. Use the same pattern for INVESTIGATE in Phase 9.
  - An abort (CHASE takes over) does not forget, so the next search starts from the newest LKP.
  - Search points: one per slice of the circle, at 0.5–1 × radius (6 m) from the LKP. Each is checked with SamplePosition plus a `NavMesh.CalculatePath` complete from the LKP.
  - `ZombieMotor.LookAround(deg/s)`: stand and turn. It resets on MoveTo/Stop.
  - The brain exposes `Search` for the debug view (orange solid = target, orange wire = pending, grey = visited, labels S1..Sn).
- Phase 9 (hearing + investigate) — **done, user-approved**:
  - `AI/Perception/NoiseSystem`: static `event Action<NoiseEvent> NoiseMade` + `Emit(pos, radius, type)`. `NoiseEvent` = Position, Radius, Type (Footstep/Distraction), TimeStamp.
  - `ZombieHearing` subscribes in OnEnable/OnDisable and keeps the newest noise with distance ≤ radius (no occlusion). The brain drains it with `TryTakeHeardNoise` **every tick**.
  - `Player/PlayerNoise` emits footsteps based on real `CharacterController.velocity` (≥ 0.5 m/s): walk 4 m every 0.5 s, sprint 12 m every 0.35 s, crouch 0 (silent). It's added in `PlayerTestSceneBuilder.CreatePlayer`.
  - **Memory holds one clue (newer wins):**
    - `RememberPlayerAt` clears the noise and `HearNoise` clears the LKP.
    - Brain: `if CanSeePlayer → RememberPlayerAt; else if heard → HearNoise`. Noises heard while seeing are dropped.
    - So INVESTIGATE and SEARCH are never both valid, and after investigating the zombie goes to WANDER (not back to the old LKP).
  - Tree: `ROOT[ CATCH, CHASE, INVESTIGATE(HasHeardNoise, InvestigateNoise), SEARCH, WANDER ]`.
  - `Actions/InvestigateAction` is one action. GoToNoise (2.5 m/s) → LookAround 2.5 s → ForgetNoise + Success. It re-targets if `memory.NoiseTime` changes; if unreachable it forgets the noise and returns Failure.
  - `Debug/NoiseDebugView` (its own scene object) draws fading noise circles. The zombie debug view shows the NOISE marker, the investigate step and the "noise n s ago" label.
- Phase 10 (throwables) — **done, user-approved**:
  - `Player/PlayerThrower`: Attack (LMB) throws `forward*12 + up*3` from 0.6 m in front of the camera. Starts with 2 rocks, max 3. `TryAddRock()`. OnGUI shows "Rocks: n / max".
  - **Relock guard:** it only throws if the cursor was already locked at the previous `LateUpdate`, so the click that re-locks never throws (works regardless of script order).
  - `Gameplay/Throwable`: `Launch(velocity, throwerCollider)` ignores collision with the player and destroys after 9 s. The first `OnCollisionEnter` with relativeVelocity ≥ 2 does `NoiseSystem.Emit(contact, 15, Distraction)`; later bounces are silent.
  - `Gameplay/RockPickup`: trigger; `TryAddRock` succeeds → `SetActive(false)`.
  - No AI changes: rock noise travels through the existing hearing → memory → INVESTIGATE path.
  - The builder adds PlayerThrower to the player and creates `Prefabs/Rock.prefab` **only if missing** (Throwable layer, Rigidbody 0.5 kg, ContinuousDynamic) plus 3 pickups. GameManager also disables PlayerThrower on CAUGHT.
  - Debug: `ZombieDebugView.labelFontSize` (serialized, default 22, range 10–48) with a dark background. The BT path, sub-step and marker labels use 0.7× that size. Distraction noise ripples are yellow, footsteps light blue.
  - Serialized fields on debug components must stay **outside** `#if UNITY_EDITOR`, so the serialization layout is the same in builds.
- Phase 11 (extraction) — **done, user-approved**:
  - `Gameplay/ExtractionZone`: trigger; on PlayerMovement enter → `GameManager.PlayerExtracted()`. Gizmo label "EXTRACTION".
  - `GameManager.EndGame(text, color)` serves both CAUGHT (red) and EXTRACTED (green). The `IsGameOver` guard means the first result wins.
  - `Time.timeScale = 0` also stops physics triggers and BT ticks, so the other result can't fire afterwards.
  - The builder places the zone at (0,0,12.5), opposite the spawn: a 4×3×4 box trigger with collider-less glowing floor + beacon visuals.
  - `GetMaterial(name, color, emission)` now supports emission. The NavMesh check now tests spawn → extraction.
- Phase 12 (full demo scene) — **done, user-approved**:
  - **Editor refactor:** all building code now lives in `Editor/LevelBuilder.cs`. `LevelBuilder.Build(LevelDefinition)` does:
    - scene, light, ground, boundary walls, `PlaceCover` callback;
    - player (+thrower), pickups, extraction, GameManager, NoiseDebugView, wander zone, zombie;
    - bake to `<scene>_NavMesh.asset`, Build Settings, validation.
  - `TestArenaBuilder` and `ZombieDemoBuilder` only contain a `LevelDefinition` + cover layout. Public helpers: `CreateRock/Tree/Bush/Wall`.
  - Obstacles get `NavMeshModifier` area "Not Walkable" in `Place()` (no NavMesh islands on rock/trunk tops). Bushes use `ignoreFromBuild`.
  - **Validation:** the spawn→extraction and zombie→zone paths plus `ExtraReachabilityChecks` must succeed, and a 3 m grid check verifies every walkable ground point connects to the extraction (no sealed pockets). Clear Log/LogError messages.
  - `ZombieDemoBuilder` (menu `Tools → Zombie Stealth → Build Full Demo Scene`) → `Scenes/ZombieStealth.unity`, inserted **first** in Build Settings.
    - Map is 70×90 (x ±35, z ±45). Spawn (0,-41), extraction (0,41), zombie (2,3), wander zone (0,2) r = 15.
    - Lanes: WEST forest = 4 staggered tree columns (x −31/−26/−21/−16), seeded jitter (seed 7), bushes where `(col+2·row)%7==3`, glade at row 6. MIDDLE = 12 boulders (3 low, about 1.5 m) + edge trees + 5 bushes. EAST meadow = sparse bushes/low rock/3 trees.
    - 6 pickups.
- **Bugfix (stuck in INVESTIGATE)** — **done, user-approved**:
  - Root cause: when a higher branch wins, the Selector ticks it first (its `OnStart` runs, e.g. Investigate → `MoveTo`) and only then aborts the old Running branch. Its `OnStop` called `motor.Stop()`, wiping the new destination. With `Destination == null`, `HasArrived`/`IsStuck` are both false forever.
  - Footsteps hid this because each new noise re-targeted. A single rock didn't.
  - Fix:
    - Removed `motor.Stop()` from all action `OnStop`s (Wander, Search, Chase, Investigate). Every action gives its own motor command in `OnStart`.
    - Investigate stores `ResolvedTarget` (= `motor.Destination` after MoveTo) and re-issues MoveTo to that same point if its movement order was cancelled.
  - Test `HigherPriorityBranch_StartsBeforeInterruptedBranchStops` documents the order (12 tests now).
  - The debug view shows the RESOLVED cube + dotted line to the original noise, and the label reads `(GoToNoise, arrived: no, snapped x m)`.
- Phase 13 (debug HUD) — **done, user-approved**:
  - `Debug/DebugHud` (scene object added by LevelBuilder) is a top-right OnGUI panel toggled with **F1** (Input System `Keyboard.current.f1Key`). It's hidden at start (`visibleOnStart`); when hidden, bottom-left shows "F1: AI debug".
  - Per zombie (all `ZombieBrain`s, sorted by name): State, BT path, Step (search/investigate sub-step), Vision, Sees player, Memory (LKP / noise age), Moving (destination, distance, speed). Optional colour legend.
  - Serialized: `fontSize` 16, `showLegend`.
  - `Debug/ZombieDebugText` holds the shared rich-text builders used by both the HUD and the world label (`ZombieDebugView`).
  - Unity 6.6: `FindObjectsByType<T>(FindObjectsSortMode)` is obsolete. Use `FindObjectsByType<T>()`.
- Phase 14 (multiple zombies + tuning) — **done, user-approved**:
  - `LevelDefinition.Zombies` is an array of `(spawn, zoneCentre, zoneRadius)`. LevelBuilder creates "Zombie N" + "WanderZone N", identical components, `avoidancePriority = 40 + 5·i`, and validates each spawn → zone.
  - The test arena has one zombie.
  - Demo zombies (zones don't overlap):
    - Zombie 1 middle-south: spawn (-2,-11), zone (-4,-12) r 9, touching the forest's inner edge.
    - Zombie 2 middle-north: spawn (3,19), zone (3,18) r 9.
    - Zombie 3 meadow: spawn (24,8), zone (24,8) r 8.
  - Pickups (7): spawn, forest S/N, middle (-6,-24), (-4,4) between Z1/Z2, (-3,28), meadow (25,-15).
  - Tuning: `ZombieMotor.arriveDistance` 0.5 → 1.0, so two agents converging on the same rock/search point can both arrive. All other AI numbers are unchanged pending playtest.
  - HUD: compact 4–5 line block per zombie (state coloured, path inline, Step only when relevant). The "Sees player" line is merged into Vision.
- Phase 15 (expanded level + visuals + difficulty) — **implemented, awaiting user test**:
  - `Editor/EnvironmentArt.cs` (new, visual-only):
    - generated meshes saved in `Assets/_Project/Meshes/`: `Rock_0..2` and `Foliage_0..2` (noisy spheres), `Cone`. Regenerated once per build via `CopySerialized`, so GUIDs stay stable;
    - tree models: 0 broadleaf, 1 conifer, 2 birch;
    - bush model (6 foliage blobs);
    - zombie body (hunched capsules/spheres, glowing eyes, 4 colour/scale variants).
    - Randomness is seeded from position (`RandomFor`).
  - `LevelBuilder`:
    - `MakeObstacle()`: Obstacle layer + static + NotWalkable modifier.
    - Trees = root CapsuleCollider (r 0.25·scale) + model. Rocks = noisy MeshCollider. Bushes = root trigger box + model.
    - `CreateMound` (sunk ellipsoid, walkable, Obstacle layer, height h needs radius ≥ 6h).
    - `CreateSolidProp`, `CreatePath` (visual dirt trail), `CreatePebble`/`CreateStump` decor.
    - `GroundPatches` (tiled coloured planes), trilight ambient + exp fog 0.006.
    - Extraction pad: concrete pad, glowing inner circle = trigger, markers, 4 lamp posts with point lights, beacon + light, barriers.
    - Pickups are glowing stone piles (trigger r 0.7).
    - Zombie root = CapsuleCollider (2 m × 0.5) + model.
    - `GetMaterial` is now public, cached per build, and re-applies colour/smoothness 0.12 every build.
    - Validation adds 8 ring points per zombie zone.
  - `ZombieDemoBuilder` map is 100×120 (x ±50, z ±60). Spawn (0,-55), extraction (0,54).
    - Ground patches: spawn z<-46, forest x<-18, rocky −18..18, meadow x>18, north z>28.
    - 3 trails (kept clear, drawn as dirt).
    - Zombies: Z1 (-8,-24) r10; Z2 (4,2) r11; Z3 (35,-8) r10; Z4 (9,38) r8; Z5 (32,22) r8; Z6 (-41,-4) r8.
    - 9 pickups. North campsite: tents, crates, campfire + light, broken fences.
    - The `Layout` spacing class guarantees 2.5 m between solid footprints, 0.3 m for soft (bushes/mounds/kept areas). Scatter areas are kept 3.5 m off the walls. Seed 2025. Logs placed/rejected counts.
  - **AI values unchanged.**
- Phase 15 audio pass — **implemented, awaiting user test** (feedback only, AI untouched):
  - `Scripts/Audio/ZombieAudio` (per zombie, 3D AudioSource) watches in **LateUpdate** (after the brain decides):
    - `vision.CanSeePlayer` false→true → "spotted" (3 s cooldown);
    - `brain.CurrentState` change → CHASE (roar 0.25 s after spotted), INVESTIGATE, SEARCH;
    - WANDER/CATCH → no cue;
    - rare groans while WANDER (8–20 s);
    - 0.6 s minimum between state cues;
    - null clips are skipped; `LastCue` is shown on the HUD.
  - `Scripts/Audio/GameAudio` (2D) plays caught/extracted once (from `GameManager.IsGameOver` + new `PlayerWon`). One shared chase loop fades in while any zombie is CHASE/CATCH and fades out after; it uses unscaled time.
  - `Editor/PlaceholderAudio` synthesizes WAVs into `Assets/_Project/Audio/` **only if missing**: Spotted, Chase, Investigate, Search, Groan1/2, Caught, Extracted, ChaseLoop. Replace a file with a real sound of the same name to keep it across rebuilds. LevelBuilder assigns them (`SetArray` helper for the groans).
  - HUD header shows "chase music ON/off"; each zombie's Moving line shows "audio: <last cue>".
- **Player footsteps** (awaiting user test):
  - `PlayerNoise.Stepped` event (`StepKind` Crouch/Walk/Sprint) fires on every step, including crouched ones, which still emit no AI noise.
  - `Scripts/Audio/PlayerFootstepAudio` (2D AudioSource on the player) plays a random clip, never the same twice in a row. Volume: crouch 0.12 / walk 0.4 / sprint 0.75; pitch 0.9–1.1, ×1.08 when sprinting.
  - Placeholders `Footstep1..3.wav` are wired in `LevelBuilder.CreatePlayer`.
  - The PlaceholderAudio seed is now a hash of the whole name.
- `Assets/_Project/Audio/Spotted.wav` is now the **user's real sound** (MGS-style alert, about 1.7 s, imported Force To Mono). The builder keeps it (it only generates missing files). Never regenerate or overwrite it.
- **Footstep surfaces** (awaiting user test):
  - `Scripts/Gameplay/FootstepSurface` (moved from Audio; enum `SurfaceType` Dirt/Grass/Forest/Gravel; static `Detect(feetPosition)`) is a tag on ground patches, mounds and trails.
  - `PlayerFootstepAudio` raycasts 1 m down from feet+0.3 on each step (`QueryTriggerInteraction.Collide`), uses `GetComponentInParent<FootstepSurface>`, falls back to Dirt, and has per-surface clip arrays (`dirtSteps/grassSteps/forestSteps/gravelSteps`).
  - Trails: the pieces are thin **trigger** BoxColliders under a holder with `FootstepSurface(Dirt)` + `NavMeshModifier.ignoreFromBuild`.
  - `GroundPatches` is now a 4-tuple with the surface; `CreateMound(..., surface)`.
  - Demo: spawn = grass, forest = forest, rocky = gravel, meadow = grass, north = dirt; mounds match their zone. The test arena's single ground = grass.
  - Placeholders: `StepGrass1..3`, `StepForest1..3`, `StepGravel1..3`. Dirt = `Footstep1..3`.
  - AI noise radius does not depend on surface.
- **Surface-dependent hearing** (gameplay change, awaiting playtest):
  - `PlayerNoise` detects the surface on each step and multiplies the noise radius: gravel ×1.3, dirt ×1, grass ×0.9, forest ×0.8 (serialized). Crouch stays 0.
  - `Stepped` event is `(StepKind, SurfaceType)`; footstep audio no longer raycasts itself.
- **Zombie footsteps** (feedback only, no AI noise): `Scripts/Audio/ZombieFootstepAudio` lives on a child "Footsteps" of each zombie with its own 3D AudioSource (Linear rolloff, 2–14 m).
  - Interval = stride 0.8 m / agent speed, clamped 0.28–0.8 s. Volume walk 0.5 / run (≥3 m/s) 0.75. Pitch 0.68–0.8 (heavier).
  - Uses the same per-surface clips (`LevelBuilder.AssignStepClips`).
- **Zombie 6 added** (awaiting playtest): west forest, spawn (-42,-2), zone (-41,-4) r 8. It covers the obstacle-free 3.5 m strip along the west wall (scatter inset), which made wall-hugging + sprinting trivial, plus the forest trail around (-38,0). The demo has **6 zombies**.
- **Zombie 5 added** (awaiting user test): spawn (33,20), zone (32,22) r 8 on the north meadow, the east route's second stage. Gaps to Z3/Z4 zones are about 12 m. Avoidance priority 60. The meadow pickup (41,14) now sits right before it. The demo has **5 zombies**.
- Namespaces: `ZombieStealth.AI.BehaviorTree`, `.AI.Zombie`, `.AI.Zombie.Actions`, `.Gameplay`, `.Player`, `.Debugging` (**not** `.Debug`, which would shadow `UnityEngine.Debug`), `.Editor`.
- Phase 10 note: PlayerLook relocks the cursor on LMB, which is also the future throw button. Resolve this when adding throwing.

Phases: 0 structure/Git · 1 player · 2 tiny test arena + NavMesh · 3 BT framework · 4 one zombie + Wander · 5 Vision · 6 Chase + Catch · 7 Last-known position · 8 Search · 9 Hearing/noise · 10 Throwable · 11 Extraction · 12 full demo scene builder · 13 debug HUD · 14 multiple zombies + tuning · 15 optional polish.

## Environment facts

- Unity **6000.6.4f1** at `/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app`, URP 17.6.0.
- **New Input System only** (`activeInputHandler: 1`). Legacy `UnityEngine.Input` throws. Read input through the project-wide actions: `InputSystem.actions.FindAction("Move")` etc. (map `Player`: Move, Look, Attack=LMB, Interact=E (Hold), Crouch=C, Sprint=LShift, Jump). Asset: `Assets/InputSystem_Actions.inputactions`. Don't edit it.
- AI Navigation 2.0.14 (`NavMeshSurface`), Test Framework 1.8.0.
- Use Unity 6 APIs (`rb.linearVelocity`, `FindObjectsByType`, etc.).

## Commands / verification

The Unity Editor is usually open on this project, so batchmode can't run (project lock). To verify compilation:
1. Bring Unity to the front to trigger an asset refresh: `osascript -e 'tell application "System Events" to set frontmost of (first process whose name is "Unity") to true'`. (osascript has no accessibility permission, so menu items can't be clicked from the shell. The user runs `Tools → …` menu items themselves.)
2. Wait for `.meta` files to appear next to new files, then grep the **project-local** log `Logs/Editor.log` (not `~/Library/Logs/Unity/Editor.log`) for `error CS`.
3. Ignore `SuperProxyClient…` exceptions. They come from the `com.unity.ai.assistant` package.

When the Editor is closed, run EditMode tests with:
```
"/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Temp/results.xml [-testFilter ZombieStealth.Tests.SomeTest] -logFile -
```

## Architecture

All project code lives in `Assets/_Project/`, split into three assemblies:
- `Scripts/` → `ZombieStealth.Runtime` (refs Input System, AI Navigation)
- `Editor/` → `ZombieStealth.Editor` (Editor only)
- `Tests/EditMode/` → `ZombieStealth.Tests.EditMode` (NUnit, `UNITY_INCLUDE_TESTS`)

Key AI design (see plan §4 and §8):
- **BT is re-evaluated from the root every tick.** The selector order is the priority: CATCH > CHASE > SEARCH(LKP) > INVESTIGATE(noise) > WANDER. State transitions come only from selector priority, not from a state machine. **Interruption = Abort:** when a composite returns at child *i*, any later child that was Running last tick gets `Abort()` (recursively), so an `ActionNode` gets `OnStop()` and restarts cleanly next time. This replaced the planned `lastTickFrame` idea because it's frame-independent and unit-testable.
- **Perception senses, brain writes memory, BT reads, actions move.** ZombieBrain copies perception into `ZombieMemory` each tick (perception components never reference memory or the motor) (a typed blackboard, not a string dictionary). Conditions read memory/perception. Actions drive the NavMeshAgent through `ZombieMotor`.
- **Vision:** distance → FOV angle (+ short proximity sense) → `Physics.Linecast` against the Obstacle layer with `QueryTriggerInteraction.Collide` (bush triggers block LOS). **Required 0.4 s grace period** after LOS breaks, so CHASE/SEARCH don't flicker.
- **Hearing:** static `NoiseSystem` event (`NoiseEvent` = position + radius). Zombies react if within the radius. No occlusion. **Newer information wins:** a noise heard during SEARCH clears the LKP, so the zombie investigates the noise instead.
- **Scenes are generated by editor scripts, never by hand-editing `.unity`/`.prefab` YAML.** The Phase 2 test arena builder grows incrementally. The full `Tools → Build Zombie Stealth Demo` builder comes in Phase 12. Prefabs are created only if missing, so manual tuning survives a rebuild.

## Rules

- Don't touch `Library/`, `Temp/`, `Logs/`. Don't delete template files (`Assets/Main.unity`, `Scenes/SampleScene.unity`, `TutorialInfo/`) without asking.
- Git: local only. Push only to remotes under `github.com/tabtale/`. Commit only when asked.
- `README.md` (course-style overview modelled on ibunceski/Project-SentryNode) documents controls, architecture, states, level and tools. Update it when behaviour, numbers or zombie count change.
- **Keep this file updated after every change**: phase status, new commands, architecture decisions.
