# Code Style

The source templates under `Assets/Portfolio/Scripts/Templates` are the authoritative references:

- [Runtime template](../../Assets/Portfolio/Scripts/Templates/Template.cs)
- [Editor template](../../Assets/Portfolio/Scripts/Templates/EditorTemplate.cs)

The files named `Template.cs.txt` and `EditorTemplate.cs.txt` in this folder are pointers, not alternate versions.

## Runtime scripts

- Private fields use a leading `_` followed by lower-camel case, for example `_movementSpeed`.
- Every `const` declaration uses `ALL_CAPS_WITH_UNDERSCORES`, regardless of whether it is public, private, class-scoped, or local. Examples: `MAXIMUM_RECENT_EVENTS`, `REPORT_ID`, and `LABEL_HEIGHT`. Constants do not use the private-field `_` prefix.
- `static readonly` fields are not `const` declarations; name them according to the normal field convention unless the project explicitly defines otherwise.
- Use PascalCase for public types, properties, events, and methods. Use camelCase for parameters and local variables.
- Keep the section separators and `#region` layout consistent with the runtime template where those sections apply.
- Keep `Update` and `FixedUpdate` small. Put component resolution, module setup, and event subscriptions in their own initialization methods where needed.
- Do not add empty lifecycle methods or initialization regions just to copy the template. Small helper/data classes should use only the sections they need.
- Keep comments technical and brief. Comments should explain intent or non-obvious behavior, not narrate the coding process.

## Custom inspectors

- Use the editor template for initialization, cached serialized properties, drawing, cleanup, and helpers when the inspector needs them.
- Cache `SerializedProperty` references for custom serialized fields.
- Pair `serializedObject.Update()` and `ApplyModifiedProperties()` when drawing serialized properties manually.
- Use `Undo` for direct object changes in Edit Mode and disable runtime-only test actions outside Play Mode.
- Simple inspectors may call `DrawDefaultInspector()` and add a small, focused control section without carrying unnecessary template boilerplate.

## Validation

Source inspection and static checks are not a substitute for Unity compilation, Play Mode testing, or testing controller output on hardware. Record those results separately and do not imply tests passed unless they were run.
