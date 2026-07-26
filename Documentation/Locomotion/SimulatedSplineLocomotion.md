# Simulated Spline Locomotion

## Purpose

The simulated controller is a parallel implementation that does not use `Rigidbody2D`. It owns its velocity, gravity and jump calculations, then moves the player anchor through `Transform.Translate`.

## SpriteShape sampling

`SpriteShapeSplinePath2D` reads the open SpriteShape spline control points and their left and right tangents. Each neighboring point pair is evaluated as a cubic Bézier segment. The component samples every segment into a cached world-space polyline and cumulative arc-length table. The cache is rebuilt when the controller reference, sampling count, point count, or transform matrix changes.

## Arc-length movement

Grounded motion is stored as one scalar distance along the open path plus a tangential speed. Horizontal input chooses whether acceleration attempts to move toward increasing or decreasing spline distance. The constraint clamps the distance at the endpoints and removes only speed that is trying to continue beyond the open path.

## Slope gravity

Gravity is projected onto the current spline tangent with a dot product. This lets slopes add or remove tangential momentum without forcing the player to a fixed target speed. Walkable slopes accept normal input in either direction. Steep slopes block input that would accelerate uphill while still allowing gravity to slide the player downhill.

## Ground detection

The open SpriteShape spline is also sampled into an `EdgeCollider2D`. `SplineGroundProbe2D` raycasts against that collider to confirm grounded contact and to detect landing while the player descends. Raycasts decide whether contact exists; the nearest sampled spline point remains the authoritative position, tangent and normal.

## Jumping and airborne motion

Jumping detaches the anchor from the spline. The current tangential speed is converted to world-space velocity, then a jump impulse is added along the current surface normal. Airborne movement maintains world-space velocity, applies simulated gravity and air acceleration, clamps fall speed, and supports early jump release by reducing upward velocity.

## Landing

During descent, the ground probe casts along the proposed movement step. A valid hit is mapped to the nearest sampled spline point. On landing, the anchor is constrained to that point and world velocity is projected back onto the spline tangent to preserve momentum.

## Current limitations

- One open ground path is supported.
- No path switching, loops, ceilings, walls, moving platforms, or arbitrary collision handling are included.
- The sampled SpriteShape spline is the movement authority; the `LineRenderer` is visual only.
- The demo reset is a simple height threshold and does not use trigger colliders.

## Demo controls

- Move: existing Gameplay/Move binding.
- Jump: existing Gameplay/Jump binding.
- Pause: existing System/Pause binding.

## Asset generation

Use `Tools/Portfolio/Locomotion Demos/Create Simulated Spline Demo` in the Unity Editor to create or refresh:

- `Assets/Portfolio/Prefabs/Locomotion/SimulatedPlayerCapsule.prefab`
- `Assets/Portfolio/Scenes/SimulatedSplineLocomotionDemo.unity`
