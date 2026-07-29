//using UnityEditor;
//using UnityEngine;
//using System.Collections.Generic;
//using Portfolio.Editor.Shared;

//[CustomEditor(typeof(MonoBehaviour), true)]
//public class EditorTemplate : Editor
//{
//    //------------------------------- Constants -------------------------------//

//    private const string ComponentsFoldoutKey = "Components";
//    private const string VfxFoldoutKey = "VFX";

//    //------------------------------- Flags & SerializedProperties -------------------------------//

//    protected SerializedProperty _exampleProp;

//    //------------------------------- Internal Editors -------------------------------//

//    protected Editor _baseEditor;

//    //------------------------------- State -------------------------------//

//    protected Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();
//    private bool _isInitialized;

//    //------------------------------- Unity -------------------------------//

//    #region Unity

//    protected virtual void OnEnable()
//    {
//        if (!HasValidTargets())
//            return;

//        CacheSerializedProperties();
//        InitializeEditors();
//        InitializeFoldouts();
//        SubscribeEvents();
//        _isInitialized = true;
//    }

//    protected virtual void OnDisable()
//    {
//        UnsubscribeEvents();
//        CleanupEditors();
//        _isInitialized = false;
//    }

//    public override void OnInspectorGUI()
//    {
//        if (!_isInitialized || !HasValidTargets())
//            return;

//        serializedObject.Update();

//        DrawChecks();
//        DrawComponents();
//        DrawVfx();
//        DrawDefaultInspectorFallback();

//        serializedObject.ApplyModifiedProperties();
//    }

//    #endregion

//    //------------------------------- Cache -------------------------------//

//    protected virtual void CacheSerializedProperties()
//    {
//        _exampleProp = serializedObject.FindProperty("_example");
//    }

//    //------------------------------- Editors -------------------------------//

//    protected virtual void InitializeEditors()
//    {
//        if (_baseEditor == null && target != null)
//            _baseEditor = CreateEditor(target, typeof(Editor));
//    }

//    protected virtual void CleanupEditors()
//    {
//        if (_baseEditor != null)
//            DestroyImmediate(_baseEditor);
//    }

//    //------------------------------- Foldouts -------------------------------//

//    protected virtual void InitializeFoldouts()
//    {
//        EnsureFoldout(ComponentsFoldoutKey, true);
//        EnsureFoldout(VfxFoldoutKey, false);
//    }

//    protected void EnsureFoldout(string key, bool defaultValue)
//    {
//        if (!_foldouts.ContainsKey(key))
//            _foldouts.Add(key, defaultValue);
//    }

//    //------------------------------- Events -------------------------------//

//    protected virtual void SubscribeEvents()
//    {
//        SceneView.duringSceneGui += OnSceneGUI;
//    }

//    protected virtual void UnsubscribeEvents()
//    {
//        SceneView.duringSceneGui -= OnSceneGUI;
//    }

//    //------------------------------- Draw -------------------------------//

//    protected virtual void DrawChecks()
//    {
//    }

//    protected virtual void DrawComponents()
//    {
//        DrawFoldoutSection(
//            ComponentsFoldoutKey,
//            () =>
//            {
//                if (_exampleProp != null)
//                    EditorGUILayout.PropertyField(_exampleProp);
//            }
//        );
//    }

//    protected virtual void DrawVfx()
//    {
//        DrawFoldoutSection(
//            VfxFoldoutKey,
//            () =>
//            {
//            }
//        );
//    }

//    protected virtual void DrawDefaultInspectorFallback()
//    {
//        if (_baseEditor != null)
//            _baseEditor.OnInspectorGUI();
//    }

//    //------------------------------- Scene -------------------------------//

//    protected virtual void OnSceneGUI(SceneView sceneView)
//    {
//    }

//    //------------------------------- Helpers -------------------------------//

//    protected void DrawFoldoutSection(string key, System.Action draw)
//    {
//        _foldouts[key] = EditorGUILayout.Foldout(
//            _foldouts[key],
//            key,
//            true,
//            CustomEditorStyles.FoldoutStyle
//        );

//        if (!_foldouts[key])
//            return;

//        EditorGUI.indentLevel++;
//        draw?.Invoke();
//        EditorGUI.indentLevel--;
//    }

//    protected void RecordTarget(string label)
//    {
//        if (target == null)
//            return;

//        Undo.RecordObject(target, label);
//        EditorUtility.SetDirty(target);
//    }

//    private bool HasValidTargets()
//    {
//        if (target == null || targets == null || targets.Length == 0 || targets[0] == null)
//            return false;

//        for (int i = 0; i < targets.Length; i++)
//        {
//            if (targets[i] == null)
//                return false;
//        }

//        return true;
//    }

//    protected void ValidateList<T>(List<T> list) where T : Object
//    {
//        if (list == null)
//            return;

//        list.RemoveAll(item => item == null);
//    }
//}
