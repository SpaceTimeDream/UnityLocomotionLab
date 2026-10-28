#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Portfolio.Editor.Shared
{
    [InitializeOnLoad]
    public static class CustomEditorStyles
    {
        //------------------------------- Parameters -------------------------------//

        #region Components

        private static readonly Dictionary<Color, Texture2D> _backgroundTextures = new Dictionary<Color, Texture2D>();

        private static GUIStyle _largeHeaderStyle;
        private static GUIStyle _mediumHeaderStyle;
        private static GUIStyle _smallHeaderStyle;
        private static GUIStyle _sectionHeaderStyle;
        private static GUIStyle _foldoutStyle;
        private static GUIStyle _standardButtonStyle;
        private static GUIStyle _compactButtonStyle;
        private static GUIStyle _miniButtonStyle;
        private static GUIStyle _toggleButtonStyle;
        private static GUIStyle _smallIconButtonStyle;
        private static GUIStyle _miniIconButtonStyle;
        private static GUIStyle _helpBoxStyle;
        private static GUIStyle _primaryGroupBackgroundStyle;
        private static GUIStyle _secondaryGroupBackgroundStyle;
        private static GUIStyle _bannerStyle;
        private static GUIStyle _errorLabelStyle;
        private static GUIStyle _overlayLabelStyle;
        private static GUIStyle _overlayBackgroundStyle;

        private static GUIContent _addIconContent;
        private static GUIContent _deleteIconContent;
        private static GUIContent _duplicateIconContent;
        private static GUIContent _resetIconContent;
        private static GUIContent _visibilityIconContent;

        #endregion

        #region Properties

        public static GUIStyle LargeHeaderStyle =>
            _largeHeaderStyle ??= CreateHeaderStyle(18);

        public static GUIStyle MediumHeaderStyle =>
            _mediumHeaderStyle ??= CreateHeaderStyle(14);

        public static GUIStyle SmallHeaderStyle =>
            _smallHeaderStyle ??= CreateHeaderStyle(12);

        public static GUIStyle SectionHeaderStyle =>
            _sectionHeaderStyle ??= CreateSectionHeaderStyle();

        public static GUIStyle FoldoutStyle =>
            _foldoutStyle ??= CreateFoldoutStyle();

        public static GUIStyle StandardButtonStyle =>
            _standardButtonStyle ??= new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 24f
            };

        public static GUIStyle CompactButtonStyle =>
            _compactButtonStyle ??= new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 20f,
                padding = new RectOffset(6, 6, 2, 2)
            };

        public static GUIStyle MiniButtonStyle =>
            _miniButtonStyle ??= new GUIStyle(EditorStyles.miniButton);

        public static GUIStyle ToggleButtonStyle =>
            _toggleButtonStyle ??= new GUIStyle(EditorStyles.toolbarButton);

        public static GUIStyle SmallIconButtonStyle =>
            _smallIconButtonStyle ??= CreateIconButtonStyle(22f);

        public static GUIStyle MiniIconButtonStyle =>
            _miniIconButtonStyle ??= CreateIconButtonStyle(18f);

        public static GUIStyle HelpBoxStyle =>
            _helpBoxStyle ??= new GUIStyle(EditorStyles.helpBox)
            {
                wordWrap = true
            };

        public static GUIStyle PrimaryGroupBackgroundStyle =>
            _primaryGroupBackgroundStyle ??= CreateGroupBackgroundStyle(
                new Color(0.22f, 0.22f, 0.22f, 0.35f));

        public static GUIStyle SecondaryGroupBackgroundStyle =>
            _secondaryGroupBackgroundStyle ??= CreateGroupBackgroundStyle(
                new Color(0.16f, 0.16f, 0.16f, 0.35f));

        public static GUIStyle BannerStyle =>
            _bannerStyle ??= CreateBannerStyle();

        public static GUIStyle ErrorLabelStyle =>
            _errorLabelStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(1f, 0.35f, 0.35f) },
                wordWrap = true
            };

        public static GUIStyle OverlayLabelStyle =>
            _overlayLabelStyle ??= CreateOverlayLabelStyle();

        public static GUIStyle OverlayBackgroundStyle =>
            _overlayBackgroundStyle ??= CreateGroupBackgroundStyle(
                new Color(0f, 0f, 0f, 0.65f));

        public static GUIContent AddIconContent =>
            _addIconContent ??= CreateIconContent("Toolbar Plus", "+", "Add");

        public static GUIContent DeleteIconContent =>
            _deleteIconContent ??= CreateIconContent("TreeEditor.Trash", "-", "Delete");

        public static GUIContent DuplicateIconContent =>
            _duplicateIconContent ??= CreateIconContent("TreeEditor.Duplicate", "D", "Duplicate");

        public static GUIContent ResetIconContent =>
            _resetIconContent ??= CreateIconContent("Refresh", "R", "Reset");

        public static GUIContent VisibilityIconContent =>
            _visibilityIconContent ??= CreateIconContent(
                "animationvisibilitytoggleon",
                "V",
                "Visibility");

        #endregion

        //------------------------------- Unity Methods -------------------------------//

        #region Unity
        static CustomEditorStyles()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Reset;
            EditorApplication.quitting += Reset;
        }

        #endregion

        //------------------------------- Core / Routines Methods -------------------------------//

        #region Styles

        internal static void Refresh()
        {
            Reset();
        }

        private static GUIStyle CreateHeaderStyle(int fontSize)
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
        }

        private static GUIStyle CreateSectionHeaderStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                padding = new RectOffset(4, 4, 4, 4),
                normal = { background = GetBackgroundTexture(new Color(0.24f, 0.24f, 0.24f, 0.8f)) }
            };
        }

        private static GUIStyle CreateFoldoutStyle()
        {
            return new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };
        }

        private static GUIStyle CreateIconButtonStyle(float size)
        {
            return new GUIStyle(EditorStyles.miniButton)
            {
                fixedWidth = size,
                fixedHeight = size,
                padding = new RectOffset(2, 2, 2, 2)
            };
        }

        private static GUIStyle CreateGroupBackgroundStyle(Color color)
        {
            return new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(0, 0, 4, 4),
                normal = { background = GetBackgroundTexture(color) }
            };
        }

        private static GUIStyle CreateBannerStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                padding = new RectOffset(8, 8, 8, 8),
                normal = { background = GetBackgroundTexture(new Color(0.18f, 0.28f, 0.42f, 0.75f)) }
            };
        }

        private static GUIStyle CreateOverlayLabelStyle()
        {
            return new GUIStyle(EditorStyles.whiteLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }

        private static GUIContent CreateIconContent(string iconName, string fallbackText, string tooltip)
        {
            GUIContent content = EditorGUIUtility.IconContent(iconName);

            if (content == null || content.image == null)
            {
                return new GUIContent(fallbackText, tooltip);
            }

            content.tooltip = tooltip;
            return content;
        }

        private static Texture2D GetBackgroundTexture(Color color)
        {
            if (_backgroundTextures.TryGetValue(color, out Texture2D texture) && texture != null)
            {
                return texture;
            }

            texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            _backgroundTextures[color] = texture;
            return texture;
        }

        #endregion

        //------------------------------- Helpers -------------------------------//

        #region Helpers

        private static void Reset()
        {
            _largeHeaderStyle = null;
            _mediumHeaderStyle = null;
            _smallHeaderStyle = null;
            _sectionHeaderStyle = null;
            _foldoutStyle = null;
            _standardButtonStyle = null;
            _compactButtonStyle = null;
            _miniButtonStyle = null;
            _toggleButtonStyle = null;
            _smallIconButtonStyle = null;
            _miniIconButtonStyle = null;
            _helpBoxStyle = null;
            _primaryGroupBackgroundStyle = null;
            _secondaryGroupBackgroundStyle = null;
            _bannerStyle = null;
            _errorLabelStyle = null;
            _overlayLabelStyle = null;
            _overlayBackgroundStyle = null;
            _addIconContent = null;
            _deleteIconContent = null;
            _duplicateIconContent = null;
            _resetIconContent = null;
            _visibilityIconContent = null;

            foreach (Texture2D texture in _backgroundTextures.Values)
            {
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }
            }

            _backgroundTextures.Clear();
        }

        #endregion
    }
}
#endif
