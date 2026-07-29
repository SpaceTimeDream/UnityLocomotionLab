# Locomotion Demos

The project contains two focused movement showcases and one shared comparison scene.

## Rigidbody2D

`RigidbodyLocomotionDemo` uses a dynamic `Rigidbody2D`, a capsule collider and the modular movement, jump, slope, steep-slope, external-force and moving-platform code. Unity velocity and gravity drive its movement.

## Simulated translate

`SimulatedSplineLocomotionDemo` uses the same controller/module flow with an internally calculated velocity. Movement is committed through `Transform.Translate`, while a kinematic `Rigidbody2D` and `CapsuleCollider2D` provide contacts, surface normals and predictive collision shape information. Rigidbody velocity and gravity do not drive the player.

The simulated movement module includes explicit ground, slope and air deceleration. Its collision resolver stops grounded motion at steep surfaces without an accumulating push-back.

## Comparison scene

`LocomotionComparisonDemo` contains both implementations on one longer test course. The green player uses Rigidbody locomotion and the orange player uses simulated translate locomotion.

The debug button switches the active implementation at the current collider-foot position. The course includes:

- A long acceleration and braking runway
- Gentle uphill and downhill slopes
- A crest and valley
- Steep uphill and downhill transitions
- A low ceiling
- A jumpable step

The orthographic camera follows only the active player's world position. Player rotation and the `0` / `180` Y-axis facing flip do not affect the camera.

## Open the demos

The finished scenes and player prefabs are committed with the project. Open a scene directly in Unity `6000.5.4f1`; no generation step is required.

## Controls

- Move: `A` / `D` or gamepad left stick
- Jump: `Space` or gamepad south button
- Pause: `Escape` or gamepad start
- Comparison scene: use the top-left debug button to switch implementations
