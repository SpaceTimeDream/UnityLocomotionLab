# Locomotion Demos

The project contains two parallel movement showcases built from the same open SpriteShape terrain.

## Rigidbody2D

`RigidbodyLocomotionDemo` uses a dynamic `Rigidbody2D`, a capsule collider and the existing modular movement, jump, slope, steep-slope, external-force and moving-platform code. The SpriteShape spline is baked to an `EdgeCollider2D` for Unity physics.

## Simulated

`SimulatedSplineLocomotionDemo` contains no `Rigidbody2D` on the player. It calculates velocity and gravity directly, moves through `Transform.Translate`, uses raycasts for grounded and landing checks, and maps contact back to the sampled `SpriteShapeController.spline`.

## Generate the assets

Open the project in Unity `6000.5.4f1`, then run:

`Tools/Portfolio/Locomotion Demos/Create Both Demos`

The builder creates or refreshes:

- `Assets/Portfolio/Prefabs/Locomotion/RigidbodyPlayerCapsule.prefab`
- `Assets/Portfolio/Prefabs/Locomotion/SimulatedPlayerCapsule.prefab`
- `Assets/Portfolio/Scenes/RigidbodyLocomotionDemo.unity`
- `Assets/Portfolio/Scenes/SimulatedSplineLocomotionDemo.unity`

## Controls

- Move: `A` / `D` or gamepad left stick
- Jump: `Space` or gamepad south button
- Pause: `Escape` or gamepad start
