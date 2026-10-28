# Simulated Translate Locomotion

## Purpose

The simulated controller follows the same controller, movement, jump, surface-probe and state flow as the Rigidbody locomotion implementation. It owns a calculated velocity and applies movement with `Transform.Translate`.

The player includes a kinematic `Rigidbody2D` and `CapsuleCollider2D`. They provide collider contacts, surface information and the authoritative capsule shape for predictive casts. Rigidbody velocity and Rigidbody gravity do not drive movement.

## Movement

Ground and air movement use separate acceleration and deceleration values. Releasing input moves horizontal or tangential speed toward zero, so a collision response cannot leave persistent horizontal drift.

Walkable slopes use the detected contact normal to calculate a tangent. Steep slopes are treated as blocking surfaces while the player is grounded. Motion into the steep surface is removed without applying a recurring separation push, and movement away remains available immediately.

## Gravity and jumping

Gravity is calculated by the controller and added to its internal velocity. Jump buffering, coyote time, anticipation and early release follow the same timing flow as the Rigidbody controller.

## Collision resolution

Before translation, `PlayerSimulatedCollisionResolver` casts the full capsule through the complete intended fixed-step displacement. It stops at the earliest collision skin, removes only velocity directed into the contact normal and preserves valid tangential motion.

Grounded movement stops at non-walkable slopes instead of projecting movement up them. Airborne motion can continue along walls or walkable landing surfaces. The resolver does not apply the old accumulated push-back behavior.

## Surface detection

`PlayerSimulatedGroundProbe` checks current Rigidbody contacts first and uses a short capsule cast as the fallback ground probe. Walkable and steep surfaces use the same angle thresholds as the rest of the locomotion family.

The SpriteShape still authors the demo terrain and its collider. Its sampled spline is no longer the authority for player position or grounded distance.

## Demo controls

The simulated player uses the shared `GameInput` bindings:

- Move horizontally: `A` / `D`, left stick left/right, or D-pad left/right.
- Look up while grounded: `W` or right stick up.
- Jump: `Space` or gamepad south face button.
- Switch active player: `Tab` or gamepad north face button (Triangle on PlayStation).
- Attack animation and rumble: left mouse button or gamepad west face button (Square on PlayStation).
- Pause: `Escape` or gamepad Start.

The left stick's vertical axis is ignored for locomotion. Its horizontal axis uses the configurable `GameInput` **Horizontal Stick Movement Threshold** (default `0.35`). Attack is visual and haptic feedback only; combat behavior is not implemented.

## Demo assets

The finished simulated player prefab and demo scene are committed under `Assets/Portfolio`. Open the scene directly; no editor generation step is required.
