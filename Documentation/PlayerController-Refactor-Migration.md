# Locomotion Refactor — Rectification and Migration Report

> Historical migration record. It describes the refactor work and validation available at the time it was written; it is not the authoritative list of current scenes or controls. For the current scene inventory and bindings, see [Locomotion Demos](Locomotion/LocomotionDemos.md) and [PlayStation Controller Components](PlayStationControllers.md).

## Scope and validation status

This rectifies the existing Code-Overhaul split, rather than proposing a fresh architecture. The repository's Rigidbody locomotion implementation is separated into PlayerRigidbodyController, PlayerRigidbodyHandler, PlayerRigidbodyPhysics, PlayerRigidbodyMovement, PlayerRigidbodyJump, and the static PlayerRigidbodyStatus model.

Repository source and serialized YAML have been inspected and structurally checked. A Unity Editor compile/play-mode run has **not** been performed, so this report does not claim Unity runtime validation.

## Rectification 1 — PlayerRigidbodyHandler owns status-driven animation

### What was wrong

The handler did not have a complete public animation API for every transition it owned, and CharacterAnimationController2D could still poll Rigidbody locomotion state independently. That created competing ownership of locomotion flags. Direction changes were also only reflected as facing changes, without requesting a turn animation through the character animation component.

### What changed

- PlayerRigidbodyHandler is the one transition bridge between PlayerRigidbodyStatus, PlayerRigidbodyPhysics, controller-facing events, and the character animation API.
- It uses CharacterAnimationController2D for idle and run/walk flags, keeps the jump animation active through jumping and falling, and drives floating-up, slide, turn, death, facing, pause/freeze, cutscene mode, and reset-to-idle transitions.
- The Rigidbody-backed CharacterAnimationController2D disables automatic locomotion polling when it resolves a PlayerRigidbodyController. The spline controller's animation path remains separate.
- PlayerRigidbodyHandlerEditor quick actions provide Find Player (selection, ping, and Scene View focus) and a play-mode Reset Player action. Reset Runtime and Zero Velocity are removed from the quick-action UI.
- PlayerRigidbodyHandler contains no PlayerAnimation reference, no Animator field, and makes no direct Animator calls.
- The reset API lives on CharacterAnimationController2D. Its internal Animator.Rebind()/Animator.Update(0f) are encapsulated there; callers use ResetToIdle() instead.

### CharacterAnimationController2D API additions

| Added public API | Purpose |
|---|---|
| SetLocomotionAnimationDriverEnabled(bool) | Turns off automatic locomotion polling for Rigidbody players. |
| SetFalling(bool) | Applies falling state through the animation parameter API. |
| SetFloatingUp(bool) | Applies floating-up state. |
| SetSliding(bool) | Applies sliding state. |
| Turn() | Requests the turn trigger through the animation component. |
| SetAnimationFrozen(bool) | Freezes/restores normal and optional cutscene Animator playback. |
| SetCutsceneMode(bool) | Owns cutscene Animator enable/disable in the animation component. |
| ResetToIdle() | Encapsulates Animator rebind/update and the idle reset pipeline. |

SetRunning(bool), SetJumping(bool), Idle(), Die(), and SetFacingDirection(float) already existed and are used by the handler.

## Rectification 2 — parameters live on their owning modules

### What was wrong

PlayerRigidbodyController still held serialized tuning, plus nested RigidbodyMovement and RigidbodyJump data. The movement and jump modules were not the inspector owners of their own parameters.

### What changed

- PlayerRigidbodyController now serializes only its Rigidbody/collider/visual/input references and module references. Its runtime input and scripted-control fields are state, not tuning.
- PlayerRigidbodyMovement serializes movement, slope movement, air movement, floating movement, facing, and speed-clamp parameters. It exposes module-owned accessors and a runtime Validate() API.
- PlayerRigidbodyJump serializes jump force, maximum force, buffer duration, coyote time, anticipation, grounded-check delay, and early-release settings. It owns jump input subscriptions and runtime validation.
- PlayerRigidbodyPhysics serializes grounding, gravity, fall clamping, slope adherence, and adhesion settings, alongside the embedded grounded/slope probe, external-force, and moving-platform settings. Ground-debugging tools, adhesion logging, and friction-material overrides have been removed. It exposes forwarding properties on the physics module for the embedded helper settings.
- PlayerRigidbodyControllerEditor groups Controller References, PlayerRigidbodyHandler, PlayerRigidbodyJump, PlayerRigidbodyMovement, and PlayerRigidbodyPhysics into independent foldouts. Each module section exposes its component reference and serialized settings. Editing the controller inspector modifies each referenced module's SerializedObject rather than copying parameters onto PlayerRigidbodyController. PlayerRigidbodyMovement shows movementSpeed as the inspector label and PlayerRigidbodyJump shows jump.

### Parameter migration table

| Old owner | Old field / setting | New owner | Inspector field |
|---|---|---|---|
| RigidbodyMovement | _maximumGroundSpeed | PlayerRigidbodyMovement | _movementSpeed (label: movementSpeed) |
| RigidbodyMovement | _moveSpeedClampModifier | PlayerRigidbodyMovement | _moveSpeedClampModifier |
| RigidbodyMovement | _groundAcceleration | PlayerRigidbodyMovement | _moveAcceleration |
| RigidbodyMovement | _groundDeceleration | PlayerRigidbodyMovement | _moveDeceleration |
| RigidbodyMovement | _slopeAcceleration | PlayerRigidbodyMovement | _slopeMoveAcceleration |
| RigidbodyMovement | _slopeDeceleration | PlayerRigidbodyMovement | _slopeMoveDeceleration |
| RigidbodyMovement | _airAcceleration | PlayerRigidbodyMovement | _airMoveAcceleration |
| RigidbodyMovement | _airDeceleration | PlayerRigidbodyMovement | _airMoveDeceleration |
| RigidbodyMovement | _steepSlopeSlideSpeed | PlayerRigidbodyMovement | _steepSlopeSlideSpeed |
| RigidbodyMovement | _slopeAdherenceSpeed | PlayerRigidbodyMovement | _slopeAdherenceSpeed |
| RigidbodyMovement | _minimumInput | PlayerRigidbodyMovement | _minimumInput |
| PlayerRigidbodyMovement behavior carried forward | Floating acceleration/deceleration, speed clamp, external velocity | PlayerRigidbodyMovement | _floatingMoveAcceleration, _floatingMoveDeceleration, _floatingMoveSpeedClamp, _floatingSpeedClampModifier, _floatingExternalVelocity |
| PlayerRigidbodyController | _flipVisualWithFacing | PlayerRigidbodyMovement | _flipVisualWithFacing |
| RigidbodyJump | _jumpVelocity | PlayerRigidbodyJump | _jump (label: jump) |
| RigidbodyJump | _maximumJumpVelocity | PlayerRigidbodyJump | _maxJumpForce (label: maxJumpForce) |
| RigidbodyJump | _jumpBufferTime | PlayerRigidbodyJump | _jumpBufferDuration |
| RigidbodyJump | _coyoteTime | PlayerRigidbodyJump | _coyoteTimeThreshold |
| RigidbodyJump | _jumpAnticipationDuration | PlayerRigidbodyJump | _jumpAnticipationDuration |
| RigidbodyJump | _groundCheckDelayFixedFrames | PlayerRigidbodyJump | _groundedCheckDelay |
| RigidbodyJump | _enableEarlyRelease | PlayerRigidbodyJump | _enableEarlyRelease |
| RigidbodyJump | _earlyReleaseVelocityMultiplier | PlayerRigidbodyJump | _earlyReleaseVelocityMultiplier |
| RigidbodyJump runtime clamp | Jump-force clamp modifier | PlayerRigidbodyJump | _jumpForceClampModifier |
| PlayerRigidbodyController | _gravityScale | PlayerRigidbodyPhysics | _gravityScale (label: gravityScale) |
| PlayerRigidbodyController | Saved reset gravity baseline | PlayerRigidbodyPhysics | Default_GravityScale |
| PlayerRigidbodyController | _baseGravity (new explicit configuration) | PlayerRigidbodyPhysics | _baseGravity |
| PlayerRigidbodyController | _jumpAscentGravityMultiplier | PlayerRigidbodyPhysics | _jumpAscentGravityMultiplier |
| PlayerRigidbodyController | _fallGravityMultiplier | PlayerRigidbodyPhysics | _fallGravityMultiplier |
| PlayerRigidbodyController | _maximumFallSpeed | PlayerRigidbodyPhysics | _fallClamp (label: fallClamp) |
| PlayerRigidbodyController | _steepSlopeJumpCheckDistance | PlayerRigidbodyPhysics | _steepSlopeJumpCheckDistance |
| PlayerRigidbodyController | _groundAdhesionForce | PlayerRigidbodyPhysics | _groundAdhesionForce |
| PlayerRigidbodyController | _state nested model | PlayerRigidbodyPhysics | _state |
| PlayerRigidbodyController | _surfaceProbe nested probe | PlayerRigidbodyPhysics | _surfaceProbe |
| PlayerRigidbodyController | _externalForces nested config | PlayerRigidbodyPhysics | _externalForces |
| PlayerRigidbodyController | _movingPlatforms nested config | PlayerRigidbodyPhysics | _movingPlatforms |

The nested PlayerRigidbodySurfaceProbe settings (ground layers, probe distance/inset/origin, rising threshold, grace duration, and slope-angle thresholds), PlayerRigidbodyExternalForceReceiver settings (horizontal protection time, resistance, settled threshold), and moving-platform enabled state are visible beneath the PlayerRigidbodyPhysics component. PlayerRigidbodyPhysics exposes module-level get/set properties for these settings.

## Rectification 3 — remove RigidbodyMovement and the prior controller monolith

### Disposition

- RigidbodyMovement.cs and its .meta have been deleted.
- RigidbodyJump.cs and its .meta have been deleted; jump behavior belongs to PlayerRigidbodyJump.
- RigidbodyLocomotionController.cs and its .meta have been deleted after converting the checked-in player prefab/scenes to PlayerRigidbodyController.
- The old-named custom editor and its .meta were removed. PlayerRigidbodyControllerEditor is the foldout-based inspector for the controller and each referenced player module.
- No obsolete shim remains and no runtime C# file should refer to either removed monolith.

### RigidbodyMovement member-by-member destination

| Old member | Destination |
|---|---|
| _maximumGroundSpeed | PlayerRigidbodyMovement._movementSpeed |
| _moveSpeedClampModifier | PlayerRigidbodyMovement._moveSpeedClampModifier |
| _groundAcceleration, _groundDeceleration | PlayerRigidbodyMovement._moveAcceleration, _moveDeceleration |
| _slopeAcceleration, _slopeDeceleration | PlayerRigidbodyMovement._slopeMoveAcceleration, _slopeMoveDeceleration |
| _airAcceleration, _airDeceleration | PlayerRigidbodyMovement._airMoveAcceleration, _airMoveDeceleration |
| _steepSlopeSlideSpeed, _slopeAdherenceSpeed, _minimumInput | Same-named PlayerRigidbodyMovement fields |
| MaximumGroundSpeed | PlayerRigidbodyMovement.movementSpeed |
| MoveSpeedClampModifier | PlayerRigidbodyMovement.moveSpeedClampModifier |
| GroundAcceleration, GroundDeceleration | PlayerRigidbodyMovement.moveAcceleration, moveDeceleration |
| SlopeAcceleration, SlopeDeceleration | PlayerRigidbodyMovement.slopeMoveAcceleration, slopeMoveDeceleration |
| AirAcceleration, AirDeceleration | PlayerRigidbodyMovement.airMoveAcceleration, airMoveDeceleration |
| ApplyMovement(...) | PlayerRigidbodyMovement.UpdateMovement() → PlayerRigidbodyMovement.ApplyMovement(...) |
| SetMoveSpeedClamp(...), AddMoveSpeedClamp(...) | Same-named PlayerRigidbodyMovement methods |
| ResetRuntimeState() | PlayerRigidbodyMovement.ResetRuntimeState() |
| Validate() | PlayerRigidbodyMovement.Validate(), called during module initialization and editor validation |
| ApplyHorizontalMovement(...), ApplySlopeMovement(...), ApplySteepSlopeMovement(...), ApplyDeceleration(...) | Private movement routines inside PlayerRigidbodyMovement |
| GetSlopeTangent(...), ClampHorizontalSpeed(...), RemoveInputIntoSlope(...), RemoveVelocityIntoSlope(...) | Private movement helpers inside PlayerRigidbodyMovement |

The deleted RigidbodyJump members were moved in the same spirit: request/release, timers, anticipation, early release, jump clamp, ground-check delay, slope restriction, and jump execution are now in PlayerRigidbodyJump. PlayerRigidbodyPhysics calls PlayerRigidbodyJump.UpdateJumpState(...) during UpdatePhysics() so the external fixed-step call order remains unchanged. Only PlayerRigidbodyJump subscribes to jump input events.

## Saved scene/prefab migration (historical)

At the time of the refactor, the old Rigidbody controller serialization was migrated in the assets listed below. The standalone `RigidbodyLocomotionDemo.unity` scene mentioned in the original migration notes is not present on the current `Code-Overhaul` branch; the maintained demo is `Assets/Portfolio/Scenes/LocomotionComparisonDemo.unity`.

| Asset | Historical migration |
|---|---|
| `Assets/Portfolio/Prefabs/Locomotion/RigidbodyPlayerCapsule.prefab` | Replaced the old controller script GUID; added handler, jump, movement, and physics components; transferred tuning to the owning modules. |
| `Assets/Portfolio/Scenes/LocomotionComparisonDemo.unity` | Migrated serialized controller references and component wiring. |

These entries record the migration performed then. They should not be read as a claim that the old standalone scene is still present.

## Architectural invariants and verification checklist

- [x] PlayerRigidbodyController is the only class among the six Rigidbody modules with Update() and FixedUpdate().
- [x] FixedUpdate() call order is ApplyPlatformDelta → UpdatePhysics → UpdateMovement → UpdateJump → FinalizePlatformSync.
- [x] PlayerRigidbodyStatus is a plain static class; the per-player LocomotionState remains embedded in PlayerRigidbodyPhysics.
- [x] Init runs Start → Initialize → InitializeModules → InitializeComponents → InitializeEvents with an _initialized guard; peer modules are resolved with GetComponent/GetComponentInChildren, not FindObjectOfType.
- [x] Movement input is read by PlayerRigidbodyController; PlayerRigidbodyJump alone subscribes to GameInput.jumpEvent and jumpCanceledEvent.
- [x] Other modules do not call PlayerRigidbodyStatus.SetStatus/AddMode/RemoveMode directly. PlayerRigidbodyHandler is the status/mode gateway.
- [x] Physics state goes PlayerRigidbodyPhysics.OnPhysicsStateChanged → PlayerRigidbodyHandler → PlayerRigidbodyStatus.HandlePhysicsState.
- [x] PlayerRigidbodyJump serialized field is labeled jump in its inspector; PlayerRigidbodyMovement serialized field is labeled movementSpeed.
- [x] PlayerRigidbodyPhysics owns grounding, gravity/slope/force tuning; its nested physics helper settings are serialized beneath it and module-level setters/getters are available. Ground-debugging and adhesion-log code has been removed, and PlayerRigidbodyPhysics no longer switches friction materials.
- [x] PlayerRigidbodyControllerEditor provides foldouts for controller references and each player module, and edits each module through its SerializedObject.
- [x] PlayerRigidbodyHandler contains no PlayerAnimation or Animator field/call and routes transitions through CharacterAnimationController2D.
- [x] Runtime source scan found no RigidbodyMovement, RigidbodyJump, or RigidbodyLocomotionController references. Their source and .meta files are deleted; no shim remains.
- [x] Serialized prefab/scene scan found no old controller GUIDs and confirmed new module links on the three migrated assets.
- [x] PlayerRigidbodyPhysics.UpdateGrounded uses a reference-style downward CircleCast, ignores grounding in refracted/no-clip/gravity-bypass/jump-delay states, checks bottom contact during upward motion, supports short last-platform grounding persistence, updates ground-contact/grounded-time state, and raises OnGroundAction.
- [x] Movement into a detected steep slope removes all horizontal velocity in the blocked direction and suppresses walking animation input while the player is pressing into the blocker. When a steep CircleCast hit occurs at the slope base, PlayerRigidbodyPhysics prefers an actual walkable Rigidbody contact when available so grounded movement and jumping can recover.
- [x] The three serialized Rigidbody-player assets include the ground-point, cast-distance, and platform-persistence fields.
- [ ] Open the project in Unity, compile, inspect the controller's editable module overlays, and run play-mode grounding tests. These checks are not available from repository source inspection alone.

## Integration checklist

1. Recommended component attachment order on the root GameObject: PlayerRigidbodyPhysics, PlayerRigidbodyMovement, PlayerRigidbodyJump, PlayerRigidbodyHandler, then PlayerRigidbodyController. The controller also resolves/adds missing modules during InitializeModules(), but explicit attachment makes serialized references and inspector review easier. All four module references on PlayerRigidbodyController should point to this same root object. PlayerRigidbodyPhysics can use an assigned groundPoint; if none is assigned, the circle-cast origin is derived from the capsule bounds. To match the reference LayerManager.GroundLayer behavior without importing the unavailable LayerManager class, the cast uses the named Ground layer when present and falls back to PlayerRigidbodySurfaceProbe.GroundLayers if that layer does not exist.
2. Keep CharacterAnimationController2D on the character/visual hierarchy and assign it to PlayerRigidbodyHandler, or let the handler resolve it below the controller. For Rigidbody-backed players, automatic locomotion polling is disabled so animation transitions have one owner.
3. Ensure GameInput exists and is assigned or available from GameInput.Instance. Re-hookup is owned by the components: PlayerRigidbodyController subscribes to GameInput.MovementValueChanged and GameManager.GameStateChanged; PlayerRigidbodyJump subscribes to GameInput.jumpEvent and jumpCanceledEvent; PlayerRigidbodyHandler subscribes to PlayerRigidbodyPhysics.OnPhysicsStateChanged and PlayerRigidbodyStatus status/mode/busy events. Their OnDisable/OnEnable paths detach and restore subscriptions.
4. Review migrated tuning in each module's inspector. FormerlySerializedAs annotations remain on renamed jump/physics fields as defensive support for same-component field renames. They cannot transfer values across different MonoBehaviour script GUIDs; the checked-in component-to-component handoff was performed explicitly in the YAML migration.
5. Hook project-specific audio/VFX/equipment/dialogue/cutscene systems through the handler's events/status APIs if those systems are brought into this repository. The reference PlayerRigidbodyPhysics.cs.txt also contains PlatformDetection/WallsAndRoofs ejection and physics-material systems that are not present in this repository's current runtime source. Those dependencies were not fabricated during this repository-local rectification; if integrated later, their state and configuration belong in PlayerRigidbodyPhysics.
6. Unity validation still needs to cover jump buffering/coyote/anticipation/release, ground-check delay, slope and steep-slope movement, floating movement, external impulses/velocity, moving platforms, gravity/adhesion, freeze/unfreeze, scripted control, reset-to-idle, turning, cutscene transitions, and the current `LocomotionComparisonDemo` scene.

## Naming and simulated-controller decomposition

The simulated implementation is coordinated by `PlayerSimulatedController`, with focused `PlayerSimulatedMovement`, `PlayerSimulatedJump`, `PlayerSimulatedGroundProbe`, `PlayerSimulatedCollisionResolver`, `PlayerSimulatedPlatformTracker`, `PlayerSimulatedPhysics`, and `PlayerSimulatedVisuals` modules. Gravity, steep-slope blocking, collision displacement resolution, ground snapping, physics gizmo state, and visual facing/alignment no longer live in the controller itself. The controller remains an orchestrator rather than a base class.

The Rigidbody implementation follows the matching `PlayerRigidbody…` naming convention, including its controller, handler, jump, movement, physics, status, ground probe, external-force receiver, and platform tracker. Unity script GUIDs were preserved while component script files moved. `PlayerSimulatedMovement`, `PlayerSimulatedJump`, and `PlayerSimulatedPhysics` are attachable MonoBehaviour components with their own serialized parameters and runtime state; `PlayerSimulatedController` stores component references and coordinates them. The simulated prefab and demo scenes were migrated from nested parameter blocks to component references while retaining their existing tuning values. `PlayerSimulatedVisuals` remains an embedded serializable helper.

### Simulated-player component ownership and handler

- `PlayerSimulatedMovement`, `PlayerSimulatedJump`, and `PlayerSimulatedPhysics` are attachable `MonoBehaviour` components. Each owns its serialized tuning and runtime state; `PlayerSimulatedPhysics` owns the calculated velocity and `LocomotionState`, while `PlayerSimulatedController` references the components and orchestrates the fixed-step sequence.
- `PlayerSimulatedHandler` owns scripted-control and movement-freeze state, status/mode reactions, simulated locomotion animation updates, facing/turn updates, reset handling, and jump-event forwarding, following the same pattern as `PlayerRigidbodyHandler`.
- Simulated-player assets serialize the three modules and handler as separate components, with the controller referencing each component.

### Automatic head-look persistence

Automatic head look still evaluates each frame in `LateUpdate`, separately from locomotion animation state. Newly acquired interest targets must satisfy the facing-angle and radius checks; the currently selected target remains selected while it is active and inside the configured look radius, even if movement or jumping briefly carries it outside the forward-facing angle. The same targeting behavior applies to both locomotion types.
