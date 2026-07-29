# Locomotion Demos

The project contains two focused movement showcases and one shared comparison scene.

## Rigidbody2D

`RigidbodyLocomotionDemo` uses a dynamic `Rigidbody2D`, a capsule collider and the modular movement, jump, slope, steep-slope, external-force and moving-platform code. Unity velocity and gravity drive its movement.

## Simulated translate

`SimulatedSplineLocomotionDemo` uses the same controller/module flow with an internally calculated velocity. Movement is committed through `Transform.Translate`, while a kinematic `Rigidbody2D` and `CapsuleCollider2D` provide contacts, surface normals and predictive collision shape information. Rigidbody velocity and gravity do not drive the player.

The simulated movement module includes explicit ground, slope and air deceleration. Ground snapping is resolved separately from locomotion so a downward support check cannot be projected into downhill drift. Its collision resolver stops grounded motion at steep surfaces without an accumulating push-back.

## Comparison scene

`LocomotionComparisonDemo` contains both implementations on one 80-unit test course. The green player uses Rigidbody locomotion and the orange player uses simulated translate locomotion.

The `SwitchPhysics` action switches the active implementation at the current collider-foot position. The Game view presents controls in a dedicated left panel, a color-coded test prompt in a separate responsive banner and a collapsible physics summary on the right. A world-space label above the active player identifies the current physics model.

The collapsed physics summary shows the active model, surface, jump state and velocity. Expanding it adds the motion method, collision method, surface normal, input and complete locomotion flags.

The course includes:

- A long acceleration and braking runway
- Gentle uphill and downhill slopes with a resting test
- A crest and valley
- An uphill jump-momentum test
- A cyan low-ceiling jump test
- A yellow jumpable step
- Steep uphill and downhill transitions at the far end

Selecting the simulated player draws its capsule, velocity, intended movement, resolved movement, surface normal, steep-slope probe and collision contacts. The comparison switcher draws labeled course-section gizmos.

The orthographic camera follows only the active player's world position. Player rotation and the `0` / `180` Y-axis facing flip do not affect the camera.

## Open the demos

The finished scenes and player prefabs are committed with the project. Open a scene directly in Unity `6000.5.4f1`; no generation step is required.

## Controls

- Move: `A` / `D` or gamepad left stick
- Jump: `Space` or gamepad south button
- Switch physics: `Tab` or gamepad select
- Pause: `Escape` or gamepad start
