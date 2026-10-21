#if UNITY_EDITOR
using Portfolio.Locomotion.Spline;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SimulatedSplineLocomotionController), true)]
[CanEditMultipleObjects]
public class SimulatedSplineLocomotionControllerEditor : Editor
{
    //------------------------------- Unity -------------------------------//

    #region Unity

    public override void OnInspectorGUI()
    {
        DrawScriptReference();
        DrawDefaultInspector();
    }

    #endregion

    //------------------------------- Draw -------------------------------//

    private void DrawScriptReference()
    {
        MonoScript script = MonoScript.FromMonoBehaviour(target as MonoBehaviour);
        EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
        EditorGUILayout.Space(3f);
    }
}
#endif
