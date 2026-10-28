# Repository Instructions

## Runtime C# Style

Use `Assets/Portfolio/Scripts/Templates/Template.cs` as the structural reference for MonoBehaviour scripts. Keep the same section separators and `#region` organization where the section applies:

1. Game-state handling and debug helpers, when needed.
2. Parameters, grouped into `Components` and `Flags` regions where useful.
3. Unity lifecycle methods in a `Unity` region.
4. Initialization in an `Initialization` region, with component/module setup and event subscriptions separated.
5. Focused core routines, event handlers, and helpers.
6. Editor-only validation under `#if UNITY_EDITOR`.
7. Nested types at the end when they belong to the class.

Do not add empty lifecycle methods or empty regions just to fill out the template. Small data types and serializable helper modules should use only the sections they need.

- Private fields use a leading `_` followed by lower-camel case, for example `_movementSpeed`.
- All `const` declarations use `ALL_CAPS_WITH_UNDERSCORES`, regardless of access level or scope. Do not prefix constants with `_`, for example `MAXIMUM_RECENT_EVENTS` or `LABEL_HEIGHT`.
- `static readonly` fields are not `const` declarations; keep them consistent with the private-field naming rule unless the project explicitly defines a different convention.
- Public properties, events, and methods use PascalCase. Parameters and local variables use camelCase.
- Keep `Update()` and `FixedUpdate()` small and delegate work to focused methods.
- Use `Start()` to call `Initialize()` when the component needs lifecycle initialization.
- Resolve Unity components in `InitializeComponents()`, owned helper modules in `InitializeModules()`, and event subscriptions in `InitializeEvents()` where those phases apply.
- Detach event subscriptions in the matching lifecycle cleanup method.
- Wrap `OnValidate()` in `#if UNITY_EDITOR`.
- Keep comments short, neutral, and technical. Do not add narration about the editing process or labels such as "new", "old", "original", or "unchanged".

## Editor Script Style

Use `Assets/Portfolio/Scripts/Templates/EditorTemplate.cs` as the structural reference for custom inspectors.

- Group inspector state, serialized properties, initialization/cleanup, drawing, and helpers into clearly labeled sections when the editor has multiple concerns.
- Cache `SerializedProperty` references during editor initialization when drawing custom serialized fields.
- Pair `serializedObject.Update()` and `serializedObject.ApplyModifiedProperties()` when drawing those properties manually.
- Use foldouts for multi-section inspectors and shared styles through `CustomEditorStyles`.
- Use `Undo` APIs for direct changes to objects in Edit Mode.
- Keep runtime-only test actions disabled outside Play Mode. Leave scene-navigation actions available in Edit Mode when safe.
- Simple inspectors that only add a few buttons can use `DrawDefaultInspector()` without adding unused boilerplate.

## Assets and Documentation

- Preserve Unity `.meta` files and existing GUIDs when editing or moving assets.
- Update the relevant documentation when controls, scene contents, public APIs, or component setup changes.
- Treat scripts, scenes, prefabs, and serialized references together when considering deletion. Do not remove a class based only on its name or an apparent lack of C# callers.
- The canonical C# style references are the templates under `Assets/Portfolio/Scripts/Templates`; documentation under `Documentation/CodeStyle` should describe the same conventions.
- Report static checks separately from Unity compilation, Play Mode testing, and hardware testing. Do not claim any of those tests unless they have actually run.
