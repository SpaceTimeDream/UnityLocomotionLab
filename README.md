# Unity Locomotion Lab

A Unity 2D character-controller showcase comparing Rigidbody-driven locomotion with a simulated-translate implementation. The project also includes shared input handling, character animation integration, camera following, and PlayStation controller feedback components.

## Project setup

- **Unity:** `6000.5.4f1`
- **Main scene:** `Assets/Portfolio/Scenes/LocomotionComparisonDemo.unity`
- **Player prefabs:**
  - `Assets/Portfolio/Prefabs/Locomotion/RigidbodyPlayerCapsule.prefab`
  - `Assets/Portfolio/Prefabs/Locomotion/SimulatedPlayerCapsule.prefab`

Open the comparison scene and enter Play Mode to compare the two locomotion approaches on the same traversal course. The scene includes acceleration/braking space, slopes, a low ceiling, a jumpable step, and steep-slope transitions.

## Locomotion implementations

**Rigidbody player** uses a dynamic `Rigidbody2D` with separate controller, handler, movement, jump, and physics components. Its movement is driven by Unity's Rigidbody velocity and gravity.

**Simulated player** calculates and owns its velocity and resolves its intended displacement before translating the transform. A kinematic `Rigidbody2D` and capsule collider provide contacts and collision-query geometry; Rigidbody velocity and gravity do not drive the movement.

Both implementations use the shared `LocomotionState` model for grounded, movement, and jump-state information. The handlers coordinate state transitions and animation rather than placing all behavior in the controller classes.

## Controls

| Action | Keyboard / Mouse | Gamepad |
|---|---|---|
| Move horizontally | A / D | Left stick left/right or D-pad left/right |
| Look up while grounded | W | Right stick up |
| Jump | Space | South face button |
| Switch active player | Tab | North face button (Triangle on PlayStation) |
| Attack animation and rumble | Left mouse button | West face button (Square on PlayStation) |
| Pause | Escape | Start |

The left stick's vertical axis is ignored for horizontal locomotion. Its horizontal axis must reach the configurable **Horizontal Stick Movement Threshold** on `GameInput` (default `0.35`). LookUp is independent from movement and uses the separate `Look` action. Attack currently plays an animation and requests a short rumble pulse; hit detection and combat are not implemented.

## Input and controller feedback

`GameInput` owns action-map binding, player-action gating, look-up input, attack requests, pause requests, and attack rumble. `DualSenseController` and `DualShockController` expose device selection, common input data, rumble, and supported device-specific output methods. Their custom inspectors contain hardware test controls.

The low-level DualSense output-report implementation targets the USB HID format. Advanced feedback support depends on Unity's device layout, operating system, connection method, and hardware. The project does not claim PS5 development-kit integration or console certification readiness. See [PlayStation Controller Components](Documentation/PlayStationControllers.md) for setup and transport limitations.

## Documentation

- [Locomotion demos and controls](Documentation/Locomotion/LocomotionDemos.md)
- [Simulated-translate locomotion](Documentation/Locomotion/SimulatedSplineLocomotion.md)
- [PlayStation controller components](Documentation/PlayStationControllers.md)
- [Code style and canonical templates](Documentation/CodeStyle/README.md)
- [Controller refactor migration record](Documentation/PlayerController-Refactor-Migration.md)
- [Review round — 26 October 2026](Documentation/CodeReview/ReviewRound-2026-10-27.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)

## Validation scope

The repository review includes static source inspection, serialized scene/prefab reference checks, and documentation consistency checks. Those checks do not substitute for opening the project in Unity, compiling, running the traversal course in Play Mode, or testing controller output on physical hardware. Record those validation results separately.
