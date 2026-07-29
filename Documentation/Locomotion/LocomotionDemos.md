# Locomotion Demos

The project contains two parallel movement showcases built from the same open SpriteShape terrain.

## Rigidbody2D

`RigidbodyLocomotionDemo` uses a dynamic `Rigidbody2D`, a capsule collider and the modular movement, jump, slope, steep-slope, external-force and moving-platform code. Unity velocity and gravity drive its movement.

## Simulated translate

`SimulatedSplineLocomotionDemo` uses the same controller/module flow with an internally calculated velocity. Movement is committed through `Transform.Translate`, while a kinematic `Rigidbody2D` and `CapsuleCollider2D` provide contacts, surface normals and predictive collision shape information. Rigidbody velocity and gravity do not drive the player.

The simulated movement module includes explicit ground, slope and air deceleration. Its collision resolver stops grounded motion at steep surfaces without an accumulating push-back.

## Open the demos

The finished scenes and player prefabs are committed with the project. Open either scene directly in Unity `6000.5.4f1`; no generation step is required.

## Controls

- Move: `A` / `D` or gamepad left stick
- Jump: `Space` or gamepad south button
- Pause: `Escape` or gamepad start
