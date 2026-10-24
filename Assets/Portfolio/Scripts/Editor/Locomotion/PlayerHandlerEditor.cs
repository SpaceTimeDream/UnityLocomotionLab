#if UNITY_EDITOR
using Portfolio.Locomotion.Rigidbody;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerHandler), true)]
[CanEditMultipleObjects]
public sealed class PlayerHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Player Status", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Status", PlayerStatus.CurrentStatus.ToString());
        EditorGUILayout.LabelField("Mode", PlayerStatus.CurrentMode.ToString());
        EditorGUILayout.LabelField("Physics Flags", PlayerStatus.CurrentState.ToString());
        EditorGUILayout.LabelField("Status Transitions", PlayerStatus.StatusTransitionCount.ToString());
        EditorGUILayout.LabelField("State Transitions", PlayerStatus.StateTransitionCount.ToString());
        EditorGUILayout.LabelField("Mode Transitions", PlayerStatus.ModeTransitionCount.ToString());
        EditorGUILayout.LabelField("Blocked Mutations", PlayerStatus.BlockedMutationCount.ToString());

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
        foreach (Object selected in targets)
        {
            PlayerHandler handler = selected as PlayerHandler;
            if (handler == null) continue;

            PlayerController controller = handler.GetComponent<PlayerController>();
            if (controller == null)
            {
                EditorGUILayout.HelpBox("PlayerController is missing from this GameObject.", MessageType.Warning);
                continue;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Runtime"))
                {
                    RunAction(controller, controller.ResetLocomotionRuntimeState);
                }

                if (GUILayout.Button("Zero Velocity"))
                {
                    RunAction(controller, controller.ZeroVelocity);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                string controlLabel = controller.State.ControlEnabled ? "Disable Control" : "Enable Control";
                if (GUILayout.Button(controlLabel))
                {
                    RunAction(controller, () => controller.SetControlEnabled(!controller.State.ControlEnabled));
                }

                string freezeLabel = controller.IsMovementFrozen ? "Unfreeze" : "Freeze";
                if (GUILayout.Button(freezeLabel))
                {
                    RunAction(controller, () =>
                    {
                        if (controller.IsMovementFrozen) controller.UnFreezeMovement();
                        else controller.FreezeMovement();
                    });
                }
            }
        }
    }

    private static void RunAction(PlayerController controller, System.Action action)
    {
        if (controller == null || action == null) return;
        Undo.RecordObject(controller, "Player Handler Quick Action");
        action.Invoke();
        EditorUtility.SetDirty(controller);
    }
}
#endif
