# Locomotion Demos

This branch contains one comparison scene with two player locomotion implementations.

## Scene

Open `Assets/Portfolio/Scenes/LocomotionComparisonDemo.unity` in Unity `6000.5.4f1`.

The scene contains both implementations on an 80-unit test course. The green player uses Rigidbody locomotion; the orange player uses simulated-translate locomotion. Switch the active player during Play Mode to compare their behavior at the same point in the course.

## Rigidbody2D player

The Rigidbody implementation uses a dynamic `Rigidbody2D`, a capsule collider, and separate controller, handler, movement, jump, and physics components. Unity velocity and gravity drive the player's movement. Its modules cover slopes, steep-slope blocking, external forces, and moving platforms.

## Simulated-translate player

The simulated implementation uses a kinematic `Rigidbody2D` and a capsule collider for contacts and casts, but movement is driven by an internally calculated velocity and committed through `Transform.Translate`. Rigidbody velocity and gravity do not drive the player.

The movement module separates ground and air acceleration/deceleration. Ground snapping and collision resolution are handled separately from movement to prevent the support probe from introducing downhill drift. Steep slopes block movement into the surface without a recurring separation push.

## Test course

The course includes:

- A long acceleration and braking runway.
- Gentle uphill and downhill slopes, a crest, and a valley.
- An uphill jump-momentum test.
- A low-ceiling jump test.
- A jumpable step.
- Steep uphill and downhill transitions.

The simulated-player debug gizmos show its capsule, velocity, intended and resolved movement, surface normal, steep-slope probe, and collision contacts. The comparison switcher marks the course sections and labels the active physics model.

The orthographic camera follows the active player's world position. Player rotation and the Y-axis facing flip do not affect the camera.

## Controls

| Action | Keyboard / Mouse | Generic Gamepad | DualSense / DualShock |
|---|---|---|---|
| Move horizontally | A / D | Left stick left/right or D-pad left/right | Left stick left/right or D-pad left/right |
| Look up while grounded | W | Right stick up | Right stick up |
| Jump | Space | South Button | X |
| Switch active player | Tab | North Button | Triangle |
| Attack animation and rumble | Left mouse button | West Button | Square |
| Pause | Escape | Start Button | Options / Start |

The left stick controls horizontal movement from its X axis only. Vertical left-stick input is ignored, and horizontal input must reach the configurable **Horizontal Stick Movement Threshold** on `GameInput` (default `0.35`). Keyboard and D-pad bindings remain separate from that analog-stick threshold.

The Play Mode control guide follows the most recently used input device: keyboard and mouse, generic Gamepad, DualSense, or DualShock. It uses generic face-button names for unrecognized gamepads and PlayStation labels for recognized DualSense/DualShock devices. Circle is currently not bound to an action in the input-actions asset.

Attack currently triggers the animation and a short rumble pulse only. Damage, hit detection, and combat behavior are not implemented.

## Prefabs and related documentation

- `Assets/Portfolio/Prefabs/Locomotion/RigidbodyPlayerCapsule.prefab`
- `Assets/Portfolio/Prefabs/Locomotion/SimulatedPlayerCapsule.prefab`
- [Simulated locomotion details](SimulatedSplineLocomotion.md)
- [PlayStation controller integration](../PlayStationControllers.md)
