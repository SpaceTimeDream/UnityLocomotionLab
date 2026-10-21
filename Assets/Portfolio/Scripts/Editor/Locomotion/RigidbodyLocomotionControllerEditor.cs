#if UNITY_EDITOR
using System.Collections.Generic;
using Portfolio.Editor.Shared;
using Portfolio.Locomotion.Rigidbody;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RigidbodyLocomotionController), true)]
[CanEditMultipleObjects]
public class RigidbodyLocomotionControllerEditor : Editor
{
    //------------------------------- Constants -------------------------------//

    private const string QuickActionsFoldoutKey = "Quick Actions";
    private const string RuntimeStatusFoldoutKey = "Runtime Status";
    private const string ComponentsFoldoutKey = "Components";
    private const string MovementFoldoutKey = "Movement";
    private const string JumpFoldoutKey = "Jump";
    private const string PhysicsFoldoutKey = "Physics";
    private const string ExternalForcesFoldoutKey = "External Forces & Platforms";
    private const string DebuggingFoldoutKey = "Debugging";
    private const float MonitorInterval = 0.1f;
    private const int MaximumVelocityHistory = 100;

    //------------------------------- Flags & SerializedProperties -------------------------------//

    private SerializedProperty _bodyProp;
    private SerializedProperty _capsuleColliderProp;
    private SerializedProperty _visualRootProp;
    private SerializedProperty _gameInputProp;
    private SerializedProperty _stateProp;
    private SerializedProperty _controlEnabledProp;
    private SerializedProperty _movementProp;
    private SerializedProperty _jumpProp;
    private SerializedProperty _surfaceProbeProp;
    private SerializedProperty _externalForcesProp;
    private SerializedProperty _movingPlatformsProp;
    private SerializedProperty _gravityScaleProp;
    private SerializedProperty _jumpAscentGravityMultiplierProp;
    private SerializedProperty _fallGravityMultiplierProp;
    private SerializedProperty _maximumFallSpeedProp;
    private SerializedProperty _steepSlopeJumpCheckDistanceProp;
    private SerializedProperty _groundAdhesionForceProp;
    private SerializedProperty _exportGroundAdhesionLogProp;
    private SerializedProperty _groundAdhesionLogIntervalProp;
    private SerializedProperty _groundAdhesionLogPathProp;
    private SerializedProperty _groundAdhesionLogErrorProp;
    private SerializedProperty _flipVisualWithFacingProp;

    //------------------------------- Internal Editors -------------------------------//

    //------------------------------- State -------------------------------//

    private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();
    private readonly List<Vector2> _velocityHistory = new List<Vector2>(MaximumVelocityHistory);

    private RigidbodyLocomotionController _controller;
    private Vector2 _lastVelocity;
    private Vector3 _lastPosition;
    private double _lastMonitorTime;
    private bool _isInitialized;

    //------------------------------- Unity -------------------------------//

    #region Unity

    private void OnEnable()
    {
        if (!HasValidTargets())
        {
            return;
        }

        CacheSerializedProperties();
        InitializeEditors();
        InitializeFoldouts();
        SubscribeEvents();
        _controller = target as RigidbodyLocomotionController;
        _isInitialized = true;
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        CleanupEditors();
        _velocityHistory.Clear();
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
        DrawChecks();
        DrawComponents();
        DrawMovement();
        DrawJump();
        DrawPhysics();
        DrawExternalForces();
        DrawDebugging();

        serializedObject.ApplyModifiedProperties();
    }

    #endregion

    //------------------------------- Cache -------------------------------//

    private void CacheSerializedProperties()
    {
        _bodyProp = serializedObject.FindProperty("_body");
        _capsuleColliderProp = serializedObject.FindProperty("_capsuleCollider");
        _visualRootProp = serializedObject.FindProperty("_visualRoot");
        _gameInputProp = serializedObject.FindProperty("_gameInput");
        _stateProp = serializedObject.FindProperty("_state");
        _controlEnabledProp = _stateProp?.FindPropertyRelative("_controlEnabled");
        _movementProp = serializedObject.FindProperty("_movement");
        _jumpProp = serializedObject.FindProperty("_jump");
        _surfaceProbeProp = serializedObject.FindProperty("_surfaceProbe");
        _externalForcesProp = serializedObject.FindProperty("_externalForces");
        _movingPlatformsProp = serializedObject.FindProperty("_movingPlatforms");
        _gravityScaleProp = serializedObject.FindProperty("_gravityScale");
        _jumpAscentGravityMultiplierProp = serializedObject.FindProperty("_jumpAscentGravityMultiplier");
        _fallGravityMultiplierProp = serializedObject.FindProperty("_fallGravityMultiplier");
        _maximumFallSpeedProp = serializedObject.FindProperty("_maximumFallSpeed");
        _steepSlopeJumpCheckDistanceProp = serializedObject.FindProperty("_steepSlopeJumpCheckDistance");
        _groundAdhesionForceProp = serializedObject.FindProperty("_groundAdhesionForce");
        _exportGroundAdhesionLogProp = serializedObject.FindProperty("_exportGroundAdhesionLog");
        _groundAdhesionLogIntervalProp = serializedObject.FindProperty("_groundAdhesionLogInterval");
        _groundAdhesionLogPathProp = serializedObject.FindProperty("_groundAdhesionLogPath");
        _groundAdhesionLogErrorProp = serializedObject.FindProperty("_groundAdhesionLogError");
        _flipVisualWithFacingProp = serializedObject.FindProperty("_flipVisualWithFacing");
    }

    //------------------------------- Editors -------------------------------//

    private void InitializeEditors()
    {
    }

    private void CleanupEditors()
    {
    }

    //------------------------------- Foldouts -------------------------------//

    private void InitializeFoldouts()
    {
        EnsureFoldout(QuickActionsFoldoutKey, true);
        EnsureFoldout(RuntimeStatusFoldoutKey, true);
        EnsureFoldout(ComponentsFoldoutKey, true);
        EnsureFoldout(MovementFoldoutKey, true);
        EnsureFoldout(JumpFoldoutKey, true);
        EnsureFoldout(PhysicsFoldoutKey, false);
        EnsureFoldout("Ground Adhesion", true);
        EnsureFoldout(ExternalForcesFoldoutKey, false);
        EnsureFoldout(DebuggingFoldoutKey, false);
    }

    private void EnsureFoldout(string key, bool defaultValue)
    {
        if (!_foldouts.ContainsKey(key))
        {
            _foldouts.Add(key, defaultValue);
        }
    }

    //------------------------------- Events -------------------------------//

    private void SubscribeEvents()
    {
        EditorApplication.update += OnEditorUpdate;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void UnsubscribeEvents()
    {
        EditorApplication.update -= OnEditorUpdate;
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnEditorUpdate()
    {
        if (!Application.isPlaying || _controller == null)
        {
            return;
        }

        double currentTime = EditorApplication.timeSinceStartup;

        if (currentTime - _lastMonitorTime < MonitorInterval)
        {
            return;
        }

        _lastMonitorTime = currentTime;
        _lastVelocity = _controller.Velocity;
        _lastPosition = _controller.transform.position;
        _velocityHistory.Add(_lastVelocity);

        if (_velocityHistory.Count > MaximumVelocityHistory)
        {
            _velocityHistory.RemoveAt(0);
        }

        Repaint();
    }

    //------------------------------- Draw -------------------------------//

    private void DrawScriptReference()
    {
        MonoScript script = MonoScript.FromMonoBehaviour(target as MonoBehaviour);
        EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
        EditorGUILayout.Space(3f);
    }

    private void DrawChecks()
    {
        DrawFoldoutSection(
            QuickActionsFoldoutKey,
            () =>
            {
                using (new EditorGUI.DisabledScope(!Application.isPlaying || targets.Length > 1))
                {
                    EditorGUILayout.BeginHorizontal();

                    if (GUILayout.Button("Reset Locomotion", CustomEditorStyles.StandardButtonStyle))
                    {
                        RecordTarget("Reset Locomotion");
                        _controller.ResetLocomotionRuntimeState();
                    }

                    if (GUILayout.Button("Zero Velocity", CustomEditorStyles.StandardButtonStyle))
                    {
                        RecordTarget("Zero Velocity");
                        _controller.ZeroVelocity();
                    }

                    EditorGUILayout.EndHorizontal();

                    string controlLabel = _controller != null && _controller.State.ControlEnabled
                        ? "Disable Control"
                        : "Enable Control";

                    if (GUILayout.Button(controlLabel, CustomEditorStyles.StandardButtonStyle))
                    {
                        RecordTarget(controlLabel);
                        _controller.SetControlEnabled(!_controller.State.ControlEnabled);
                    }
                }
            });

        DrawFoldoutSection(
            RuntimeStatusFoldoutKey,
            () =>
            {
                if (!Application.isPlaying || _controller == null)
                {
                    EditorGUILayout.HelpBox("Runtime movement status is available in Play Mode.", MessageType.Info);
                    return;
                }

                DrawRuntimeStatus();
            });
    }

    private void DrawComponents()
    {
        DrawFoldoutSection(
            ComponentsFoldoutKey,
            () =>
            {
                DrawProperty(_bodyProp);
                DrawProperty(_capsuleColliderProp);
                DrawProperty(_visualRootProp);
                DrawProperty(_gameInputProp);
                DrawProperty(_stateProp, true);
                DrawProperty(_flipVisualWithFacingProp);
            });
    }

    private void DrawMovement()
    {
        DrawFoldoutSection(
            MovementFoldoutKey,
            () => DrawProperty(_movementProp, true));
    }

    private void DrawJump()
    {
        DrawFoldoutSection(
            JumpFoldoutKey,
            () => DrawProperty(_jumpProp, true));
    }

    private void DrawPhysics()
    {
        DrawFoldoutSection(
            PhysicsFoldoutKey,
            () =>
            {
                DrawProperty(_surfaceProbeProp, true);
                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("Gravity", EditorStyles.boldLabel);
                DrawProperty(_gravityScaleProp);
                DrawProperty(_jumpAscentGravityMultiplierProp);
                DrawProperty(_fallGravityMultiplierProp);
                DrawProperty(_maximumFallSpeedProp);
                DrawProperty(_steepSlopeJumpCheckDistanceProp);
            });

        DrawFoldoutSection(
            "Ground Adhesion",
            () =>
            {
                DrawProperty(_groundAdhesionForceProp);
                DrawProperty(_exportGroundAdhesionLogProp);
                DrawProperty(_groundAdhesionLogIntervalProp);

                using (new EditorGUI.DisabledScope(true))
                {
                    DrawProperty(_groundAdhesionLogPathProp);
                    DrawProperty(_groundAdhesionLogErrorProp);
                }
            });
    }

    private void DrawExternalForces()
    {
        DrawFoldoutSection(
            ExternalForcesFoldoutKey,
            () =>
            {
                DrawProperty(_externalForcesProp, true);
                DrawProperty(_movingPlatformsProp, true);
            });
    }

    private void DrawDebugging()
    {
        DrawFoldoutSection(
            DebuggingFoldoutKey,
            () =>
            {
                if (!Application.isPlaying || _controller == null)
                {
                    EditorGUILayout.HelpBox("Velocity monitoring is available in Play Mode.", MessageType.Info);
                    return;
                }

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Vector2Field("Velocity", _lastVelocity);
                    EditorGUILayout.Vector3Field("Position", _lastPosition);
                    EditorGUILayout.FloatField(
                        new GUIContent(
                            "Horizontal Distance / Fixed Frame",
                            "World-space horizontal distance measured between FixedUpdate calls."),
                        _controller.HorizontalDistancePerFixedFrame);
                    EditorGUILayout.FloatField(
                        "Expected Flat Distance",
                        _controller.Movement.MaximumGroundSpeed * Time.fixedDeltaTime);
                }

                Rect graphRect = GUILayoutUtility.GetRect(
                    EditorGUIUtility.currentViewWidth - 40f,
                    110f);

                if (Event.current.type == EventType.Repaint)
                {
                    GUI.Box(graphRect, GUIContent.none);
                    DrawVelocityGraph(graphRect);
                }
            });
    }

    private void DrawRuntimeStatus()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Locomotion State", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Current States", _controller.State.CurrentState.ToString());
        EditorGUILayout.IntField("State Transitions", _controller.State.TransitionCount);
        EditorGUILayout.Toggle("Control Enabled", _controller.State.ControlEnabled);
        EditorGUILayout.Toggle("Grounded", _controller.State.Grounded);
        EditorGUILayout.Toggle("Walking", _controller.State.IsWalking);
        EditorGUILayout.Toggle("Jumping", _controller.State.IsJumping);
        EditorGUILayout.Toggle("Falling", _controller.State.Falling);
        EditorGUILayout.Toggle("On Slope", _controller.State.OnWalkableSlope);
        EditorGUILayout.Toggle("On Steep Slope", _controller.State.OnSteepSlope);
        EditorGUILayout.Toggle("Sliding", _controller.State.Sliding);
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
        EditorGUILayout.FloatField("Movement Input", _controller.MovementInput);
        EditorGUILayout.FloatField("Facing Direction", _controller.FacingDirection);
        EditorGUILayout.FloatField(
            "Steep Block Direction",
            _controller.SteepSlopeBlockDirection);
        EditorGUILayout.Vector2Field("Velocity", _controller.Velocity);
        EditorGUILayout.FloatField("Speed", _controller.Speed);
        EditorGUILayout.FloatField("Grounded Time", _controller.SurfaceProbe.GroundedTime);
        EditorGUILayout.Toggle("External Force", _controller.ExternalForces.HasExternalForce);
        EditorGUILayout.Toggle("Input Protected", _controller.ExternalForces.IsControlProtected);
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Jump", EditorStyles.boldLabel);
        EditorGUILayout.Toggle("Buffered", _controller.Jump.HasBufferedJump);
        EditorGUILayout.Toggle("Coyote Time", _controller.Jump.CanUseCoyoteTime);
        EditorGUILayout.Toggle("Anticipation", _controller.Jump.IsInAnticipationFrame);
        EditorGUILayout.Toggle("Ascent", _controller.Jump.IsInJumpAscentState);
        EditorGUILayout.Toggle("Jump Disabled", _controller.Jump.IsJumpDisabled);
        EditorGUILayout.Toggle("Steep Slope Restricted", _controller.Jump.IsSteepSlopeJumpRestricted);
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Current Device",
            _controller.Input != null ? _controller.Input.GetCurrentDevice() : "N/A");
        EditorGUILayout.Toggle(
            "Game Controls Enabled",
            _controller.Input != null && _controller.Input.GameControlsEnabled);
        EditorGUILayout.Toggle(
            "System Controls Enabled",
            _controller.Input != null && _controller.Input.SystemControlsEnabled);
        EditorGUILayout.EndVertical();
    }

    private void DrawVelocityGraph(Rect rect)
    {
        if (_velocityHistory.Count < 2)
        {
            return;
        }

        float maximumVelocity = 10f;

        for (int i = 0; i < _velocityHistory.Count; i++)
        {
            maximumVelocity = Mathf.Max(
                maximumVelocity,
                Mathf.Abs(_velocityHistory[i].x),
                Mathf.Abs(_velocityHistory[i].y));
        }

        Handles.color = Color.gray;
        float centerY = rect.y + rect.height * 0.5f;
        Handles.DrawLine(
            new Vector3(rect.x, centerY),
            new Vector3(rect.xMax, centerY));

        DrawVelocityChannel(rect, maximumVelocity, true, Color.green);
        DrawVelocityChannel(rect, maximumVelocity, false, Color.red);
    }

    private void DrawVelocityChannel(Rect rect, float maximumVelocity, bool horizontal, Color color)
    {
        Handles.color = color;

        for (int i = 0; i < _velocityHistory.Count - 1; i++)
        {
            float firstX = rect.x + i / (float)(_velocityHistory.Count - 1) * rect.width;
            float secondX = rect.x + (i + 1) / (float)(_velocityHistory.Count - 1) * rect.width;
            float firstValue = horizontal ? _velocityHistory[i].x : _velocityHistory[i].y;
            float secondValue = horizontal ? _velocityHistory[i + 1].x : _velocityHistory[i + 1].y;
            float firstY = rect.center.y - firstValue / maximumVelocity * rect.height * 0.5f;
            float secondY = rect.center.y - secondValue / maximumVelocity * rect.height * 0.5f;

            Handles.DrawLine(
                new Vector3(firstX, firstY),
                new Vector3(secondX, secondY));
        }
    }

    //------------------------------- Scene -------------------------------//

    private void OnSceneGUI(SceneView sceneView)
    {
    }

    //------------------------------- Helpers -------------------------------//

    private void DrawFoldoutSection(string key, System.Action draw)
    {
        _foldouts[key] = EditorGUILayout.Foldout(
            _foldouts[key],
            key,
            true,
            CustomEditorStyles.FoldoutStyle);

        if (!_foldouts[key])
        {
            return;
        }

        EditorGUI.indentLevel++;
        draw?.Invoke();
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(3f);
    }

    private void DrawProperty(SerializedProperty property, bool includeChildren = false)
    {
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, includeChildren);
        }
    }

    private void RecordTarget(string label)
    {
        if (target == null)
        {
            return;
        }

        Undo.RecordObject(target, label);
        EditorUtility.SetDirty(target);
    }

    private bool HasValidTargets()
    {
        if (target == null || targets == null || targets.Length == 0 || targets[0] == null)
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
}
#endif
