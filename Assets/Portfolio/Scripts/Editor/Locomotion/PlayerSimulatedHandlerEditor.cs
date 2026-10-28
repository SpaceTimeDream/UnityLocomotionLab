#if UNITY_EDITOR
using Portfolio.Editor.Shared;
using Portfolio.Locomotion.Rigidbody;
using Portfolio.Locomotion.Spline;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerSimulatedHandler), true)]
[CanEditMultipleObjects]
public sealed class PlayerSimulatedHandlerEditor : Editor
{
    //------------------------------- Unity Methods -------------------------------//

    #region Unity

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Player Status", CustomEditorStyles.SectionHeaderStyle);
        EditorGUILayout.LabelField("Status", PlayerRigidbodyStatus.CurrentStatus.ToString());
        EditorGUILayout.LabelField("Mode", PlayerRigidbodyStatus.CurrentMode.ToString());
        EditorGUILayout.LabelField("Physics Flags", PlayerRigidbodyStatus.CurrentState.ToString());
        EditorGUILayout.LabelField(
            "Status Transitions",
            PlayerRigidbodyStatus.StatusTransitionCount.ToString());
        EditorGUILayout.LabelField(
            "State Transitions",
            PlayerRigidbodyStatus.StateTransitionCount.ToString());
        EditorGUILayout.LabelField(
            "Mode Transitions",
            PlayerRigidbodyStatus.ModeTransitionCount.ToString());
        EditorGUILayout.LabelField(
            "Blocked Mutations",
            PlayerRigidbodyStatus.BlockedMutationCount.ToString());

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quick Actions", CustomEditorStyles.SectionHeaderStyle);

        foreach (Object selected in targets)
        {
            PlayerSimulatedHandler handler = selected as PlayerSimulatedHandler;
            if (handler == null)
            {
                continue;
            }

            PlayerSimulatedController controller = handler.GetComponent<PlayerSimulatedController>();
            if (controller == null)
            {
                EditorGUILayout.HelpBox("PlayerSimulatedController is missing from this GameObject.", MessageType.Warning);
                continue;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Find Player", CustomEditorStyles.CompactButtonStyle))
                {
                    FocusPlayer(handler);
                }

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("Reset Player", CustomEditorStyles.CompactButtonStyle))
                    {
                        RunAction(controller, () => ResetPlayer(controller));
                    }
                }
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string controlLabel = controller.State.ControlEnabled ? "Disable Control" : "Enable Control";
                    if (GUILayout.Button(controlLabel, CustomEditorStyles.CompactButtonStyle))
                    {
                        RunAction(controller, () =>
                            handler.SetControlEnabled(!controller.State.ControlEnabled));
                    }

                    string freezeLabel = controller.IsMovementFrozen ? "Unfreeze" : "Freeze";
                    if (GUILayout.Button(freezeLabel, CustomEditorStyles.CompactButtonStyle))
                    {
                        RunAction(controller, () =>
                        {
                            if (controller.IsMovementFrozen)
                            {
                                controller.UnFreezeMovement();
                            }
                            else
                            {
                                controller.FreezeMovement();
                            }
                        });
                    }
                }
            }
        }
    }

    #endregion

    //------------------------------- Helpers -------------------------------//

    #region Helpers

    private static void FocusPlayer(PlayerSimulatedHandler handler)
    {
        if (handler == null)
        {
            return;
        }

        GameObject player = handler.gameObject;
        Selection.activeGameObject = player;
        EditorGUIUtility.PingObject(player);

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            sceneView.FrameSelected();
            sceneView.Focus();
        }
    }

    private static void ResetPlayer(PlayerSimulatedController controller)
    {
        if (controller == null)
        {
            return;
        }

        controller.UnFreezeMovement();
        controller.ResetLocomotionRuntimeState();
        controller.SetControlEnabled(true);

        if (controller.MainCollider != null)
        {
            controller.MainCollider.enabled = true;
        }
    }

    private static void RunAction(PlayerSimulatedController controller, System.Action action)
    {
        if (controller == null || action == null)
        {
            return;
        }

        Undo.RecordObject(controller, "Player Simulated Handler Quick Action");
        action.Invoke();
        EditorUtility.SetDirty(controller);
    }

    #endregion
}
#endif
