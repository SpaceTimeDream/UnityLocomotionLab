using Portfolio.Character;
using UnityEditor;
using UnityEngine;

namespace Portfolio.Editor.Locomotion
{
    [CustomEditor(typeof(BasicCharacterLookTarget2D))]
    public class BasicCharacterLookTarget2DEditor : UnityEditor.Editor
    {
        //------------------------------- Scene GUI -------------------------------//

        #region Scene GUI

        private GUIStyle _labelStyle;

        private void OnSceneGUI()
        {
            BasicCharacterLookTarget2D lookTarget = (BasicCharacterLookTarget2D)target;

            if (lookTarget == null || !lookTarget.isActiveAndEnabled)
            {
                return;
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    normal = { textColor = Color.white },
                    padding = new RectOffset(8, 8, 4, 4)
                };
            }

            Vector3 position = lookTarget.LookPoint.position + Vector3.up * 0.55f;
            Handles.color = new Color(1f, 0.75f, 0.2f, 1f);
            Handles.DrawWireDisc(lookTarget.LookPoint.position, Vector3.forward, 0.12f);
            Handles.Label(position, $"LOOK TARGET  |  PRIORITY {lookTarget.Priority}", _labelStyle);
        }

        #endregion

        //------------------------------- Inspector -------------------------------//

        #region Inspector

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.HelpBox(
                "The target label and priority are shown above this target in the Game view while playing and in the Scene view while editing.",
                MessageType.Info);
        }

        #endregion
    }
}
