#if UNITY_EDITOR
using Portfolio.Input;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DualSenseController))]
public sealed class DualSenseControllerEditor : Editor
{
    //------------------------------- Unity Methods -------------------------------//

    #region Unity

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("HARDWARE TESTS", EditorStyles.boldLabel);

        DualSenseController controller = (DualSenseController)target;
        EditorGUILayout.LabelField("Connection", controller.IsConnected ? controller.DeviceName : "Disconnected");

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode and connect a controller to run hardware tests.", MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying || !controller.IsConnected))
        {
            if (GUILayout.Button("Test Rumble"))
            {
                controller.DebugTestRumble();
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying || !controller.IsConnected || !controller.IsDualSenseDevice))
            {
                if (GUILayout.Button("Test Light Bar"))
                {
                    controller.SetLightBarColor(new Color(0.1f, 0.45f, 1f, 1f));
                }

                if (GUILayout.Button("Test Adaptive Triggers"))
                {
                    controller.SetAdaptiveTriggerEffects(
                        DualSenseTriggerEffect.ContinuousResistance(2, 6),
                        DualSenseTriggerEffect.SectionResistance(3, 8, 5));
                }

                if (GUILayout.Button("Test Microphone LED"))
                {
                    controller.SetMicrophoneMuteLed(DualSenseMicLedState.Pulsating);
                }

                if (GUILayout.Button("Test Player LEDs"))
                {
                    controller.SetPlayerLeds(0b00111, DualSensePlayerLedBrightness.Medium);
                }

                if (GUILayout.Button("Reset Feedback"))
                {
                    controller.StopFeedback();
                    controller.SetMicrophoneMuteLed(DualSenseMicLedState.Off);
                    controller.SetPlayerLeds(0, DualSensePlayerLedBrightness.High);
                    controller.SetLightBarColor(Color.black);
                }
            }
        }
    }

    #endregion
}

[CustomEditor(typeof(DualShockController))]
public sealed class DualShockControllerEditor : Editor
{
    //------------------------------- Unity Methods -------------------------------//

    #region Unity

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("HARDWARE TESTS", EditorStyles.boldLabel);

        DualShockController controller = (DualShockController)target;
        EditorGUILayout.LabelField("Connection", controller.IsConnected ? controller.DeviceName : "Disconnected");

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode and connect a controller to run hardware tests.", MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying || !controller.IsConnected))
        {
            if (GUILayout.Button("Test Rumble"))
            {
                controller.DebugTestRumble();
            }

            if (GUILayout.Button("Stop Rumble"))
            {
                controller.StopRumble();
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying || !controller.IsConnected || !controller.SupportsLightBar))
            {
                if (GUILayout.Button("Test Light Bar"))
                {
                    controller.SetLightBarColor(new Color(0.1f, 0.45f, 1f, 1f));
                }

                if (GUILayout.Button("Test Rumble + Light Bar"))
                {
                    controller.DebugTestRumbleAndLightBar();
                }

                if (GUILayout.Button("Reset Light Bar"))
                {
                    controller.SetLightBarColor(Color.black);
                }
            }

            if (GUILayout.Button("Reset Haptics"))
            {
                controller.ResetHaptics();
            }
        }
    }

    #endregion
}
#endif
