# Repository Instructions

## C# Script Style
- Private fields begin with `_`.
- Organize scripts with consistent section headers in this order: Fields, Properties, Unity Methods, Public Methods, Private Methods.
- Use `Start()` to call `Initialize()`.
- Cache Unity components through `InitializeComponents()`.
- Cache owned helper modules through `InitializeModules()`.
- Subscribe to events through `InitializeEvents()` where applicable.
- Unsubscribe events in `OnDestroy()` or the matching lifecycle method.
- Keep `Update()` and `FixedUpdate()` small; delegate behavior to focused methods.
- Wrap `OnValidate()` in `#if UNITY_EDITOR` when validation is needed.
- Keep comments short, neutral and technical.
- Do not add narrative comments or labels such as “new”, “old”, “original” or “unchanged”.

## Editor Script Style
- Cache `SerializedProperty` references during editor initialization.
- Use `serializedObject.Update()` before drawing serialized data.
- Use `serializedObject.ApplyModifiedProperties()` after drawing serialized data.
- Use foldouts for grouped inspector sections when the inspector has multiple concerns.
- Clean up editor state when needed.
- Use Undo APIs for direct object changes.
