# UnityLocomotionLab

UnityLocomotionLab is a focused Unity 2D locomotion project created to demonstrate modular gameplay programming, collision handling, slope movement, and player-state architecture.

The main demo compares two locomotion implementations over the same test course:

- **Rigidbody Physics** — dynamic `Rigidbody2D` movement using velocity and Unity gravity.
- **Simulated Physics** — transform-driven movement using calculated gravity, kinematic contact data, and capsule sweeps.

**Status:** Portfolio showcase — ongoing development.

This repository will continue to grow with additional locomotion experiments, test scenarios, debugging tools, documentation, and presentation improvements.

## Highlights

- Modular separation between input, controllers, movement, jumping, surface detection, collision resolution, and locomotion state.
- Ground, wall, ceiling, walkable-slope, and steep-slope handling.
- Acceleration, deceleration, slope movement, air control, and momentum preservation.
- Coyote time, jump buffering, variable jump height, early release, and uphill jumping.
- Directional steep-slope blocking while allowing movement and jumping away.
- Moving-platform synchronization and external-force handling.
- Keyboard and gamepad support through Unity's Input System.
- Runtime physics summaries, contextual test prompts, player labels, custom inspector diagnostics, and scene gizmos.
- Camera follow independent of the player’s visual facing rotation.
- Runtime switching between physics models while preserving the player’s position.

## Comparison Demo

Open:

`Assets/Portfolio/Scenes/LocomotionComparisonDemo.unity`

The course includes several focused test scenarios:

1. Long runway for acceleration, deceleration, and stopping distance.
2. Rolling slopes for testing grounded movement and unwanted sliding.
3. Uphill sections for testing slope momentum and jumping.
4. Low ceiling for head collisions and early jump release.
5. Raised step for obstacle-clearing tests.
6. Steep end slopes for directional blocking and escape behavior.

The active implementation can be changed at runtime:

- **Green:** Rigidbody Physics
- **Orange:** Simulated Physics

The comparison scene also displays contextual prompts and expandable runtime information about the active controller.

## Controls

| Action | Keyboard | Gamepad |
|---|---|---|
| Move | A / D | Left Stick |
| Jump | Space | South Button |
| Switch Physics | Tab | Select |
| Pause | Escape | Start |

## Project Structure

| Area | Purpose |
|---|---|
| `Locomotion/Rigidbody` | Rigidbody-based controller and supporting modules |
| `Locomotion/Spline` | Transform-driven simulated controller and collision handling |
| `Locomotion/Core` | Shared locomotion state, camera follow, and demo presentation |
| `Input` | Input actions, device handling, and gameplay input events |
| `Editor/Locomotion` | Custom inspectors and editor diagnostics |
| `Scenes` | Individual demonstrations and the comparison course |

## Getting Started

1. Clone or download the repository.
2. Open the project in Unity Editor `6000.5.4f1`.
3. Allow Unity to resolve the required packages.
4. Open `LocomotionComparisonDemo`.
5. Enter Play Mode.

The project uses Unity's Input System, 2D Physics, and 2D SpriteShape packages.

## Portfolio Purpose

UnityLocomotionLab is a standalone technical portfolio project demonstrating my approach to gameplay programming:

- Modular and maintainable architecture
- Clear separation of responsibilities
- Responsive player movement
- Defensive collision and slope handling
- Editor-facing diagnostics
- Iterative gameplay testing

It is currently presented as a technical showcase rather than a finished Asset Store package or production-ready drop-in controller.

## Planned Development

Future updates may include:

- Additional locomotion experiments and movement models
- More edge-case and stress-test scenarios
- Expanded debugging and visualization tools
- Further controller tuning and code refinement
- Additional documentation and demonstration media

## Author

**Ali AlHubail**  
Unity Gameplay & Game Systems Programmer

[GitHub Profile](https://github.com/SpaceTimeDream)

## License

No open-source license has currently been assigned. This repository is shared for portfolio and reference purposes.
