#if UNITY_EDITOR
using Portfolio.Editor.Shared;
using Portfolio.Locomotion.Rigidbody;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerRigidbodyController), true)]
[CanEditMultipleObjects]
public sealed class PlayerRigidbodyControllerEditor : Editor
{
    //------------------------------- Foldout State -------------------------------//

    private bool _showControllerReferences = true;
    private bool _showPlayerHandler = true;
    private bool _showPlayerJump = true;
    private bool _showPlayerMovement = true;
    private bool _showPlayerPhysics = true;

    //------------------------------- SerializedProperties -------------------------------//

    private SerializedProperty _bodyProperty;
    private SerializedProperty _capsuleColliderProperty;
    private SerializedProperty _visualRootProperty;
    private SerializedProperty _gameInputProperty;
    private SerializedProperty _playerHandlerProperty;
    private SerializedProperty _playerJumpProperty;
    private SerializedProperty _playerMovementProperty;
    private SerializedProperty _playerPhysicsProperty;

    //------------------------------- Module Editors -------------------------------//

    private SerializedObject _handlerSerializedObject;
    private SerializedObject _jumpSerializedObject;
    private SerializedObject _movementSerializedObject;
    private SerializedObject _physicsSerializedObject;

    private PlayerRigidbodyController _controller;
    private bool _isInitialized;

    //------------------------------- Unity -------------------------------//

    #region Unity

    private void OnEnable()
    {
        if (!HasValidTargets())
        {
            return;
        }

        _controller = target as PlayerRigidbodyController;
        CacheSerializedProperties();
        RefreshModuleSerializedObjects();
        _isInitialized = true;
    }

    private void OnDisable()
    {
        _handlerSerializedObject = null;
        _jumpSerializedObject = null;
        _movementSerializedObject = null;
        _physicsSerializedObject = null;
        _controller = null;
        _isInitialized = false;
    }

    public override void OnInspectorGUI()
    {
        if (!_isInitialized || !HasValidTargets())
        {
            return;
        }

        serializedObject.Update();
        DrawScriptReference();
        DrawControllerReferences();

        DrawPlayerModule("Player Handler", ref _showPlayerHandler, _playerHandlerProperty, ModuleKind.Handler);
        DrawPlayerModule("Player Jump", ref _showPlayerJump, _playerJumpProperty, ModuleKind.Jump);
        DrawPlayerModule("Player Movement", ref _showPlayerMovement, _playerMovementProperty, ModuleKind.Movement);
        DrawPlayerModule("Player Physics", ref _showPlayerPhysics, _playerPhysicsProperty, ModuleKind.Physics);

        serializedObject.ApplyModifiedProperties();
    }

    #endregion

    //------------------------------- Cache -------------------------------//

    #region Cache

    private void CacheSerializedProperties()
    {
        _bodyProperty = serializedObject.FindProperty("_body");
        _capsuleColliderProperty = serializedObject.FindProperty("_capsuleCollider");
        _visualRootProperty = serializedObject.FindProperty("_visualRoot");
        _gameInputProperty = serializedObject.FindProperty("_gameInput");
        _playerHandlerProperty = serializedObject.FindProperty("_playerHandler");
        _playerJumpProperty = serializedObject.FindProperty("_playerJump");
        _playerMovementProperty = serializedObject.FindProperty("_playerMovement");
        _playerPhysicsProperty = serializedObject.FindProperty("_playerPhysics");
    }

    private void RefreshModuleSerializedObjects()
    {
        if (_controller == null)
        {
            _handlerSerializedObject = null;
            _jumpSerializedObject = null;
            _movementSerializedObject = null;
            _physicsSerializedObject = null;
            return;
        }

        _handlerSerializedObject = _controller.PlayerRigidbodyHandler != null
            ? new SerializedObject(_controller.PlayerRigidbodyHandler)
            : null;
        _jumpSerializedObject = _controller.PlayerRigidbodyJump != null
            ? new SerializedObject(_controller.PlayerRigidbodyJump)
            : null;
        _movementSerializedObject = _controller.PlayerRigidbodyMovement != null
            ? new SerializedObject(_controller.PlayerRigidbodyMovement)
            : null;
        _physicsSerializedObject = _controller.PlayerRigidbodyPhysics != null
            ? new SerializedObject(_controller.PlayerRigidbodyPhysics)
            : null;
    }

    #endregion

    //------------------------------- Draw -------------------------------//

    #region Draw

    private void DrawScriptReference()
    {
        MonoScript script = MonoScript.FromMonoBehaviour(_controller);
        EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
        EditorGUILayout.Space(3f);
    }

    private void DrawControllerReferences()
    {
        if (!BeginFoldoutSection("Controller References", ref _showControllerReferences))
        {
            return;
        }

        serializedObject.Update();
        DrawProperty(_bodyProperty, "Rigidbody 2D");
        DrawProperty(_capsuleColliderProperty, "Capsule Collider");
        DrawProperty(_visualRootProperty, "Visual Root");
        DrawProperty(_gameInputProperty, "Game Input");
        serializedObject.ApplyModifiedProperties();
        EndFoldoutSection();
    }

    private void DrawPlayerModule(
        string title,
        ref bool expanded,
        SerializedProperty componentProperty,
        ModuleKind moduleKind)
    {
        if (!BeginFoldoutSection(title, ref expanded))
        {
            return;
        }

        serializedObject.Update();
        DrawProperty(componentProperty, "Component");
        serializedObject.ApplyModifiedProperties();
        RefreshModuleSerializedObjects();

        DrawModuleParameters(GetModuleSerializedObject(moduleKind));
        EndFoldoutSection();
    }

    private SerializedObject GetModuleSerializedObject(ModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case ModuleKind.Handler:
                return _handlerSerializedObject;
            case ModuleKind.Jump:
                return _jumpSerializedObject;
            case ModuleKind.Movement:
                return _movementSerializedObject;
            case ModuleKind.Physics:
                return _physicsSerializedObject;
            default:
                return null;
        }
    }

    private static bool BeginFoldoutSection(string title, ref bool expanded)
    {
        EditorGUILayout.BeginVertical(CustomEditorStyles.HelpBoxStyle);
        expanded = EditorGUILayout.Foldout(expanded, title, true, CustomEditorStyles.FoldoutStyle);

        if (expanded)
        {
            EditorGUILayout.Space(3f);
            return true;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3f);
        return false;
    }

    private static void EndFoldoutSection()
    {
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3f);
    }

    private static void DrawModuleParameters(SerializedObject moduleObject)
    {
        if (moduleObject == null || moduleObject.targetObject == null)
        {
            EditorGUILayout.HelpBox("This component is not assigned yet.", MessageType.Info);
            return;
        }

        moduleObject.Update();

        SerializedProperty property = moduleObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (property.propertyPath == "m_Script")
            {
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        moduleObject.ApplyModifiedProperties();
    }

    private static void DrawProperty(SerializedProperty property, string label)
    {
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label));
        }
    }

    #endregion

    //------------------------------- Helpers -------------------------------//

    private bool HasValidTargets()
    {
        if (target == null || targets == null || targets.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    private enum ModuleKind
    {
        Handler,
        Jump,
        Movement,
        Physics
    }
}
#endif
