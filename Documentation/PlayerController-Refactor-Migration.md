# Player locomotion refactor and migration

## Architecture

- `PlayerController` is the Unity lifecycle coordinator. It owns the only locomotion `Update` and `FixedUpdate`, reads input, handles public control/reset APIs, and coordinates the modules.
- The fixed-step order is intentionally explicit:
  1. `PlayerPhysics.ApplyPlatformDelta()`
  2. `PlayerPhysics.UpdatePhysics()`
  3. `PlayerMovement.UpdateMovement()`
  4. `PlayerJump.UpdateJump()`
  5. `PlayerPhysics.FinalizePlatformSync()`
- `PlayerPhysics` owns ground probing, platform movement/finalization, gravity, adhesion, external-force finalization, and physics-state refresh.
- `PlayerMovement` owns movement execution and facing/slope direction decisions.
- `PlayerJump` owns jump requests/releases and jump application, while `RigidbodyJump` retains the established buffering, coyote-time, and anticipation implementation.
- `PlayerHandler` owns control-input selection, scripted-control state, freeze/unfreeze behavior, and locomotion transition extension events.
- `PlayerStatus` is a static global status/mode model based on the reference. `LocomotionState` remains the per-controller physics/locomotion model; it is not folded into the global status flags.

## Compatibility and migration

- `RigidbodyLocomotionController` remains only as an obsolete subclass of `PlayerController`. This keeps the original script GUID available for existing scenes/prefabs while new runtime code uses `PlayerController`.
- The custom Inspector now targets `PlayerController`; its existing serialized property names are retained to avoid moving tuning data during the behavior refactor.
- `LocomotionDemoSwitcher` now references `PlayerController`.
- Do not remove the compatibility shim until all scenes and prefabs have been opened, migrated, saved, and checked for missing scripts. If the project opts to remove it later, migrate serialized components first.

## Integration extension points

The source repository does not contain the reference game's animation, VFX, equipment, dialogue, or cutscene systems. This refactor therefore does not introduce those dependencies. Use `PlayerHandler` transition/jump events and `PlayerStatus` status/mode events to connect those systems if they are added later.

## Integration checklist

- [ ] Open the Unity project and allow scripts to recompile.
- [ ] Check the Console for compile errors and missing scripts.
- [ ] Verify existing Rigidbody player prefabs still load through the compatibility shim.
- [ ] Verify the custom Inspector, quick actions, movement tuning, jump tuning, grounding diagnostics, and external-force controls.
- [ ] Test keyboard/gamepad movement, facing, jump buffering, coyote time, jump release, steep-slope restrictions, ground adhesion, and gravity/fall-speed clamping.
- [ ] Test moving platforms, external impulses/velocities, pause/freeze/unfreeze, scripted control, and reset.
- [ ] Switch between the Rigidbody and spline demo players and confirm position/control transfer still works.
- [ ] After validation, consider migrating prefab components from `RigidbodyLocomotionController` to `PlayerController` and removing the shim in a separate cleanup commit.

## Validation status

Source changes were made on `Code-Overhaul`. A Unity Editor compile/play-mode run is still required; repository-level structural checks are not a substitute for Unity validation.
