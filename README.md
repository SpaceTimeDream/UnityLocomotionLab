# Unity Locomotion Lab

A Unity 2D character-controller project for comparing two locomotion approaches in one playable test course: a dynamic Rigidbody2D controller and a custom simulated-velocity controller. The project focuses on movement behavior, collision handling, modular controller architecture, runtime diagnostics, character animation, and desktop gamepad integration.

## Project overview

- **Unity version:** Unity 6 — 6000.5.4f1
- **Main scene:** [LocomotionComparisonDemo.unity](Assets/Portfolio/Scenes/LocomotionComparisonDemo.unity)
- **Rigidbody player prefab:** [RigidbodyPlayerCapsule.prefab](Assets/Portfolio/Prefabs/Locomotion/RigidbodyPlayerCapsule.prefab)
- **Simulated player prefab:** [SimulatedPlayerCapsule.prefab](Assets/Portfolio/Prefabs/Locomotion/SimulatedPlayerCapsule.prefab)

The scene places both players on the same traversal course so you can switch between them and compare how each implementation handles acceleration, jumps, slopes, and collision transitions.

## What it demonstrates

- **Two locomotion models.** Compare Unity-driven Rigidbody2D motion with a controller that calculates its own velocity and resolves movement before translating the player.
- **Modular controller design.** Controller, handler, movement, jump, physics, grounding, collision, and platform responsibilities are separated into focused components.
- **Movement tuning.** Ground and air acceleration/deceleration, jump buffering, coyote time, jump anticipation and early release, gravity tuning, walkable slopes, and steep-slope blocking.
- **Character integration.** Shared input, animation-state coordination, facing and turn behavior, automatic head look, camera following, and switching the active player.
- **Runtime diagnostics.** A comparison HUD reports locomotion state and physics values. Gizmos expose relevant movement, ground-probe, and collision information. HUD layout and font sizes can be tuned in the Inspector.
- **Input and controller feedback.** Keyboard/mouse and gamepad controls share the same actions. The project includes DualShock and DualSense components with rumble support and additional device-specific output features where the active Unity backend supports them.

## Quick start

1. Open the repository folder in Unity Hub as an existing project.
2. Use Unity **6000.5.4f1** and allow Unity Package Manager to resolve the project dependencies.
3. Open **Assets/Portfolio/Scenes/LocomotionComparisonDemo.unity**.
4. Enter Play Mode and use the controls below to move around the course and switch players.

The comparison scene and player prefabs are already included; no scene-generation step is required.

## Controls

| Action | Keyboard / Mouse | Generic Gamepad | DualSense / DualShock |
|---|---|---|---|
| Move horizontally | A / D | Left stick left/right or D-pad left/right | Left stick left/right or D-pad left/right |
| Look up while grounded | W | Right stick up | Right stick up |
| Jump | Space | South face button | X |
| Switch active player | Tab | North face button | Triangle |
| Attack animation and rumble | Left mouse button | West face button | Square |
| Pause | Escape | Start button | Options / Start |

The left stick's vertical axis is ignored for locomotion. Its horizontal input must reach the configurable **Horizontal Stick Movement Threshold** on GameInput (default: 0.35). The in-game control guide adapts to the most recently used input device. Circle is not currently bound to an action.

Attack is intentionally a demonstration input: it plays the attack animation and requests a short rumble pulse. Combat, hit detection, and damage behavior are not implemented.

## How the locomotion models differ

### Rigidbody2D controller

The green player uses a dynamic Rigidbody2D. Unity's physics simulation drives its velocity and gravity; separate controller, handler, movement, jump, and physics components coordinate locomotion and state changes. This implementation also includes support for external forces and moving platforms.

### Simulated-velocity controller

The orange player calculates and owns its velocity, resolves the intended fixed-step displacement with capsule casts, and then applies the result through Transform.Translate. A kinematic Rigidbody2D and capsule collider provide collision geometry and contact information, but Rigidbody velocity and gravity do not drive the player's movement.

Both players use the shared locomotion state and input/character integration where applicable, while their movement and collision responsibilities remain distinct.

## Repository layout

- **Assets/Portfolio/Scenes** — the comparison scene.
- **Assets/Portfolio/Prefabs/Locomotion** — the configured player prefabs.
- **Assets/Portfolio/Scripts/Runtime/Locomotion** — locomotion controllers and supporting components.
- **Assets/Portfolio/Scripts/Runtime/Input** — shared input and PlayStation controller components.
- **Assets/Portfolio/Scripts/Templates** — canonical runtime and custom-inspector templates.
- **Documentation** — setup, implementation details, controller limitations, code conventions, and historical refactor/review notes.
- **AGENTS.md** — repository-specific development and coding instructions.
- **THIRD-PARTY-NOTICES.md** — third-party attribution and license notice.

## Documentation

- [Locomotion demos and controls](Documentation/Locomotion/LocomotionDemos.md)
- [Simulated-translate locomotion details](Documentation/Locomotion/SimulatedSplineLocomotion.md)
- [PlayStation controller components and limitations](Documentation/PlayStationControllers.md)
- [Code style and canonical templates](Documentation/CodeStyle/README.md)
- [Historical controller-refactor migration report](Documentation/PlayerController-Refactor-Migration.md)
- [Code review round — 27 October 2026](Documentation/CodeReview/ReviewRound-2026-10-27.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)

## Controller and platform limitations

The PlayStation controller components are intended for desktop Unity projects using the Input System. Advanced DualSense features use a low-level USB HID output-report path; availability depends on Unity's device layout, operating system, connection method, and hardware. Bluetooth behavior differs on some hosts.

This project does **not** claim PS5 development-kit integration, console certification, or production-ready console support. Successful API calls indicate that Unity accepted a command, not that a physical device produced a verified effect.

## Validation status

The repository has received static source, documentation, and serialized scene/prefab-reference checks. These checks are not a substitute for opening the project in Unity, compiling it, testing the comparison course in Play Mode, or verifying controller output on physical hardware. Validate those separately on the intended Unity version and devices.
